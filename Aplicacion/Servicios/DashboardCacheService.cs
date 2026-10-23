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

        ResultadoLecturaCache lectura = _cache.Obtener(ClavesCache.DashboardResumen);
        EntradaCacheDashboard? entrada = lectura.Estado == EstadoLecturaCache.Hit
            ? Deserializar(lectura.Valor)
            : null;

        if (entrada is not null)
        {
            _logger.LogInformation(
                "HIT -> Redis | operación GET | clave {Clave} | réplica {Replica} | generado por {ReplicaOrigen} | lectura Hit | escritura NoIntentada",
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

        EstadoCache estado = lectura.Estado switch
        {
            EstadoLecturaCache.Miss => EstadoCache.Miss,
            EstadoLecturaCache.Deshabilitado => EstadoCache.Deshabilitado,
            EstadoLecturaCache.Hit => EstadoCache.EntradaInvalida,
            _ => EstadoCache.NoDisponible
        };

        return RecuperarDesdeMySql(estado, lectura.Estado, replicaActual);
    }

    private ResultadoDashboard RecuperarDesdeMySql(
        EstadoCache estado,
        EstadoLecturaCache lectura,
        string replicaActual)
    {
        DashboardViewModel dashboard = _dashboardService.GetDashboardData();

        var nuevaEntrada = new EntradaCacheDashboard(
            dashboard,
            replicaActual,
            _timeProvider.GetLocalNow());

        bool? almacenado = estado == EstadoCache.Deshabilitado
            ? null
            : _cache.Guardar(
                ClavesCache.DashboardResumen,
                JsonSerializer.Serialize(nuevaEntrada),
                _opciones.Expiracion);

        RegistrarRecuperacion(estado, lectura, almacenado, replicaActual);

        return new ResultadoDashboard(
            dashboard,
            estado,
            replicaActual,
            replicaActual,
            nuevaEntrada.GeneradoEn,
            almacenado == true ? _cache.TiempoRestante(ClavesCache.DashboardResumen) : null,
            almacenado);
    }

    private void RegistrarRecuperacion(
        EstadoCache estado,
        EstadoLecturaCache lectura,
        bool? almacenado,
        string replicaActual)
    {
        string recorrido = estado switch
        {
            EstadoCache.Miss when almacenado == true => "MISS -> MySQL -> Redis",
            EstadoCache.Miss => "MISS -> MySQL | SET fallido",
            EstadoCache.Deshabilitado => "DESHABILITADO -> MySQL",
            EstadoCache.EntradaInvalida => "ENTRADA INVALIDA -> MySQL",
            _ => "ERROR GET -> MySQL"
        };
        string escritura = almacenado switch
        {
            true => "Exitosa",
            false => "Fallida",
            null => "NoIntentada"
        };
        string resultadoLectura = estado == EstadoCache.EntradaInvalida
            ? "ValorPresenteInvalido"
            : lectura.ToString();
        LogLevel nivel = estado is EstadoCache.NoDisponible or EstadoCache.EntradaInvalida || almacenado == false
            ? LogLevel.Warning
            : LogLevel.Information;

        _logger.Log(nivel,
            "{Recorrido} | operación GET/SET | clave {Clave} | réplica {Replica} | lectura {Lectura} | estado {Estado} | escritura {Escritura}",
            recorrido, ClavesCache.DashboardResumen, replicaActual, resultadoLectura, estado, escritura);
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
            using JsonDocument documento = JsonDocument.Parse(valor);
            return EsEntradaValida(documento.RootElement)
                ? documento.RootElement.Deserialize<EntradaCacheDashboard>()
                : null;
        }
        catch (JsonException)
        {
            // El coordinador registra EntradaInvalida sin incluir el contenido recibido.
            return null;
        }
    }

    private static bool EsEntradaValida(JsonElement entrada)
    {
        return entrada.ValueKind == JsonValueKind.Object
            && entrada.TryGetProperty(nameof(EntradaCacheDashboard.Dashboard), out JsonElement resumen)
            && resumen.ValueKind == JsonValueKind.Object
            && TieneConteoValido(resumen, nameof(DashboardViewModel.MecanicosDisponibles))
            && TieneConteoValido(resumen, nameof(DashboardViewModel.VehiculosRegistrados))
            && TieneConteoValido(resumen, nameof(DashboardViewModel.ServiciosRegistrados))
            && entrada.TryGetProperty(nameof(EntradaCacheDashboard.Replica), out JsonElement replica)
            && replica.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(replica.GetString())
            && entrada.TryGetProperty(nameof(EntradaCacheDashboard.GeneradoEn), out JsonElement generadoEn)
            && generadoEn.ValueKind == JsonValueKind.String
            && generadoEn.TryGetDateTimeOffset(out DateTimeOffset fecha)
            && fecha != default;
    }

    private static bool TieneConteoValido(JsonElement resumen, string propiedad)
    {
        return resumen.TryGetProperty(propiedad, out JsonElement conteo)
            && conteo.ValueKind == JsonValueKind.Number
            && conteo.TryGetInt32(out int cantidad)
            && cantidad >= 0;
    }

    private sealed record EntradaCacheDashboard(
        DashboardViewModel Dashboard,
        string Replica,
        DateTimeOffset GeneradoEn);
}
