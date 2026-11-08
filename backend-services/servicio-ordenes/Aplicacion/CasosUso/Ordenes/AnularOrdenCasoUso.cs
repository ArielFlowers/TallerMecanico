using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Contratos;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed class AnularOrdenCasoUso(
    IUnidadTrabajoOrden unidadTrabajo,
    ICurrentUser currentUser,
    TimeProvider reloj,
    ILogger<AnularOrdenCasoUso> logger,
    IAuditoriaPort? auditoria = null)
{
    private bool PuedeAnular => currentUser.EstaAutenticado
        && !string.IsNullOrWhiteSpace(currentUser.Username)
        && currentUser.Rol == Roles.Administrador;

    public ResultadoOrden Ejecutar(int ordenId)
    {
        if (!PuedeAnular) return ResultadoOrden.Fallo(string.Empty, "Solo un administrador puede anular órdenes.");
        if (ordenId <= 0) return ResultadoOrden.Fallo("OrdenId", "Selecciona una orden válida.");
        try
        {
            using ITransaccionOrden transaccion = unidadTrabajo.Iniciar();
            Orden? orden = transaccion.Ordenes.ObtenerParaAnular(ordenId);
            if (orden is null) return ResultadoOrden.Fallo("OrdenId", "La orden no existe.");
            if (!orden.PuedeAnularse()) return ResultadoOrden.Fallo("OrdenId", "La orden ya está anulada.");

            var detalles = transaccion.Detalles.ObtenerPorOrden(ordenId);
            var productos = transaccion.Productos.BloquearPorId(detalles.Select(detalle => detalle.ProductoId));
            if (productos.Count != detalles.Count)
                throw new PersistenciaOrdenException("No se encontraron todos los productos de la orden.");

            orden.Anular(currentUser.Username!, reloj.GetUtcNow().UtcDateTime);
            if (!transaccion.Ordenes.MarcarAnulada(ordenId, orden.AnuladoPor!, orden.FechaAnulacion!.Value))
                throw new PersistenciaOrdenException("No se pudo marcar la orden como anulada.");
            foreach (DetalleOrden detalle in detalles)
                if (!transaccion.Productos.Restituir(detalle.ProductoId, detalle.Cantidad))
                    throw new PersistenciaOrdenException("No se pudo restituir el stock de un producto.");

            transaccion.Confirmar();
            RegistrarEnBitacora(orden, detalles.Count);
            return ResultadoOrden.Correcto(orden);
        }
        catch (PersistenciaOrdenException error)
        {
            logger.LogError(error, "Falló la anulación de la orden {OrdenId}.", ordenId);
            return ResultadoOrden.Fallo(string.Empty, "No se pudo confirmar la anulación. Consulta el estado de la orden antes de reintentar.");
        }
    }

    // La anulación es la baja lógica de la orden: queda en la bitácora como las bajas de los catálogos.
    // Se registra después de confirmar; si la bitácora falla, la anulación ya está hecha y solo se avisa.
    private void RegistrarEnBitacora(Orden orden, int repuestos)
    {
        try
        {
            auditoria?.Registrar(new RegistroAuditoria
            {
                UsuarioId = currentUser.UsuarioId,
                Username = currentUser.Username,
                Accion = "ORDEN_ANULADA",
                Entidad = "Orden",
                EntidadId = orden.Id.ToString(),
                Fecha = orden.FechaAnulacion!.Value,
                Detalle = $"Total: {orden.Total:0.00}. Repuestos restituidos: {repuestos}."
            });
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "La orden {OrdenId} se anuló, pero no se pudo registrar en la bitácora.", orden.Id);
        }
    }
}
