using Microsoft.Extensions.Configuration;
using TallerMecanico.Application.Exceptions;
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
        Uri uriBase = EnlaceVerificacion.ObtenerBase(_configuration);
        _email.ValidarConfiguracion();
        cancellationToken.ThrowIfCancellationRequested();

        // Primero se guarda la solicitud y se obtiene el token.
        string token = _registro.CrearSolicitudPendiente(
            username,
            email,
            password);

        try
        {
            await _email.EnviarVerificacionAsync(
                email.Trim().ToLowerInvariant(),
                EnlaceVerificacion.Crear(uriBase, token),
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            // La solicitud ya existe. Se conserva para el reenvío.
            throw new SolicitudGuardadaCorreoPendienteException(ex);
        }
    }
}
