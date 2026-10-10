using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Services;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class RevisionSolicitudesServiceTests
{
    [Fact]
    public void Administrador_PuedeAprobar()
    {
        var repositorio = new RevisionFalsa();

        var usuario = new UsuarioFalso
        {
            EstaAutenticado = true,
            UsuarioId = 1,
            Username = "admin",
            Rol = "Administrador"
        };

        var servicio = new RevisionSolicitudesService(
            repositorio, usuario);

        Assert.True(servicio.Aprobar(69));

        Assert.Equal(69, repositorio.SolicitudRecibida);
        Assert.Equal(1, repositorio.AdministradorRecibido);
        Assert.True(repositorio.AprobarRecibido);
    }

    [Fact]
    public void Administrador_PuedeRechazar()
    {
        var repositorio = new RevisionFalsa();

        var usuario = new UsuarioFalso
        {
            EstaAutenticado = true,
            UsuarioId = 1,
            Rol = "Administrador"
        };

        var servicio = new RevisionSolicitudesService(
            repositorio, usuario);

        Assert.True(servicio.Rechazar(69));

        Assert.False(repositorio.AprobarRecibido);
        Assert.Equal(1, repositorio.Llamadas);
    }

    [Fact]
    public void Recepcionista_NoPuedeAprobar()
    {
        var repositorio = new RevisionFalsa();

        var usuario = new UsuarioFalso
        {
            EstaAutenticado = true,
            UsuarioId = 2,
            Rol = "Recepcionista"
        };

        var servicio = new RevisionSolicitudesService(
            repositorio, usuario);

        Assert.Throws<UnauthorizedAccessException>(
            () => servicio.Aprobar(69));

        Assert.Equal(0, repositorio.Llamadas);
    }

    [Fact]
    public void UsuarioAnonimo_NoPuedeRevisarSolicitudes()
    {
        var repositorio = new RevisionFalsa();

        var usuario = new UsuarioFalso
        {
            EstaAutenticado = false,
            UsuarioId = null,
            Rol = null
        };

        var servicio = new RevisionSolicitudesService(
            repositorio, usuario);

        Assert.Throws<UnauthorizedAccessException>(
            () => servicio.ObtenerPendientes());

        Assert.Equal(0, repositorio.Llamadas);
    }

    private sealed class UsuarioFalso : ICurrentUser
    {
        public bool EstaAutenticado { get; set; }

        public int? UsuarioId { get; set; }

        public string? Username { get; set; }

        public string? Rol { get; set; }
    }

    private sealed class RevisionFalsa : IRevisionSolicitudesPort
    {
        public long SolicitudRecibida { get; private set; }

        public int AdministradorRecibido { get; private set; }

        public bool AprobarRecibido { get; private set; }

        public int Llamadas { get; private set; }

        public IReadOnlyList<SolicitudRegistro> ObtenerPendientes()
        {
            Llamadas++;

            return Array.Empty<SolicitudRegistro>();
        }

        public bool ResolverSolicitud(
            long solicitudId,
            int administradorId,
            bool aprobar)
        {
            Llamadas++;

            SolicitudRecibida = solicitudId;
            AdministradorRecibido = administradorId;
            AprobarRecibido = aprobar;

            return true;
        }
    }
}