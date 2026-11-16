using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class ReenvioVerificacionService
{
    private readonly ISolicitudRegistroPort _solicitudes;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly IConfiguracionTokensPort _configuracionTokens;

    public ReenvioVerificacionService(
        ISolicitudRegistroPort solicitudes,
        IEmailService email,
        IConfiguration configuration,
        IConfiguracionTokensPort configuracionTokens)
    {
        _solicitudes = solicitudes;
        _email = email;
        _configuration = configuration;
        _configuracionTokens = configuracionTokens;
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

        // La invitación y la verificación del registro tienen su propia duración.
        TimeSpan duracion = solicitud.EsInvitacionAdministrativa
            ? _configuracionTokens.ObtenerDuracionInvitacion()
            : _configuracionTokens.ObtenerDuracionVerificacion();

        DateTime expiracion = DateTime.UtcNow + duracion;

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
            solicitud.EsInvitacionAdministrativa
                    ? EnlaceVerificacion.CrearActivacionInvitacion(uriBase, token)
                    : EnlaceVerificacion.CrearVerificacion(uriBase, token),
            cancellationToken,
            solicitud.Username);
    }
}
