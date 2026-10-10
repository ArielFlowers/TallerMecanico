using System.Security.Cryptography;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Services;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public class RegistroUsuarioServiceTests
{
    [Fact]
    public void CrearSolicitud_GuardaDatosSeguros()
    {
        var usuarios = new UsuariosFalsos();
        var solicitudes = new SolicitudesFalsas();
        var hasher = new HasherFalso();

        var servicio = new RegistroUsuarioService(
            usuarios, solicitudes, hasher);

        string token = servicio.CrearSolicitudPendiente(
            "empleado123",
            "Empleado@Empresa.com.bo",
            "ClaveSegura123");

        Assert.NotNull(solicitudes.Guardada);

        var guardada = solicitudes.Guardada!;

        Assert.Equal("empleado123", guardada.Username);
        Assert.Equal("empleado@empresa.com.bo", guardada.Email);
        Assert.Equal("PendienteVerificacion", guardada.Estado);
        Assert.Equal("HASH_DE_PRUEBA", guardada.PasswordHash);

        Assert.Equal(64, token.Length);
        Assert.NotEqual(token, guardada.TokenVerificacionHash);

        byte[] tokenBytes = Convert.FromHexString(token);

        string hashEsperado = Convert.ToHexString(
            SHA256.HashData(tokenBytes));

        Assert.Equal(hashEsperado, guardada.TokenVerificacionHash);

        Assert.NotNull(guardada.UltimoIntentoVerificacionEn);
        Assert.NotNull(guardada.TokenExpiraEn);
        Assert.True(guardada.TokenExpiraEn > guardada.FechaSolicitud);
    }

    [Theory]
    [InlineData("ab", "empleado@empresa.com", "ClaveSegura123")]
    [InlineData("empleado", "correo-invalido", "ClaveSegura123")]
    [InlineData("empleado", "empleado@empresa.com", "123")]
    public void CrearSolicitud_RechazaDatosInvalidos(
        string username, string email, string password)
    {
        var solicitudes = new SolicitudesFalsas();

        var servicio = new RegistroUsuarioService(
            new UsuariosFalsos(),
            solicitudes,
            new HasherFalso());

        Assert.Throws<ArgumentException>(() =>
            servicio.CrearSolicitudPendiente(
                username, email, password));

        Assert.Null(solicitudes.Guardada);
    }

    [Fact]
    public void CrearSolicitud_RechazaUsuarioExistente()
    {
        var usuarios = new UsuariosFalsos
        {
            UsernameExiste = true
        };

        var solicitudes = new SolicitudesFalsas();

        var servicio = new RegistroUsuarioService(
            usuarios, solicitudes, new HasherFalso());

        Assert.Throws<InvalidOperationException>(() =>
            servicio.CrearSolicitudPendiente(
                "empleado123",
                "empleado@empresa.com",
                "ClaveSegura123"));

        Assert.Null(solicitudes.Guardada);
    }

    [Fact]
    public void CrearSolicitud_RechazaEmailPendiente()
    {
        var solicitudes = new SolicitudesFalsas
        {
            EmailExiste = true
        };

        var servicio = new RegistroUsuarioService(
            new UsuariosFalsos(),
            solicitudes,
            new HasherFalso());

        Assert.Throws<InvalidOperationException>(() =>
            servicio.CrearSolicitudPendiente(
                "empleado123",
                "empleado@empresa.com",
                "ClaveSegura123"));

        Assert.Null(solicitudes.Guardada);
    }

    private sealed class HasherFalso : IPasswordHasher
    {
        public string GenerarHash(string password)
            => "HASH_DE_PRUEBA";

        public bool VerificarPassword(
            string password, string passwordHash)
            => passwordHash == "HASH_DE_PRUEBA";
    }

    private sealed class UsuariosFalsos : IUsuarioPort
    {
        public bool UsernameExiste { get; set; }
        public bool EmailExiste { get; set; }

        public Usuario? ObtenerPorUsername(string username) => null;
        public Usuario? ObtenerPorId(int id) => null;

        public bool ExisteUsername(string username) => UsernameExiste;
        public bool ExisteEmail(string email) => EmailExiste;

        public void Agregar(Usuario usuario) { }
        public void Actualizar(Usuario usuario) { }
    }

    private sealed class SolicitudesFalsas : ISolicitudRegistroPort
    {
        public bool UsernameExiste { get; set; }
        public bool EmailExiste { get; set; }

        public SolicitudRegistro? Guardada { get; private set; }

        public bool ExisteUsername(string username) => UsernameExiste;
        public bool ExisteEmail(string email) => EmailExiste;

        public void Agregar(SolicitudRegistro solicitud)
        {
            Guardada = solicitud;
        }

        public SolicitudRegistro? ObtenerPorId(long id) => null;

        public SolicitudRegistro? ObtenerPorTokenHash(string hash)
            => null;

                                public SolicitudRegistro? ObtenerPendientePorEmail(string email)
        {
            return null;
        }
        public bool IntentarRenovarTokenConLimite(
            long solicitudId,
            string nuevoTokenHash,
            DateTime nuevaExpiracion)
        {
            return false;
        }
        public bool ConfirmarEmail(string tokenHash) => false;

        public void Actualizar(SolicitudRegistro solicitud) { }
    }
}