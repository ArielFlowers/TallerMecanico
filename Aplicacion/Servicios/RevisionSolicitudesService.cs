using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

namespace TallerMecanico.Services;

public sealed class RevisionSolicitudesService
{
    private readonly IRevisionSolicitudesPort _revision;
    private readonly ICurrentUser _usuarioActual;

    public RevisionSolicitudesService(
        IRevisionSolicitudesPort revision,
        ICurrentUser usuarioActual)
    {
        _revision = revision;
        _usuarioActual = usuarioActual;
    }

    public IReadOnlyList<SolicitudRegistro> ObtenerPendientes()
    {
        ValidarAdministrador();

        return _revision.ObtenerPendientes();
    }

    public bool Aprobar(long solicitudId)
    {
        int administradorId = ValidarAdministrador();

        return _revision.ResolverSolicitud(
            solicitudId,
            administradorId,
            aprobar: true);
    }

    public bool Rechazar(long solicitudId)
    {
        int administradorId = ValidarAdministrador();

        return _revision.ResolverSolicitud(
            solicitudId,
            administradorId,
            aprobar: false);
    }

    private int ValidarAdministrador()
    {
        if (!_usuarioActual.EstaAutenticado ||
            _usuarioActual.Rol != "Administrador" ||
            _usuarioActual.UsuarioId is not int administradorId ||
            administradorId <= 0)
        {
            throw new UnauthorizedAccessException(
                "Solo un administrador puede revisar solicitudes.");
        }

        return administradorId;
    }
}