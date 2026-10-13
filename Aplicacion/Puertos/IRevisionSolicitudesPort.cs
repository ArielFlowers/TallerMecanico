using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IRevisionSolicitudesPort
{
    IReadOnlyList<SolicitudRegistro> ObtenerPendientes();

    bool ResolverSolicitud(
        long solicitudId,
        int administradorId,
        bool aprobar);
}