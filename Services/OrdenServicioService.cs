using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Validators;

namespace TallerMecanico.Services;

public sealed class OrdenServicioService(
    IUnidadTrabajoOrdenPort unidadTrabajo,
    ICurrentUser currentUser,
    ValidacionOrdenes validador,
    TimeProvider reloj,
    ILogger<OrdenServicioService> logger)
{
    private const decimal ImporteMaximo = 9999999999999999.99m;

    public bool PuedeCrear => UsuarioValido && currentUser.Rol is "Administrador" or "Recepcionista";
    public bool PuedeAnular => UsuarioValido && currentUser.Rol == "Administrador";
    private bool UsuarioValido => currentUser.EstaAutenticado && !string.IsNullOrWhiteSpace(currentUser.Username);

    public ResultadoOrden CrearOrden(CrearOrdenSolicitud solicitud)
    {
        if (!PuedeCrear) return ResultadoOrden.Fallo(string.Empty, "No tienes permiso para crear órdenes.");
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

            var erroresCabecera = ValidarCabecera(transaccion.Cabecera, solicitud);
            if (erroresCabecera.Count > 0) return ResultadoOrden.Fallo(erroresCabecera);

            var productos = transaccion.Productos.BloquearPorId(lineas.Select(linea => linea.ProductoId))
                .ToDictionary(producto => producto.Id);
            // Otro envío pudo confirmar el mismo token mientras esperábamos los bloqueos.
            existente = transaccion.Ordenes.ObtenerPorToken(solicitud.Token);
            if (existente is not null) return RecuperarReenvio(existente);

            var erroresStock = ValidarStock(lineas, productos);
            if (erroresStock.Count > 0) return ResultadoOrden.Fallo(erroresStock);

            decimal total = lineas.Sum(linea => productos[linea.ProductoId].Precio * linea.Cantidad);
            if (total > ImporteMaximo) return ResultadoOrden.Fallo("Lineas", "El importe total supera el máximo permitido.");

            var orden = new Orden
            {
                VehiculoId = solicitud.VehiculoId, MecanicoId = solicitud.MecanicoId, Fecha = solicitud.Fecha,
                Token = solicitud.Token, CreadoPor = currentUser.Username!, FechaCreacion = reloj.GetUtcNow().UtcDateTime
            };
            transaccion.Ordenes.Insertar(orden);
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

    private static Dictionary<string, string> ValidarCabecera(ICabeceraOrdenPort cabecera, CrearOrdenSolicitud solicitud)
    {
        var errores = new Dictionary<string, string>();
        if (!cabecera.ExisteVehiculo(solicitud.VehiculoId, out int? clienteId))
            errores[nameof(solicitud.VehiculoId)] = "El vehículo seleccionado no existe.";
        else if (!clienteId.HasValue)
            errores[nameof(solicitud.VehiculoId)] = "Asigna un cliente al vehículo antes de crear una orden.";
        if (!cabecera.ExisteMecanico(solicitud.MecanicoId))
            errores[nameof(solicitud.MecanicoId)] = "El mecánico seleccionado no existe.";
        return errores;
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
