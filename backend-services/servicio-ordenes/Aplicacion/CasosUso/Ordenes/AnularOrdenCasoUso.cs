using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed class AnularOrdenCasoUso(
    IUnidadTrabajoOrden unidadTrabajo,
    ICurrentUser currentUser,
    TimeProvider reloj,
    ILogger<AnularOrdenCasoUso> logger)
{
    private bool PuedeAnular => currentUser.EstaAutenticado
        && !string.IsNullOrWhiteSpace(currentUser.Username)
        && currentUser.Rol == "Administrador";

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
            return ResultadoOrden.Correcto(orden);
        }
        catch (PersistenciaOrdenException error)
        {
            logger.LogError(error, "Falló la anulación de la orden {OrdenId}.", ordenId);
            return ResultadoOrden.Fallo(string.Empty, "No se pudo confirmar la anulación. Consulta el estado de la orden antes de reintentar.");
        }
    }
}
