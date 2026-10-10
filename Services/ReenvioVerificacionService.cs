using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Services;

public sealed class ReenvioVerificacionService
{
    private readonly ISolicitudRegistroPort _solicitudes;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;

    public ReenvioVerificacionService(
        ISolicitudRegistroPort solicitudes,
        IEmailService email,
        IConfiguration configuration)
    {
        _solicitudes = solicitudes;
        _email = email;
        _configuration = configuration;
    }

    public async Task SolicitarReenvioAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
        {
            return;
        }

        string correoNormalizado = email.Trim().ToLowerInvariant();

        var solicitud =
            _solicitudes.ObtenerPendientePorEmail(correoNormalizado);

        if (solicitud is null)
        {
            return;
        }

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
                "App:PublicBaseUrl no es valida.");
        }

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);

        string token = Convert.ToHexString(tokenBytes);

        string tokenHash = Convert.ToHexString(
            SHA256.HashData(tokenBytes));

        DateTime expiracion = DateTime.UtcNow.AddHours(1);

        bool actualizado =
            _solicitudes.IntentarRenovarTokenConLimite(
                solicitud.Id,
                tokenHash,
                expiracion);

        if (!actualizado)
        {
            return;
        }

        string rutaVerificacion =
            "/Auth/VerificarEmail?token=" +
            Uri.EscapeDataString(token);

        Uri enlace = new Uri(uriBase, rutaVerificacion);

        await _email.EnviarVerificacionAsync(
            correoNormalizado,
            enlace.AbsoluteUri,
            cancellationToken);
    }
}