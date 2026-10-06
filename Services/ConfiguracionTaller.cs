namespace TallerMecanico.Services;

public sealed class ConfiguracionTaller
{
    private static readonly Lazy<ConfiguracionTaller> _instancia =
        new(() => new ConfiguracionTaller());

    public static ConfiguracionTaller Instancia => _instancia.Value;

    private ConfiguracionTaller()
    {
    }

    public string NombreTaller => "AutoTaller Pro";

    public string Version => "v1.0";

    public string Moneda => "Bs.";

    public string FormatoFecha => "dd/MM/yyyy";
}
