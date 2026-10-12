namespace TallerMecanico.Application.Exceptions;

public sealed class ConfiguracionRegistroException : InvalidOperationException
{
    public string Clave { get; }

    public ConfiguracionRegistroException(string clave)
        : base($"Falta una configuración válida para {clave}.")
    {
        Clave = clave;
    }
}
