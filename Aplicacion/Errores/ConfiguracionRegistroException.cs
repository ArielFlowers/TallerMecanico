namespace TallerMecanico.Aplicacion.Errores;

public sealed class ConfiguracionRegistroException : InvalidOperationException
{
    public string Clave { get; }

    public ConfiguracionRegistroException(string clave)
        : base($"Falta una configuración válida para {clave}.")
    {
        Clave = clave;
    }
}
