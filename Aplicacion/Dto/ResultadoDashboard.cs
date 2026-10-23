namespace TallerMecanico.Aplicacion.Dto;

public enum EstadoCache
{
    Hit,
    Miss,
    NoDisponible,
    Deshabilitado,
    EntradaInvalida
}

public sealed record ResultadoDashboard(
    DashboardViewModel Dashboard,
    EstadoCache Estado,
    string ReplicaOrigen,
    string ReplicaActual,
    DateTimeOffset GeneradoEn,
    TimeSpan? TiempoRestante,
    bool? Almacenado = null);
