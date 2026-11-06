using TallerMecanico.Aplicacion.CasosUso.Ordenes;

namespace TallerMecanico.Aplicacion.Fachadas;

public sealed class OrdenServicioFacade(CrearOrdenCasoUso crearOrden, AnularOrdenCasoUso anularOrden)
{
    public DateTime FechaActual => crearOrden.FechaActual;

    public ResultadoOrden CrearOrden(CrearOrdenSolicitud solicitud)
    {
        if (!crearOrden.PuedeCrear) return ResultadoOrden.Fallo(string.Empty, "No tienes permiso para crear órdenes.");
        return crearOrden.Ejecutar(solicitud);
    }

    public ResultadoOrden AnularOrden(int ordenId) => anularOrden.Ejecutar(ordenId);
}
