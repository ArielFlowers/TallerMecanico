using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

namespace TallerMecanico.Aplicacion.Fachadas;

public sealed class OrdenServicioFacade(CrearOrdenCasoUso crearOrden, AnularOrdenCasoUso anularOrden, ITokenOrden tokens)
{
    public DateTime FechaActual => crearOrden.FechaActual;

    public Guid GenerarTokenFormulario()
    {
        if (!crearOrden.PuedeCrear) throw new UnauthorizedAccessException("No tienes permiso para crear órdenes.");
        return tokens.Generar();
    }

    public ResultadoOrden CrearOrden(CrearOrdenSolicitud solicitud)
    {
        if (!crearOrden.PuedeCrear) return ResultadoOrden.Fallo(string.Empty, "No tienes permiso para crear órdenes.");
        if (!tokens.EsValido(solicitud.Token))
            return ResultadoOrden.Fallo("Token", "El formulario venció o no pertenece a esta sesión. Revisa las órdenes antes de iniciar uno nuevo.");
        return crearOrden.Ejecutar(solicitud);
    }

    public ResultadoOrden AnularOrden(int ordenId) => anularOrden.Ejecutar(ordenId);
}
