using System.Text.Json;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class OpcionesCacheDashboard
{
    public TimeSpan Expiracion { get; init; } = TimeSpan.FromSeconds(60);
}

// H2: patrón cache-aside sobre el resumen del dashboard.
// MISS: consulta MySQL mediante DashboardService y guarda en Redis.
// HIT: devuelve el valor guardado en Redis sin tocar MySQL.
public class DashboardCacheService
{
    private readonly DashboardService _dashboardService;
    private readonly ICacheDistribuidaPort _cache;
    private readonly OpcionesCacheDashboard _opciones;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DashboardCacheService> _logger;

    public DashboardCacheService(
        DashboardService dashboardService,
        ICacheDistribuidaPort cache,
        OpcionesCacheDashboard opciones,
        TimeProvider timeProvider,
        ILogger<DashboardCacheService> logger)
    {
        _dashboardService = dashboardService;
        _cache = cache;
        _opciones = opciones;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public ResultadoDashboard Obtener()
    {
        string replicaActual = Environment.MachineName;

        EntradaCacheDashboard? entrada =
            Deserializar(_cache.Obtener(ClavesCache.DashboardResumen));

        if (entrada is not null)
        {
            _logger.LogInformation(
                "HIT -> Redis | clave {Clave} | réplica {Replica} | generado por {ReplicaOrigen}",
                ClavesCache.DashboardResumen,
                replicaActual,
                entrada.Replica);

            return new ResultadoDashboard(
                entrada.Dashboard,
                EstadoCache.Hit,
                entrada.Replica,
                replicaActual,
                entrada.GeneradoEn,
                _cache.TiempoRestante(ClavesCache.DashboardResumen));
        }

        DashboardViewModel dashboard = _dashboardService.GetDashboardData();

        var nuevaEntrada = new EntradaCacheDashboard(
            dashboard,
            replicaActual,
            _timeProvider.GetLocalNow());

        bool guardado = _cache.Guardar(
            ClavesCache.DashboardResumen,
            JsonSerializer.Serialize(nuevaEntrada),
            _opciones.Expiracion);

        if (guardado)
        {
            _logger.LogInformation(
                "MISS -> MySQL -> Redis | clave {Clave} | réplica {Replica} | expira en {Segundos} s",
                ClavesCache.DashboardResumen,
                replicaActual,
                _opciones.Expiracion.TotalSeconds);
        }
        else
        {
            _logger.LogWarning(
                "MISS -> MySQL | Redis no disponible, el resultado no se almacenó | réplica {Replica}",
                replicaActual);
        }

        return new ResultadoDashboard(
            dashboard,
            guardado ? EstadoCache.Miss : EstadoCache.NoDisponible,
            replicaActual,
            replicaActual,
            nuevaEntrada.GeneradoEn,
            guardado ? _opciones.Expiracion : null);
    }

    public bool Invalidar()
    {
        bool eliminado = _cache.Eliminar(ClavesCache.DashboardResumen);

        _logger.LogInformation(
            "INVALIDACION -> Redis DEL {Clave} | eliminado: {Eliminado} | réplica {Replica}",
            ClavesCache.DashboardResumen,
            eliminado,
            Environment.MachineName);

        return eliminado;
    }

    private EntradaCacheDashboard? Deserializar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EntradaCacheDashboard>(valor);
        }
        catch (JsonException error)
        {
            _logger.LogWarning(
                error,
                "Valor inválido en {Clave}; se tratará como MISS.",
                ClavesCache.DashboardResumen);
            return null;
        }
    }

    private sealed record EntradaCacheDashboard(
        DashboardViewModel Dashboard,
        string Replica,
        DateTimeOffset GeneradoEn);
}
