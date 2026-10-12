using System.Net;
using System.Text;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using TallerMecanico.Application.Exceptions;
using TallerMecanico.Application.Ports;
using TallerMecanico.Infraestructura.Adapters.Gmail;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class GmailEmailServiceTests
{
    private const string Enlace = "https://localhost:7043/Auth/VerificarEmail?token=TOKEN_SINTETICO";
    private const string Sensible = "NO_REGISTRAR_PASSWORD_TOKEN_COOKIE";

    private static IConfiguration Configuracion(string? clave = null, string? valor = null)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "smtp.gmail.com",
            ["Email:Smtp:Port"] = "465",
            ["Email:Smtp:Username"] = "remitente@example.com",
            ["Email:Smtp:Password"] = Sensible,
            ["Email:Smtp:From"] = "AutoTaller <remitente@example.com>"
        };
        if (clave is not null) valores[clave] = valor;
        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [Theory]
    [InlineData("Email:Smtp:Host", null)]
    [InlineData("Email:Smtp:Host", "otro-servidor")]
    [InlineData("Email:Smtp:Port", null)]
    [InlineData("Email:Smtp:Port", "587")]
    [InlineData("Email:Smtp:Port", "no-es-numero")]
    [InlineData("Email:Smtp:From", null)]
    [InlineData("Email:Smtp:From", "correo-invalido")]
    [InlineData("Email:Smtp:Username", " ")]
    [InlineData("Email:Smtp:Password", " ")]
    public async Task ConfiguracionInvalida_NoCreaCliente(string clave, string? valor)
    {
        int clientes = 0;
        var servicio = new GmailEmailService(Configuracion(clave, valor), crearCliente: () =>
        {
            clientes++;
            throw new InvalidOperationException("No debe crearse un cliente.");
        });

        var error = Assert.Throws<ConfiguracionRegistroException>(servicio.ValidarConfiguracion);
        Assert.Equal(clave, error.Clave);
        await Assert.ThrowsAsync<ConfiguracionRegistroException>(() =>
            servicio.EnviarVerificacionAsync("empleado@example.com", Enlace));
        Assert.Equal(0, clientes);
        Assert.DoesNotContain(Sensible, error.ToString());
    }

    [Fact]
    public void Adaptador_SeResuelvePorInyeccionSinFactoryRegistrada()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(Configuracion())
            .AddScoped<IEmailService, GmailEmailService>()
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEmailService>().ValidarConfiguracion();
    }

    [Fact]
    public void ValidarConfiguracion_NoConecta()
    {
        var servicio = new GmailEmailService(Configuracion(), crearCliente: () =>
            throw new InvalidOperationException("No debe crearse un cliente."));
        servicio.ValidarConfiguracion();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AceptadoPorSmtp_NoFallaPorDesconexion(bool cierreCancelado)
    {
        var cliente = new ClienteSmtpSimulado
        {
            ErrorCierre = cierreCancelado
                ? new OperationCanceledException(Sensible) : new IOException(Sensible)
        };
        var logger = new LoggerEnMemoria<GmailEmailService>();
        var servicio = new GmailEmailService(Configuracion(), logger, () => cliente);

        await servicio.EnviarVerificacionAsync("empleado@example.com", Enlace);

        Assert.Equal(1, cliente.Envios);
        Assert.Equal(1, cliente.Cierres);
        Assert.Equal(SecureSocketOptions.SslOnConnect, cliente.Seguridad);
        Assert.Contains(logger.Entradas, entrada => entrada.Mensaje.Contains("Aceptado: True"));
        AssertLogsSeguros(logger);
    }

    [Theory]
    [InlineData("Conexion")]
    [InlineData("Autenticacion")]
    [InlineData("Envio")]
    public async Task FalloSmtp_IdentificaEtapaSinRespuestaSensible(string etapa)
    {
        var cliente = new ClienteSmtpSimulado { EtapaConFallo = etapa };
        var servicio = new GmailEmailService(Configuracion(), crearCliente: () => cliente);

        var error = await Assert.ThrowsAsync<EnvioVerificacionException>(() =>
            servicio.EnviarVerificacionAsync("empleado@example.com", Enlace));

        Assert.Equal(etapa, error.Etapa);
        Assert.Equal(550, error.CodigoSmtp);
        Assert.Contains(nameof(SmtpCommandException), error.TipoError);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(Sensible, error.ToString());
        Assert.DoesNotContain("TOKEN_SINTETICO", error.ToString());
    }

    [Fact]
    public async Task FalloEnvioYCierre_ConservaErrorDeEnvio()
    {
        var cliente = new ClienteSmtpSimulado
        {
            EtapaConFallo = "Envio", ErrorCierre = new IOException(Sensible)
        };
        var logger = new LoggerEnMemoria<GmailEmailService>();
        var servicio = new GmailEmailService(Configuracion(), logger, () => cliente);

        var error = await Assert.ThrowsAsync<EnvioVerificacionException>(() =>
            servicio.EnviarVerificacionAsync("empleado@example.com", Enlace));

        Assert.Equal("Envio", error.Etapa);
        Assert.Equal(550, error.CodigoSmtp);
        Assert.Contains(logger.Entradas, entrada => entrada.Mensaje.Contains("Aceptado: False"));
        AssertLogsSeguros(logger);
    }

    private static void AssertLogsSeguros(LoggerEnMemoria<GmailEmailService> logger)
    {
        Assert.All(logger.Entradas, entrada =>
        {
            Assert.Null(entrada.Error);
            Assert.DoesNotContain(Sensible, entrada.Mensaje);
            Assert.DoesNotContain("TOKEN_SINTETICO", entrada.Mensaje);
            Assert.DoesNotContain("remitente@example.com", entrada.Mensaje);
        });
    }

    private sealed class ClienteSmtpSimulado : SmtpClient
    {
        private bool _conectado;
        public override bool IsConnected => _conectado;
        public string? EtapaConFallo { get; set; }
        public Exception? ErrorCierre { get; set; }
        public int Envios { get; private set; }
        public int Cierres { get; private set; }
        public SecureSocketOptions Seguridad { get; private set; }

        private void FallarSiCorresponde(string etapa)
        {
            if (EtapaConFallo == etapa)
                throw new SmtpCommandException(SmtpErrorCode.MessageNotAccepted,
                    SmtpStatusCode.MailboxUnavailable, Sensible);
        }

        public override Task ConnectAsync(string host, int port = 0,
            SecureSocketOptions options = SecureSocketOptions.Auto,
            CancellationToken cancellationToken = default)
        {
            FallarSiCorresponde("Conexion");
            Seguridad = options;
            _conectado = true;
            return Task.CompletedTask;
        }

        public override Task AuthenticateAsync(Encoding encoding, ICredentials credentials,
            CancellationToken cancellationToken = default)
        {
            FallarSiCorresponde("Autenticacion");
            return Task.CompletedTask;
        }

        public override Task<string> SendAsync(FormatOptions options, MimeMessage message,
            CancellationToken cancellationToken = default, ITransferProgress? progress = null)
        {
            Envios++;
            FallarSiCorresponde("Envio");
            return Task.FromResult("ACEPTADO_SIMULADO");
        }

        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
        {
            Cierres++;
            _conectado = false;
            if (ErrorCierre is not null) throw ErrorCierre;
            return Task.CompletedTask;
        }
    }
}
