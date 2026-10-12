namespace TallerMecanico.Application.Exceptions;

public sealed class SolicitudRegistroDuplicadaException : InvalidOperationException
{
    public string Campo { get; }

    public SolicitudRegistroDuplicadaException(string campo)
        : base("Los datos de registro no están disponibles.")
    {
        Campo = campo;
    }
}
