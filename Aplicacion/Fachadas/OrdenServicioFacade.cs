using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.Aplicacion.Fachadas;

public sealed class OrdenServicioFacade(OrdenServicioService servicio, ITokenOrdenPort tokens)
{
    public DateTime FechaActual => servicio.FechaActual;

    public Guid GenerarTokenFormulario()
    {
        if (!servicio.PuedeCrear) throw new UnauthorizedAccessException("No tienes permiso para crear órdenes.");
        return tokens.Generar();
    }

    public ResultadoOrden CrearOrden(CrearOrdenSolicitud solicitud)
    {
        if (!servicio.PuedeCrear) return ResultadoOrden.Fallo(string.Empty, "No tienes permiso para crear órdenes.");
        if (!tokens.EsValido(solicitud.Token))
            return ResultadoOrden.Fallo("Token", "El formulario venció o no pertenece a esta sesión. Revisa las órdenes antes de iniciar uno nuevo.");
        return servicio.CrearOrden(solicitud);
    }

    public ResultadoOrden AnularOrden(int ordenId) => servicio.AnularOrden(ordenId);
}
