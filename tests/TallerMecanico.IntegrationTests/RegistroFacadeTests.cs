using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Pages.Auth;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class RegistroFacadeTests
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
            () => fixture.CrearFachada(url).SolicitarRegistroAsync(
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
            fixture.CrearFachada().SolicitarRegistroAsync(
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

        await fixture.CrearFachada().SolicitarRegistroAsync(
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
            fixture.CrearFachada().SolicitarRegistroAsync(
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
            fixture.CrearFachada().SolicitarRegistroAsync(
                "empleado", "empleado@example.com", PasswordPrueba, cancellation.Token));

        Assert.Null(fixture.Solicitudes.Guardada);
        Assert.Equal(0, fixture.Usuarios.Consultas);
    }

    [Fact]
    public void Pagina_RegistroPublicoGet_RedirigeAlLogin()
    {
        var fixture = new RegistroCorreoFixture();
        var logger = new LoggerEnMemoria<RegistroModel>();

        var page = new RegistroModel(fixture.CrearFachada(), logger)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var resultado = page.OnGet();

        var redireccion =
            Assert.IsType<RedirectToPageResult>(resultado);

        Assert.Equal("/Auth/Login", redireccion.PageName);
        Assert.Null(fixture.Solicitudes.Guardada);
    }

    [Fact]
    public void Pagina_RegistroPublicoPost_RechazaSinGuardarDatos()
    {
        var fixture = new RegistroCorreoFixture();
        var logger = new LoggerEnMemoria<RegistroModel>();

        var page = new RegistroModel(fixture.CrearFachada(), logger)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            },
            Input = new RegistroInput
            {
                Username = "empleado",
                Email = "empleado@example.com",
                Password = PasswordPrueba,
                ConfirmarPassword = PasswordPrueba
            }
        };

        var resultado = page.OnPostAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(resultado);
        Assert.False(page.SolicitudRecibida);
        Assert.Null(fixture.Solicitudes.Guardada);

        Assert.All(logger.Entradas, entrada =>
        {
            Assert.DoesNotContain(PasswordPrueba, entrada.Mensaje);
            Assert.DoesNotContain("empleado@example.com", entrada.Mensaje);
        });
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
