namespace TallerMecanico.Aplicacion.Puertos;

// RF-07: la duración de cada token se lee de la tabla ConfiguracionTokens.
public interface IConfiguracionTokensPort
{
    TimeSpan ObtenerDuracionVerificacion();

    TimeSpan ObtenerDuracionInvitacion();

    TimeSpan ObtenerDuracionSesion();
}
