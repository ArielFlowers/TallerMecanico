using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Contratos;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed class CrearOrdenCasoUso(
    IUnidadTrabajoOrden unidadTrabajo,
    ICurrentUser currentUser,
    ValidacionOrdenes validador,
    TimeProvider reloj,
    ILogger<CrearOrdenCasoUso> logger)
{
    private const decimal ImporteMaximo = 9999999999999999.99m;
    private static readonly TimeSpan DesfaseBolivia = TimeSpan.FromHours(-4);

    public DateTime FechaActual => reloj.GetUtcNow().ToOffset(DesfaseBolivia).Date;
    public bool PuedeCrear => UsuarioValido && currentUser.Rol is Roles.Administrador or Roles.Recepcionista;
    private bool UsuarioValido => currentUser.EstaAutenticado && !string.IsNullOrWhiteSpace(currentUser.Username);

    public ResultadoOrden Ejecutar(CrearOrdenSolicitud solicitud)
    {
        if (!PuedeCrear) return ResultadoOrden.Fallo(string.Empty, "No tienes permiso para crear órdenes.");
        solicitud = solicitud with { Fecha = FechaActual };
        var errores = validador.Validar(solicitud);
        if (errores.Count > 0) return ResultadoOrden.Fallo(errores);

        IReadOnlyList<LineaOrdenSolicitud> lineas;
        try { lineas = validador.AgruparLineas(solicitud.Lineas); }
        catch (OverflowException) { return ResultadoOrden.Fallo("Lineas", "La cantidad acumulada de un producto es demasiado grande."); }

        try
        {
            using ITransaccionOrden transaccion = unidadTrabajo.Iniciar();
            return RegistrarEnTransaccion(transaccion, solicitud, lineas);
        }
        catch (TokenOrdenDuplicadoException)
        {
            return RecuperarTokenConfirmado(solicitud.Token);
        }
        catch (PersistenciaOrdenException error)
        {
            logger.LogError(error, "Falló la creación de una orden con token {Token}.", solicitud.Token);
            return ResultadoOrden.Fallo(string.Empty, "No se pudo confirmar la orden. Reintenta con el mismo formulario.");
        }
    }

    // Todo ocurre dentro de la misma transacción: si algo falla antes de Confirmar, se revierte.
    private ResultadoOrden RegistrarEnTransaccion(
        ITransaccionOrden transaccion, CrearOrdenSolicitud solicitud, IReadOnlyList<LineaOrdenSolicitud> lineas)
    {
        Orden? existente = transaccion.Ordenes.ObtenerPorToken(solicitud.Token);
        if (existente is not null) return RecuperarReenvio(existente);

        var (erroresCabecera, servicios) = ValidarCabecera(transaccion.Cabecera, solicitud);
        if (erroresCabecera.Count > 0) return ResultadoOrden.Fallo(erroresCabecera);

        var productos = transaccion.Productos.BloquearPorId(lineas.Select(linea => linea.ProductoId))
            .ToDictionary(producto => producto.Id);
        // Otro envío pudo confirmar el mismo token mientras esperábamos los bloqueos.
        existente = transaccion.Ordenes.ObtenerPorToken(solicitud.Token);
        if (existente is not null) return RecuperarReenvio(existente);

        var erroresStock = ValidarStock(lineas, productos);
        if (erroresStock.Count > 0) return ResultadoOrden.Fallo(erroresStock);

        Orden orden = ArmarOrden(solicitud, servicios);
        var repuestos = lineas.Select(linea => CrearRepuesto(linea, productos[linea.ProductoId].Precio)).ToList();

        decimal total = orden.CalcularTotal(repuestos);
        if (total > ImporteMaximo) return ResultadoOrden.Fallo("Lineas", "El importe total supera el máximo permitido.");

        GuardarOrden(transaccion, orden, repuestos, total);
        transaccion.Confirmar();
        return ResultadoOrden.Correcto(orden);
    }

    private Orden ArmarOrden(CrearOrdenSolicitud solicitud, IReadOnlyList<Servicio> servicios)
    {
        var cantidades = solicitud.CantidadesServicios?.ToDictionary(linea => linea.ServicioId, linea => linea.Cantidad) ?? [];
        Servicio principal = servicios[0];
        return new Orden
        {
            VehiculoId = solicitud.VehiculoId, MecanicoId = solicitud.MecanicoId, Fecha = solicitud.Fecha,
            ServicioId = principal.Id,
            CostoServicio = principal.Costo * CantidadCobrada(principal, cantidades),
            Servicios = servicios.Select(servicio => CrearDetalleServicio(servicio, CantidadCobrada(servicio, cantidades))).ToList(),
            Token = solicitud.Token, CreadoPor = currentUser.Username!, FechaCreacion = reloj.GetUtcNow().UtcDateTime
        };
    }

    // Un servicio sin unidad de cantidad siempre cuenta como uno.
    private static int CantidadCobrada(Servicio servicio, IReadOnlyDictionary<int, int> cantidades) =>
        servicio.UnidadCantidad is null ? 1 : cantidades.GetValueOrDefault(servicio.Id, 1);

    private static DetalleServicioOrden CrearDetalleServicio(Servicio servicio, int cantidad) => new()
    {
        ServicioId = servicio.Id,
        Nombre = servicio.Nombre,
        Descripcion = servicio.Descripcion,
        Costo = servicio.Costo * cantidad,
        TiempoEstimadoHoras = servicio.TiempoEstimadoHoras * cantidad,
        Cantidad = cantidad,
        UnidadCantidad = servicio.UnidadCantidad
    };

    private static void GuardarOrden(ITransaccionOrden transaccion, Orden orden, IReadOnlyList<DetalleOrden> repuestos, decimal total)
    {
        transaccion.Ordenes.Insertar(orden);
        foreach (DetalleServicioOrden servicio in orden.Servicios)
        {
            servicio.OrdenId = orden.Id;
            transaccion.Ordenes.InsertarServicio(servicio);
        }
        foreach (DetalleOrden repuesto in repuestos)
        {
            repuesto.OrdenId = orden.Id;
            transaccion.Detalles.Insertar(repuesto);
            if (!transaccion.Productos.Descontar(repuesto.ProductoId, repuesto.Cantidad))
                throw new PersistenciaOrdenException("No se pudo descontar el stock bloqueado.");
        }
        transaccion.Ordenes.ActualizarTotal(orden.Id, total);
        orden.Total = total;
    }

    private static DetalleOrden CrearRepuesto(LineaOrdenSolicitud linea, decimal precio)
    {
        var repuesto = new DetalleOrden { ProductoId = linea.ProductoId, Cantidad = linea.Cantidad, PrecioUnitario = precio };
        if (!repuesto.EsValido())
            throw new PersistenciaOrdenException("El repuesto tiene una cantidad o un precio no válidos.");
        repuesto.Subtotal = repuesto.CalcularSubtotal();
        return repuesto;
    }

    private ResultadoOrden RecuperarReenvio(Orden orden) => orden.CreadoPor == currentUser.Username
        ? ResultadoOrden.Correcto(orden, esReenvio: true)
        : ResultadoOrden.Fallo("Token", "El token pertenece a otro usuario.");

    private ResultadoOrden RecuperarTokenConfirmado(Guid token)
    {
        try
        {
            using ITransaccionOrden consulta = unidadTrabajo.Iniciar();
            Orden? existente = consulta.Ordenes.ObtenerPorToken(token);
            return existente is null
                ? ResultadoOrden.Fallo("Token", "No se pudo recuperar la orden. Reintenta con el mismo formulario.")
                : RecuperarReenvio(existente);
        }
        catch (PersistenciaOrdenException error)
        {
            logger.LogError(error, "Falló la recuperación del token {Token}.", token);
            return ResultadoOrden.Fallo("Token", "No se pudo recuperar la orden. Reintenta con el mismo formulario.");
        }
    }

    private static (Dictionary<string, string> Errores, IReadOnlyList<Servicio> Servicios) ValidarCabecera(ICabeceraOrdenRepository cabecera, CrearOrdenSolicitud solicitud)
    {
        var errores = new Dictionary<string, string>();
        var servicios = new List<Servicio>();
        if (!cabecera.ExisteVehiculo(solicitud.VehiculoId, out int? clienteId))
            errores[nameof(solicitud.VehiculoId)] = "El vehículo seleccionado no existe.";
        else if (!clienteId.HasValue)
            errores[nameof(solicitud.VehiculoId)] = "Asigna un cliente al vehículo antes de crear una orden.";
        if (!cabecera.ExisteMecanico(solicitud.MecanicoId))
            errores[nameof(solicitud.MecanicoId)] = "El mecánico seleccionado no existe.";
        foreach (int servicioId in solicitud.ServiciosSeleccionados.Distinct())
        {
            if (!cabecera.ExisteServicio(servicioId, out Servicio? servicio) || servicio is null)
                errores[$"Servicios[{servicioId}]"] = "Uno de los servicios seleccionados ya no existe.";
            else if (!CatalogoServicios.EsSeleccionable(servicio))
                errores[$"Servicios[{servicioId}]"] = "El servicio seleccionado no pertenece al catálogo disponible de Nueva Orden.";
            else
                servicios.Add(servicio);
        }
        var cantidades = solicitud.CantidadesServicios?.ToDictionary(linea => linea.ServicioId, linea => linea.Cantidad) ?? [];
        foreach (Servicio servicio in servicios)
        {
            int cantidad = cantidades.GetValueOrDefault(servicio.Id, 1);
            if (cantidad < 1 || cantidad > 1000 || (servicio.UnidadCantidad is null && cantidad != 1))
                errores[$"Servicios[{servicio.Id}]"] = "La cantidad del servicio no es válida.";
        }
        if (cantidades.Keys.Except(servicios.Select(servicio => servicio.Id)).Any())
            errores["ServicioIds"] = "La orden contiene cantidades para servicios no seleccionados.";
        if (servicios.Count == 0)
            errores["ServicioIds"] = "Agrega al menos un servicio a la orden.";
        return (errores, servicios);
    }

    private static Dictionary<string, string> ValidarStock(IReadOnlyList<LineaOrdenSolicitud> lineas, IReadOnlyDictionary<int, Producto> productos)
    {
        var errores = new Dictionary<string, string>();
        foreach (LineaOrdenSolicitud linea in lineas)
        {
            if (!productos.TryGetValue(linea.ProductoId, out Producto? producto))
                errores[$"Productos[{linea.ProductoId}]"] = "El producto seleccionado no existe.";
            else if (producto.Stock < linea.Cantidad)
                errores[$"Productos[{linea.ProductoId}]"] = $"Stock insuficiente para {producto.Nombre}: disponibles {producto.Stock}, solicitados {linea.Cantidad}.";
        }
        return errores;
    }
}
