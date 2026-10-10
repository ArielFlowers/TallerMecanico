using System.Security.Cryptography;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Services;

public sealed class VerificacionEmailService
{
    private readonly ISolicitudRegistroPort _solicitudes;

    public VerificacionEmailService(
        ISolicitudRegistroPort solicitudes)
    {
        _solicitudes = solicitudes;
    }

    public bool ConfirmarEmail(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) ||
            token.Length != 64)
        {
            return false;
        }

        byte[] tokenBytes;

        try
        {
            tokenBytes = Convert.FromHexString(token);
        }
        catch (FormatException)
        {
            return false;
        }

        string tokenHash = Convert.ToHexString(
            SHA256.HashData(tokenBytes));

        return _solicitudes.ConfirmarEmail(tokenHash);
    }
}