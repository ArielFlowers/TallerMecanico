using Microsoft.Extensions.Configuration;
using TallerMecanico.Infraestructura.Adapters.Gmail;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class EnvioGmailRealTests
{
    [Fact]
    public async Task EnviarCorreoReal_SoloConAutorizacionExplicita()
    {
        // No enviar durante las pruebas habituales.
        if (Environment.GetEnvironmentVariable(
            "AUTOTALLER_PRUEBA_SMTP") != "SI")
        {
            return;
        }

        string? destinatario = Environment.GetEnvironmentVariable(
            "AUTOTALLER_SMTP_DESTINATARIO");

        if (string.IsNullOrWhiteSpace(destinatario))
        {
            throw new InvalidOperationException(
                "Falta configurar el destinatario de prueba.");
        }

        IConfiguration configuracion = new ConfigurationBuilder()
            .AddUserSecrets(
                typeof(GmailEmailService).Assembly,
                optional: false)
            .Build();

        var servicio = new GmailEmailService(configuracion);

        // Enlace ficticio. No corresponde a una solicitud real.
        const string enlacePrueba =
            "http://localhost:5000/Auth/VerificarEmail" +
            "?token=PRUEBA_SMTP_NO_VALIDO";

        await servicio.EnviarVerificacionAsync(
            destinatario,
            enlacePrueba);
    }
}