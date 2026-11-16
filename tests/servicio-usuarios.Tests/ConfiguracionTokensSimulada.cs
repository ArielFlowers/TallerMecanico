using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.IntegrationTests;

// Doble de la tabla ConfiguracionTokens (RF-07): por defecto usa los mismos valores
// que el inicializador del esquema.
internal sealed class ConfiguracionTokensSimulada : IConfiguracionTokensPort
{
    public int MinutosVerificacion { get; set; } = 60;
    public int MinutosInvitacion { get; set; } = 60;
    public int MinutosSesion { get; set; } = 30;

    public TimeSpan ObtenerDuracionVerificacion() => TimeSpan.FromMinutes(MinutosVerificacion);

    public TimeSpan ObtenerDuracionInvitacion() => TimeSpan.FromMinutes(MinutosInvitacion);

    public TimeSpan ObtenerDuracionSesion() => TimeSpan.FromMinutes(MinutosSesion);
}
