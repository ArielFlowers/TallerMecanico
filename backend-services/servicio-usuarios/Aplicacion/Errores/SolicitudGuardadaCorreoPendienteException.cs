namespace TallerMecanico.Aplicacion.Errores;

public sealed class SolicitudGuardadaCorreoPendienteException
    : EnvioVerificacionException
{
    public SolicitudGuardadaCorreoPendienteException(Exception causa)
        : base(
            causa is EnvioVerificacionException envio
                ? envio.Etapa : "EnvioCorreo",
            causa is EnvioVerificacionException error
                ? error.TipoError : causa.GetType().FullName!,
            (causa as EnvioVerificacionException)?.CodigoSmtp)
    {
    }
}
