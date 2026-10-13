using Microsoft.Extensions.Configuration;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.Aplicacion.Fachadas;

/// <summary>
/// Simplifica para la interfaz web la creación de una solicitud de registro
/// y el envío de su enlace de verificación.
/// </summary>
public sealed class RegistroFacade
{
    private readonly RegistroUsuarioService _registroUsuario;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public RegistroFacade(
        RegistroUsuarioService registroUsuario,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _registroUsuario = registroUsuario;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task SolicitarRegistroAsync(
        string username,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        Uri uriBase = EnlaceVerificacion.ObtenerBase(_configuration);
        _emailService.ValidarConfiguracion();
        cancellationToken.ThrowIfCancellationRequested();

        string token = _registroUsuario.CrearSolicitudPendiente(
            username,
            email,
            password);

        try
        {
            await _emailService.EnviarVerificacionAsync(
                email.Trim().ToLowerInvariant(),
                EnlaceVerificacion.Crear(uriBase, token),
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            // La solicitud ya existe y se conserva para permitir el reenvío.
            throw new SolicitudGuardadaCorreoPendienteException(ex);
        }
    }
}
