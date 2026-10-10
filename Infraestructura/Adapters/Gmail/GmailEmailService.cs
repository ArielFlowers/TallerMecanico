using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Infraestructura.Adapters.Gmail;

public sealed class GmailEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public GmailEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task EnviarVerificacionAsync(
        string destinatario,
        string enlaceVerificacion,
        CancellationToken cancellationToken = default)
    {
        string host = ObtenerConfiguracion("Email:Smtp:Host");
        string username = ObtenerConfiguracion("Email:Smtp:Username");
        string password = ObtenerConfiguracion("Email:Smtp:Password");
        string remitente = ObtenerConfiguracion("Email:Smtp:From");

        int puerto = _configuration.GetValue<int>("Email:Smtp:Port");

        if (host != "smtp.gmail.com" || puerto != 465)
        {
            throw new InvalidOperationException(
                "La configuracion SMTP no coincide con Gmail puerto 465.");
        }

        if (!Uri.TryCreate(
                enlaceVerificacion,
                UriKind.Absolute,
                out Uri? enlace) ||
            (enlace.Scheme != Uri.UriSchemeHttps &&
             !(enlace.Scheme == Uri.UriSchemeHttp && enlace.IsLoopback)))
        {
            throw new ArgumentException(
                "El enlace de verificacion no es valido.");
        }

        var mensaje = new MimeMessage();

        mensaje.From.Add(MailboxAddress.Parse(remitente));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = "Verifica tu correo - AutoTaller Pro";

        mensaje.Body = new TextPart("plain")
        {
            Text =
                "Has solicitado una cuenta en AutoTaller Pro." +
                Environment.NewLine +
                Environment.NewLine +
                "Verifica tu correo utilizando este enlace:" +
                Environment.NewLine +
                enlaceVerificacion +
                Environment.NewLine +
                Environment.NewLine +
                "El enlace vence en una hora." +
                Environment.NewLine +
                "Despues necesitaras aprobacion del administrador." +
                Environment.NewLine +
                Environment.NewLine +
                "Si no solicitaste esta cuenta, ignora este mensaje."
        };

        using var cliente = new SmtpClient();

        cliente.Timeout = 15000;

        await cliente.ConnectAsync(
            host,
            puerto,
            SecureSocketOptions.SslOnConnect,
            cancellationToken);

        await cliente.AuthenticateAsync(
            username,
            password,
            cancellationToken);

        await cliente.SendAsync(mensaje, cancellationToken);

        await cliente.DisconnectAsync(true, cancellationToken);
    }

    private string ObtenerConfiguracion(string clave)
    {
        string? valor = _configuration[clave];

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException(
                $"Falta la configuracion {clave}.");
        }

        return valor;
    }
}