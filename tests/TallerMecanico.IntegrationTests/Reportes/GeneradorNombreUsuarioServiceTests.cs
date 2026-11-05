using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;
[Trait("Area", "Backend")]
public class GeneradorNombreUsuarioServiceTests
{
    private sealed class PuertosFalsos : IUsuarioPort, ISolicitudRegistroPort
    {
        private readonly HashSet<string> _usuarios = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _solicitudes = new(StringComparer.OrdinalIgnoreCase);

        public void AgregarUsuario(string username) => _usuarios.Add(username);
        public void AgregarSolicitud(string username) => _solicitudes.Add(username);

        public Usuario? ObtenerPorUsername(string username) => null;
        public Usuario? ObtenerPorId(int id) => null;
        public bool ExisteUsername(string username) =>
            _usuarios.Contains(username) || _solicitudes.Contains(username);
        public bool ExisteEmail(string email) => false;
        public void Agregar(Usuario usuario) => _usuarios.Add(usuario.Username);
        public void Actualizar(Usuario usuario) { }

        public void Agregar(SolicitudRegistro solicitud) => _solicitudes.Add(solicitud.Username);
        public SolicitudRegistro? ObtenerPorId(long id) => null;
        public SolicitudRegistro? ObtenerPorTokenHash(string tokenHash) => null;
        public SolicitudRegistro? ObtenerPendientePorEmail(string email) => null;
        public bool IntentarRenovarTokenConLimite(
            long solicitudId, string nuevoTokenHash, DateTime nuevaExpiracion) => false;
        public SolicitudRegistro? ConfirmarEmail(string tokenHash) => null;
        public void Actualizar(SolicitudRegistro solicitud) { }
    }

    [Theory]
    [InlineData("Juan.Pérez@empresa.com", "juan.perez")]
    [InlineData("MARÍA_GONZÁLEZ@correo.com", "maria_gonzalez")]
    [InlineData("user@domain.com", "user")]
    public void GenerarUsuarioUnico_NormalizaAcentosYMinusculas(string correo, string esperado)
    {
        var puertos = new PuertosFalsos();
        var servicio = new GeneradorNombreUsuarioService(puertos, puertos);

        string resultado = servicio.GenerarUsuarioUnico(correo);

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void GenerarUsuarioUnico_RechazaCorreoVacio()
    {
        var servicio = new GeneradorNombreUsuarioService(new PuertosFalsos(), new PuertosFalsos());

        Assert.Throws<ArgumentException>(() => servicio.GenerarUsuarioUnico(""));
    }

    [Fact]
    public void GenerarUsuarioUnico_AgregaSufijoIncrementalCuandoColisiona()
    {
        var puertos = new PuertosFalsos();
        puertos.AgregarUsuario("ana.lopez");
        var servicio = new GeneradorNombreUsuarioService(puertos, puertos);

        string resultado = servicio.GenerarUsuarioUnico("Ana.Lopez@empresa.com");

        Assert.Equal("ana.lopez-1", resultado);
    }

    [Fact]
    public void GenerarUsuarioUnico_RespetraLongitudMaximaConSufijo()
    {
        var puertos = new PuertosFalsos();
        string baseLargo = new string('a', 49);
        puertos.AgregarUsuario(baseLargo);
        var servicio = new GeneradorNombreUsuarioService(puertos, puertos);

        string resultado = servicio.GenerarUsuarioUnico($"{baseLargo}@empresa.com");

        Assert.True(resultado.Length <= 50);
        Assert.EndsWith("-1", resultado);
    }
}
