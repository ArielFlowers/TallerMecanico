using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.Infraestructura.Correo;

public sealed class GmailEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailEmailService> _logger;
    private readonly Func<SmtpClient> _crearCliente;

    public GmailEmailService(
        IConfiguration configuration,
        ILogger<GmailEmailService>? logger = null,
        Func<SmtpClient>? crearCliente = null)
    {
        _configuration = configuration;
        _logger = logger ?? NullLogger<GmailEmailService>.Instance;
        _crearCliente = crearCliente ?? (() => new SmtpClient());
    }

    public void ValidarConfiguracion()
    {
        ObtenerConfiguracionValidada();
    }

    public async Task EnviarVerificacionAsync(
        string destinatario,
        string enlaceVerificacion,
        CancellationToken cancellationToken = default)
    {
        var configuracion = ObtenerConfiguracionValidada();

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

        mensaje.From.Add(configuracion.Remitente);
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

        using SmtpClient cliente = _crearCliente();

        cliente.Timeout = 15000;

        string etapa = "Conexion";
        bool aceptadoPorSmtp = false;

        try
        {
            await cliente.ConnectAsync(
                configuracion.Host,
                configuracion.Puerto,
                configuracion.Seguridad,
                cancellationToken);

            if (configuracion.Autenticar)
            {
                etapa = "Autenticacion";
                await cliente.AuthenticateAsync(
                    configuracion.Usuario,
                    configuracion.Password,
                    cancellationToken);
            }

            etapa = "Envio";
            await cliente.SendAsync(mensaje, cancellationToken);
            aceptadoPorSmtp = true;
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            throw new EnvioVerificacionException(
                etapa, ex.GetType().FullName!, ObtenerCodigoSmtp(ex));
        }
        finally
        {
            if (cliente.IsConnected)
            {
                try
                {
                    await cliente.DisconnectAsync(true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // El cierre no cambia la aceptación ni oculta el error original.
                    _logger.LogWarning(
                        "Fallo al cerrar SMTP. Aceptado: {Aceptado}; Tipo: {Tipo}; Código: {Codigo}",
                        aceptadoPorSmtp, ex.GetType().FullName, ObtenerCodigoSmtp(ex));
                }
            }
        }
    }

    private (
        string Host,
        int Puerto,
        string Usuario,
        string Password,
        bool Autenticar,
        SecureSocketOptions Seguridad,
        MailboxAddress Remitente) ObtenerConfiguracionValidada()
    {
        string host = ObtenerConfiguracion("Email:Smtp:Host").Trim();

        if (!int.TryParse(_configuration["Email:Smtp:Port"], out int puerto) ||
            puerto is < 1 or > 65535)
        {
            throw new ConfiguracionRegistroException("Email:Smtp:Port");
        }

        string usuario = (_configuration["Email:Smtp:Username"] ?? string.Empty).Trim();
        string password = _configuration["Email:Smtp:Password"] ?? string.Empty;
        bool tieneUsuario = !string.IsNullOrWhiteSpace(usuario);
        bool tienePassword = !string.IsNullOrWhiteSpace(password);

        if (tieneUsuario != tienePassword)
        {
            throw new ConfiguracionRegistroException(
                tieneUsuario
                    ? "Email:Smtp:Password"
                    : "Email:Smtp:Username");
        }

        string from = ObtenerConfiguracion("Email:Smtp:From");
        if (!MailboxAddress.TryParse(from, out MailboxAddress? remitente) ||
            !ValidadorSolicitudRegistro.EmailValido(remitente.Address))
        {
            throw new ConfiguracionRegistroException("Email:Smtp:From");
        }

        return (
            host,
            puerto,
            usuario,
            password,
            tieneUsuario,
            ObtenerSeguridad(),
            remitente);
    }

    private SecureSocketOptions ObtenerSeguridad()
    {
        string? valor = _configuration["Email:Smtp:Security"];

        if (string.IsNullOrWhiteSpace(valor))
        {
            return SecureSocketOptions.SslOnConnect;
        }

        if (!Enum.TryParse(valor.Trim(), ignoreCase: true, out SecureSocketOptions seguridad) ||
            !Enum.IsDefined(seguridad))
        {
            throw new ConfiguracionRegistroException("Email:Smtp:Security");
        }

        return seguridad;
    }

    private static int? ObtenerCodigoSmtp(Exception error)
    {
        for (Exception? causa = error; causa is not null; causa = causa.InnerException)
        {
            if (causa is SmtpCommandException smtp)
            {
                return (int)smtp.StatusCode;
            }
        }

        return null;
    }

    private string ObtenerConfiguracion(string clave)
    {
        string? valor = _configuration[clave];

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ConfiguracionRegistroException(clave);
        }

        return valor;
    }
}
