using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Services;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public class ReenvioVerificacionServiceTests
{
    private static ReenvioVerificacionService CrearServicio(
        SolicitudesFalsas solicitudes,
        EmailFalso email)
    {
        IConfiguration configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["App:PublicBaseUrl"] = "https://localhost:7043"
                })
            .Build();

        return new ReenvioVerificacionService(
            solicitudes,
            email,
            configuracion);
    }

    [Fact]
    public async Task Reenvio_NoEnviaSiNoExisteSolicitud()
    {
        var solicitudes = new SolicitudesFalsas();
        var email = new EmailFalso();

        var servicio = CrearServicio(solicitudes, email);

        await servicio.SolicitarReenvioAsync(
            "inexistente@empresa.com");

        Assert.Equal(0, email.Envios);
        Assert.Equal(0, solicitudes.IntentosRenovacion);
    }

    [Fact]
    public async Task Reenvio_GeneraTokenSeguroYEnviaEnlace()
    {
        var solicitudes = new SolicitudesFalsas
        {
            Solicitud = new SolicitudRegistro
            {
                Id = 15,
                Email = "empleado@empresa.com",
                Estado = "PendienteVerificacion"
            },
            PermitirRenovacion = true
        };

        var email = new EmailFalso();
        var servicio = CrearServicio(solicitudes, email);

        await servicio.SolicitarReenvioAsync(
            "EMPLEADO@EMPRESA.COM");

        Assert.Equal(1, solicitudes.IntentosRenovacion);
        Assert.Equal(15, solicitudes.IdRecibido);
        Assert.Equal(1, email.Envios);
        Assert.Equal("empleado@empresa.com", email.Destinatario);

        Assert.NotNull(email.Enlace);
        Assert.NotNull(solicitudes.HashRecibido);

        var uri = new Uri(email.Enlace!);

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("/Auth/VerificarEmail", uri.AbsolutePath);

        const string prefijo = "?token=";

        Assert.StartsWith(prefijo, uri.Query);

        string token = Uri.UnescapeDataString(
            uri.Query[prefijo.Length..]);

        Assert.Equal(64, token.Length);

        byte[] bytes = Convert.FromHexString(token);

        string hashEsperado = Convert.ToHexString(
            SHA256.HashData(bytes));

        Assert.Equal(hashEsperado, solicitudes.HashRecibido);
        Assert.NotEqual(token, solicitudes.HashRecibido);
    }

    [Fact]
    public async Task Reenvio_NoEnviaSiMySqlBloquea()
    {
        var solicitudes = new SolicitudesFalsas
        {
            Solicitud = new SolicitudRegistro
            {
                Id = 20,
                Email = "empleado@empresa.com"
            },
            PermitirRenovacion = false
        };

        var email = new EmailFalso();
        var servicio = CrearServicio(solicitudes, email);

        await servicio.SolicitarReenvioAsync(
            "empleado@empresa.com");

        Assert.Equal(1, solicitudes.IntentosRenovacion);
        Assert.Equal(0, email.Envios);
    }

    [Fact]
    public async Task Reenvio_PropagaErrorDeGmail()
    {
        var solicitudes = new SolicitudesFalsas
        {
            Solicitud = new SolicitudRegistro
            {
                Id = 30,
                Email = "empleado@empresa.com"
            },
            PermitirRenovacion = true
        };

        var email = new EmailFalso
        {
            SimularFallo = true
        };

        var servicio = CrearServicio(solicitudes, email);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SolicitarReenvioAsync(
                "empleado@empresa.com"));

        Assert.Equal(1, solicitudes.IntentosRenovacion);
        Assert.Equal(1, email.Envios);
    }

    private sealed class EmailFalso : IEmailService
    {
        public int Envios { get; private set; }
        public string? Destinatario { get; private set; }
        public string? Enlace { get; private set; }
        public bool SimularFallo { get; set; }

        public Task EnviarVerificacionAsync(
            string destinatario,
            string enlaceVerificacion,
            CancellationToken cancellationToken = default)
        {
            Envios++;
            Destinatario = destinatario;
            Enlace = enlaceVerificacion;

            if (SimularFallo)
            {
                throw new InvalidOperationException(
                    "Fallo SMTP simulado.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SolicitudesFalsas : ISolicitudRegistroPort
    {
        public SolicitudRegistro? Solicitud { get; set; }
        public bool PermitirRenovacion { get; set; }

        public int IntentosRenovacion { get; private set; }
        public long IdRecibido { get; private set; }
        public string? HashRecibido { get; private set; }

        public SolicitudRegistro? ObtenerPendientePorEmail(
            string email)
        {
            return Solicitud;
        }

        public bool IntentarRenovarTokenConLimite(
            long solicitudId,
            string nuevoTokenHash,
            DateTime nuevaExpiracion)
        {
            IntentosRenovacion++;
            IdRecibido = solicitudId;
            HashRecibido = nuevoTokenHash;

            return PermitirRenovacion;
        }

        public bool ExisteUsername(string username) => false;
        public bool ExisteEmail(string email) => false;

        public void Agregar(SolicitudRegistro solicitud) { }

        public SolicitudRegistro? ObtenerPorId(long id) => null;

        public SolicitudRegistro? ObtenerPorTokenHash(
            string tokenHash) => null;

        public bool ConfirmarEmail(string tokenHash) => false;

        public void Actualizar(SolicitudRegistro solicitud) { }
    }
}