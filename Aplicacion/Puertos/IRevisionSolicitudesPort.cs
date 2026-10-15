using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IRevisionSolicitudesPort
{
    IReadOnlyList<SolicitudRegistro> ObtenerPendientes();

    bool ResolverSolicitud(
        long solicitudId,
        int administradorId,
        bool aprobar);
}