namespace TallerMecanico.Application.Exceptions;

public class EnvioVerificacionException : Exception
{
    public string Etapa { get; }
    public string TipoError { get; }
    public int? CodigoSmtp { get; }

    // Solo conserva metadatos seguros: una respuesta SMTP o una excepción
    // interna pueden contener el enlace, credenciales u otros datos sensibles.
    public EnvioVerificacionException(
        string etapa,
        string tipoError,
        int? codigoSmtp = null)
        : base("No se pudo confirmar la aceptación del correo por SMTP.")
    {
        Etapa = etapa;
        TipoError = tipoError;
        CodigoSmtp = codigoSmtp;
    }
}
