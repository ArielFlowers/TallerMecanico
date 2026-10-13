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

        // Validar antes de consultar o consumir el intervalo de reenvío.
        Uri uriBase = EnlaceVerificacion.ObtenerBase(_configuration);
        _email.ValidarConfiguracion();
        cancellationToken.ThrowIfCancellationRequested();

        var solicitud =
            _solicitudes.ObtenerPendientePorEmail(correoNormalizado);

        if (solicitud is null)
        {
            return;
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

        await _email.EnviarVerificacionAsync(
            correoNormalizado,
            EnlaceVerificacion.Crear(uriBase, token),
            cancellationToken);
    }
}
