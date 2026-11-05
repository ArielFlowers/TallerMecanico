namespace TallerMecanico.Aplicacion.Dto;

public enum EstadoLecturaCache
{
    Hit,
    Miss,
    Error,
    Deshabilitado
}

public sealed record ResultadoLecturaCache(
    EstadoLecturaCache Estado,
    string? Valor = null);
