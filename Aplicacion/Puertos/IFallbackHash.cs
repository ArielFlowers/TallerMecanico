namespace TallerMecanico.Aplicacion.Puertos;

/// <summary>
/// Hash ficticio de tiempo constante para no revelar si un usuario existe.
/// </summary>
public interface IFallbackHash
{
    string Hash { get; }
}
