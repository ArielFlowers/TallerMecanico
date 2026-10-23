using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class OrdenServicioService(
    IUnidadTrabajoOrdenPort unidadTrabajo,
    ICurrentUser currentUser,
    ValidacionOrdenes validador,
    TimeProvider reloj,
    ILogger<OrdenServicioService> logger)
{
    private const decimal ImporteMaximo = 9999999999999999.99m;
    private static readonly TimeSpan DesfaseBolivia = TimeSpan.FromHours(-4);

    public DateTime FechaActual => reloj.GetUtcNow().ToOffset(DesfaseBolivia).Date;
    public bool PuedeCrear => UsuarioValido && currentUser.Rol is "Administrador" or "Recepcionista";
    public bool PuedeAnular => UsuarioValido && currentUser.Rol == "Administrador";
    private bool UsuarioValido => currentUser.EstaAutenticado && !string.IsNullOrWhiteSpace(currentUser.Username);

    public ResultadoOrden CrearOrden(CrearOrdenSolicitud solicitud)
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
            Orden? existente = transaccion.Ordenes.ObtenerPorToken(solicitud.Token);
            if (existente is not null) return RecuperarReenvio(existente);

            var (erroresCabecera, servicios) = ValidarCabecera(transaccion.Cabecera, solicitud);
            if (erroresCabecera.Count > 0) return ResultadoOrden.Fallo(erroresCabecera);
            var cantidadesServicios = solicitud.CantidadesServicios?.ToDictionary(linea => linea.ServicioId, linea => linea.Cantidad) ?? [];
            decimal costoServicios = servicios.Sum(servicio => servicio.Costo * (servicio.UnidadCantidad is null ? 1 : cantidadesServicios.GetValueOrDefault(servicio.Id, 1)));

            var productos = transaccion.Productos.BloquearPorId(lineas.Select(linea => linea.ProductoId))
                .ToDictionary(producto => producto.Id);
            // Otro envío pudo confirmar el mismo token mientras esperábamos los bloqueos.
            existente = transaccion.Ordenes.ObtenerPorToken(solicitud.Token);
            if (existente is not null) return RecuperarReenvio(existente);

            var erroresStock = ValidarStock(lineas, productos);
            if (erroresStock.Count > 0) return ResultadoOrden.Fallo(erroresStock);

            decimal total = lineas.Sum(linea => productos[linea.ProductoId].Precio * linea.Cantidad)
                + costoServicios;
            if (total > ImporteMaximo) return ResultadoOrden.Fallo("Lineas", "El importe total supera el máximo permitido.");

            var orden = new Orden
            {
                VehiculoId = solicitud.VehiculoId, MecanicoId = solicitud.MecanicoId, Fecha = solicitud.Fecha,
                ServicioId = servicios[0].Id,
                CostoServicio = servicios[0].Costo * (servicios[0].UnidadCantidad is null ? 1 : cantidadesServicios.GetValueOrDefault(servicios[0].Id, 1)),
                Servicios = servicios.Select(servicio => new DetalleServicioOrden
                {
                    ServicioId = servicio.Id,
                    Nombre = servicio.Nombre,
                    Descripcion = servicio.Descripcion,
                    Costo = servicio.Costo * (servicio.UnidadCantidad is null ? 1 : cantidadesServicios.GetValueOrDefault(servicio.Id, 1)),
                    TiempoEstimadoHoras = servicio.TiempoEstimadoHoras * (servicio.UnidadCantidad is null ? 1 : cantidadesServicios.GetValueOrDefault(servicio.Id, 1)),
                    Cantidad = servicio.UnidadCantidad is null ? 1 : cantidadesServicios.GetValueOrDefault(servicio.Id, 1),
                    UnidadCantidad = servicio.UnidadCantidad
                }).ToList(),
                Token = solicitud.Token, CreadoPor = currentUser.Username!, FechaCreacion = reloj.GetUtcNow().UtcDateTime
            };
            transaccion.Ordenes.Insertar(orden);
            foreach (DetalleServicioOrden servicio in orden.Servicios)
            {
                servicio.OrdenId = orden.Id;
                transaccion.Ordenes.InsertarServicio(servicio);
            }
            foreach (LineaOrdenSolicitud linea in lineas)
            {
                decimal precio = productos[linea.ProductoId].Precio;
                transaccion.Detalles.Insertar(new DetalleOrden
                {
                    OrdenId = orden.Id, ProductoId = linea.ProductoId, Cantidad = linea.Cantidad,
                    PrecioUnitario = precio, Subtotal = precio * linea.Cantidad
                });
                if (!transaccion.Productos.Descontar(linea.ProductoId, linea.Cantidad))
                    throw new PersistenciaOrdenException("No se pudo descontar el stock bloqueado.");
            }
            transaccion.Ordenes.ActualizarTotal(orden.Id, total);
            orden.Total = total;
            transaccion.Confirmar();
            return ResultadoOrden.Correcto(orden);
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

    public ResultadoOrden AnularOrden(int ordenId)
    {
        if (!PuedeAnular) return ResultadoOrden.Fallo(string.Empty, "Solo un administrador puede anular órdenes.");
        if (ordenId <= 0) return ResultadoOrden.Fallo("OrdenId", "Selecciona una orden válida.");
        try
        {
            using ITransaccionOrden transaccion = unidadTrabajo.Iniciar();
            Orden? orden = transaccion.Ordenes.ObtenerParaAnular(ordenId);
            if (orden is null) return ResultadoOrden.Fallo("OrdenId", "La orden no existe.");
            if (orden.Estado == EstadoOrden.Anulada) return ResultadoOrden.Fallo("OrdenId", "La orden ya está anulada.");

            var detalles = transaccion.Detalles.ObtenerPorOrden(ordenId);
            var productos = transaccion.Productos.BloquearPorId(detalles.Select(detalle => detalle.ProductoId));
            if (productos.Count != detalles.Count)
                throw new PersistenciaOrdenException("No se encontraron todos los productos de la orden.");

            DateTime fechaUtc = reloj.GetUtcNow().UtcDateTime;
            if (!transaccion.Ordenes.MarcarAnulada(ordenId, currentUser.Username!, fechaUtc))
                throw new PersistenciaOrdenException("No se pudo marcar la orden como anulada.");
            foreach (DetalleOrden detalle in detalles)
                if (!transaccion.Productos.Restituir(detalle.ProductoId, detalle.Cantidad))
                    throw new PersistenciaOrdenException("No se pudo restituir el stock de un producto.");

            orden.Estado = EstadoOrden.Anulada;
            orden.AnuladoPor = currentUser.Username;
            orden.FechaAnulacion = fechaUtc;
            transaccion.Confirmar();
            return ResultadoOrden.Correcto(orden);
        }
        catch (PersistenciaOrdenException error)
        {
            logger.LogError(error, "Falló la anulación de la orden {OrdenId}.", ordenId);
            return ResultadoOrden.Fallo(string.Empty, "No se pudo confirmar la anulación. Consulta el estado de la orden antes de reintentar.");
        }
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

    private static (Dictionary<string, string> Errores, IReadOnlyList<Servicio> Servicios) ValidarCabecera(ICabeceraOrdenPort cabecera, CrearOrdenSolicitud solicitud)
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
