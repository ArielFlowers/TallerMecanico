using Microsoft.Extensions.Configuration;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Services;

public sealed class RegistroConEmailService
{
    private readonly RegistroUsuarioService _registro;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;

    public RegistroConEmailService(
        RegistroUsuarioService registro,
        IEmailService email,
        IConfiguration configuration)
    {
        _registro = registro;
        _email = email;
        _configuration = configuration;
    }

    public async Task SolicitarRegistroAsync(
        string username,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        string? baseUrl = _configuration["App:PublicBaseUrl"];

        if (!Uri.TryCreate(
                baseUrl,
                UriKind.Absolute,
                out Uri? uriBase) ||
            (uriBase.Scheme != Uri.UriSchemeHttps &&
             !(uriBase.Scheme == Uri.UriSchemeHttp &&
               uriBase.IsLoopback)) ||
            !string.IsNullOrEmpty(uriBase.UserInfo) ||
            !string.IsNullOrEmpty(uriBase.Query) ||
            !string.IsNullOrEmpty(uriBase.Fragment))
        {
            throw new InvalidOperationException(
                "App:PublicBaseUrl debe ser HTTPS " +
                "o HTTP en localhost para desarrollo.");
        }

        // Primero se guarda la solicitud y se obtiene el token.
        string token = _registro.CrearSolicitudPendiente(
            username,
            email,
            password);

        // Nunca se escribe el token en logs.
        string rutaVerificacion =
            "/Auth/VerificarEmail?token=" +
            Uri.EscapeDataString(token);

        Uri enlace = new Uri(uriBase, rutaVerificacion);

        await _email.EnviarVerificacionAsync(
            email.Trim().ToLowerInvariant(),
            enlace.AbsoluteUri,
            cancellationToken);
    }
}