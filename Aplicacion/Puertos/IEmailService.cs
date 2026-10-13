namespace TallerMecanico.Application.Ports;

public interface IEmailService
{
    // Comprueba opciones locales; no conecta ni envía mensajes.
    void ValidarConfiguracion();

    // Completar esta tarea significa aceptación por SMTP, no entrega en el buzón.
    Task EnviarVerificacionAsync(
        string destinatario,
        string enlaceVerificacion,
        CancellationToken cancellationToken = default);
}
