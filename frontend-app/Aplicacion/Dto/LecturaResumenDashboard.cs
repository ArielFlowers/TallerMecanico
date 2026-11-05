namespace TallerMecanico.Aplicacion.Dto;

// Generación y contenido se capturan juntos, antes de consultar MySQL.
public sealed record LecturaResumenDashboard(ResultadoLecturaCache Lectura, string? Generacion = null);

public enum EstadoPublicacionResumen
{
    NoIntentada,
    Publicada,
    RechazadaPorGeneracion,
    EntradaExistente,
    NoDisponible,
    Error,
    Deshabilitado
}
