namespace TallerMecanico.Application.Ports;

public interface IEmailService
{
    Task EnviarVerificacionAsync(
        string destinatario,
        string enlaceVerificacion,
        CancellationToken cancellationToken = default);
}