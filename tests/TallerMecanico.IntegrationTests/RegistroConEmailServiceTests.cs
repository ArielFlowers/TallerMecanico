using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Application.Exceptions;
using TallerMecanico.Pages.Auth;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class RegistroConEmailServiceTests
{
    private const string PasswordPrueba = "ClaveSoloParaPruebas123";

    [Theory]
    [InlineData(null)]
    [InlineData("no-es-url")]
    [InlineData("http://example.com")]
    [InlineData("https://localhost:7043?dato=1")]
    [InlineData("https://localhost:7043#fragmento")]
    [InlineData("https://usuario:valor@localhost:7043")]
    public async Task UrlInvalida_NoConsultaNiGuarda(string? url)
    {
        var fixture = new RegistroCorreoFixture();
        var error = await Assert.ThrowsAsync<ConfiguracionRegistroException>(
            () => fixture.CrearRegistro(url).SolicitarRegistroAsync(
                "empleado", "empleado@example.com", PasswordPrueba));

        Assert.Equal("App:PublicBaseUrl", error.Clave);
        Assert.Null(fixture.Solicitudes.Guardada);
        Assert.Equal(0, fixture.Usuarios.Consultas);
        Assert.Equal(0, fixture.Solicitudes.Consultas);
        Assert.Equal(0, fixture.Correo.Envios);
    }

    [Fact]
    public async Task SmtpSinConfigurar_NoConsultaNiGuarda()
    {
        var fixture = new RegistroCorreoFixture();
        fixture.Correo.ErrorConfiguracion = new ConfiguracionRegistroException("Email:Smtp:Password");

        await Assert.ThrowsAsync<ConfiguracionRegistroException>(() =>
            fixture.CrearRegistro().SolicitarRegistroAsync(
                "empleado", "empleado@example.com", PasswordPrueba));

        Assert.Null(fixture.Solicitudes.Guardada);
        Assert.Equal(0, fixture.Usuarios.Consultas);
        Assert.Equal(0, fixture.Solicitudes.Consultas);
        Assert.Equal(0, fixture.Correo.Envios);
    }

    [Fact]
    public async Task CorreoAceptado_GuardaAntesDeEnviar()
    {
        var fixture = new RegistroCorreoFixture();
        fixture.Correo.AntesDeEnviar = () => Assert.NotNull(fixture.Solicitudes.Guardada);

        await fixture.CrearRegistro().SolicitarRegistroAsync(
            "empleado", "empleado@example.com", PasswordPrueba);

        Assert.Equal(1, fixture.Correo.Envios);
        Assert.Equal("PendienteVerificacion", fixture.Solicitudes.Guardada!.Estado);
    }

    [Fact]
    public async Task FalloSmtp_ConservaSolicitudYDiagnosticoSeguro()
    {
        var fixture = new RegistroCorreoFixture();
        fixture.Correo.ErrorEnvio = new EnvioVerificacionException("Autenticacion", "ErrorSimulado", 535);

        var error = await Assert.ThrowsAsync<SolicitudGuardadaCorreoPendienteException>(() =>
            fixture.CrearRegistro().SolicitarRegistroAsync(
                "empleado", "empleado@example.com", PasswordPrueba));

        Assert.NotNull(fixture.Solicitudes.Guardada);
        Assert.Equal("Autenticacion", error.Etapa);
        Assert.Equal(535, error.CodigoSmtp);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task CancelacionPrevia_NoGuarda()
    {
        var fixture = new RegistroCorreoFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.CrearRegistro().SolicitarRegistroAsync(
                "empleado", "empleado@example.com", PasswordPrueba, cancellation.Token));

        Assert.Null(fixture.Solicitudes.Guardada);
        Assert.Equal(0, fixture.Usuarios.Consultas);
    }

    [Theory]
    [InlineData("Aceptado")]
    [InlineData("Duplicado")]
    [InlineData("FalloEnvio")]
    public async Task Pagina_MismoResultadoPublicoSinSecretos(string caso)
    {
        var fixture = new RegistroCorreoFixture();
        fixture.Usuarios.UsernameExiste = caso == "Duplicado";
        const string contenidoSensible = "NO_REGISTRAR_PASSWORD_TOKEN_COOKIE";
        if (caso == "FalloEnvio") fixture.Correo.ErrorEnvio = new IOException(contenidoSensible);

        var logger = new LoggerEnMemoria<RegistroModel>();
        var page = new RegistroModel(fixture.CrearRegistro(), logger)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Input = new RegistroInput
            {
                Username = "empleado", Email = "empleado@example.com",
                Password = PasswordPrueba, ConfirmarPassword = PasswordPrueba
            }
        };

        Assert.IsType<PageResult>(await page.OnPostAsync(CancellationToken.None));
        Assert.True(page.SolicitudRecibida);
        Assert.True(page.ModelState.IsValid);
        Assert.Empty(page.Input.Password);
        Assert.Empty(page.Input.Username);
        Assert.All(logger.Entradas, entrada =>
        {
            Assert.Null(entrada.Error);
            Assert.DoesNotContain(contenidoSensible, entrada.Mensaje);
            Assert.DoesNotContain(PasswordPrueba, entrada.Mensaje);
            Assert.DoesNotContain("empleado@example.com", entrada.Mensaje);
            if (fixture.Solicitudes.Guardada is not null)
                Assert.DoesNotContain(fixture.Solicitudes.Guardada.TokenVerificacionHash!, entrada.Mensaje);
        });
        if (caso == "FalloEnvio") Assert.NotNull(fixture.Solicitudes.Guardada);
        if (caso == "Duplicado") Assert.Null(fixture.Solicitudes.Guardada);
    }

    [Fact]
    public async Task Pagina_ConfiguracionInvalidaSeDiagnosticaSinRevelarla()
    {
        var fixture = new RegistroCorreoFixture();
        fixture.Correo.ErrorConfiguracion = new ConfiguracionRegistroException("Email:Smtp:Password");
        var logger = new LoggerEnMemoria<RegistroModel>();
        var page = new RegistroModel(fixture.CrearRegistro(), logger)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Input = new RegistroInput
            {
                Username = "empleado", Email = "empleado@example.com",
                Password = PasswordPrueba, ConfirmarPassword = PasswordPrueba
            }
        };

        await page.OnPostAsync(CancellationToken.None);

        Assert.False(page.SolicitudRecibida);
        Assert.Null(fixture.Solicitudes.Guardada);
        Assert.Contains(logger.Entradas, entrada => entrada.Mensaje.Contains("Email:Smtp:Password"));
        Assert.All(page.ModelState.Values.SelectMany(valor => valor.Errors),
            error => Assert.DoesNotContain("Email:Smtp", error.ErrorMessage));
        Assert.All(logger.Entradas, entrada => Assert.DoesNotContain(PasswordPrueba, entrada.Mensaje));
    }

    [Fact]
    public async Task Reenvio_ConfiguracionInvalidaNoConsumeIntervalo()
    {
        var fixture = new RegistroCorreoFixture();
        fixture.Correo.ErrorConfiguracion = new ConfiguracionRegistroException("Email:Smtp:From");

        await Assert.ThrowsAsync<ConfiguracionRegistroException>(
            () => fixture.CrearReenvio().SolicitarReenvioAsync("empleado@example.com"));

        Assert.Equal(0, fixture.Solicitudes.Consultas);
        Assert.Equal(0, fixture.Solicitudes.Renovaciones);
        Assert.Equal(0, fixture.Correo.Envios);
    }
}
