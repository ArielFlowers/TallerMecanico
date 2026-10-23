using StackExchange.Redis;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.Infraestructura.Cache;

// H2: adaptador de Redis para el puerto de caché distribuida.
// Usa SET con expiración absoluta (PX en la publicación condicional), por lo que
// los valores se pueden inspeccionar con redis-cli GET clave.
public sealed class RedisCacheAdapter : ICacheDistribuidaPort, ICacheResumenDashboardPort
{
    private const string LeerResumen = """
        local generacion = redis.call('GET', KEYS[2])
        if not generacion then
            redis.call('SET', KEYS[2], ARGV[1])
            redis.call('DEL', KEYS[1])
            generacion = ARGV[1]
        end
        return {generacion, redis.call('GET', KEYS[1])}
        """;

    private const string PublicarResumenCondicional = """
        if redis.call('GET', KEYS[2]) ~= ARGV[1] then return 0 end
        local actual = redis.call('GET', KEYS[1])
        if ARGV[4] == '0' then
            if actual then return 2 end
        elseif actual ~= ARGV[5] then
            return 2
        end
        redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
        return 1
        """;

    private const string InvalidarResumenConGeneracion = """
        redis.call('SET', KEYS[2], ARGV[1])
        return redis.call('DEL', KEYS[1])
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheAdapter> _logger;

    public RedisCacheAdapter(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheAdapter> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public bool EstaDisponible => _redis.IsConnected;

    public LecturaResumenDashboard ObtenerResumen()
    {
        var error = new LecturaResumenDashboard(new(EstadoLecturaCache.Error));
        return Ejecutar("EVAL GET/generación", ClavesCache.DashboardResumen, baseDatos =>
        {
            RedisResult[] respuesta = (RedisResult[])baseDatos.ScriptEvaluate(
                LeerResumen, ClavesResumen(), [NuevaGeneracion()])!;
            string? valor = (string?)respuesta[1];
            return new LecturaResumenDashboard(
                new(valor is null ? EstadoLecturaCache.Miss : EstadoLecturaCache.Hit, valor),
                (string?)respuesta[0]);
        }, error, error);
    }

    public EstadoPublicacionResumen PublicarResumen(
        LecturaResumenDashboard lectura, string valor, TimeSpan expiracion)
    {
        if (lectura.Generacion is null) return EstadoPublicacionResumen.NoDisponible;

        EstadoPublicacionResumen resultado = Ejecutar("EVAL SET condicional", ClavesCache.DashboardResumen,
            baseDatos => (long)baseDatos.ScriptEvaluate(PublicarResumenCondicional, ClavesResumen(),
                [lectura.Generacion, valor, (long)expiracion.TotalMilliseconds,
                    lectura.Lectura.Valor is null ? "0" : "1", lectura.Lectura.Valor ?? string.Empty]) switch
                {
                    1 => EstadoPublicacionResumen.Publicada,
                    0 => EstadoPublicacionResumen.RechazadaPorGeneracion,
                    _ => EstadoPublicacionResumen.EntradaExistente
                }, EstadoPublicacionResumen.Error, EstadoPublicacionResumen.NoDisponible);

        if (resultado is EstadoPublicacionResumen.RechazadaPorGeneracion or EstadoPublicacionResumen.EntradaExistente)
            _logger.LogInformation("PUBLICACION RECHAZADA | clave {Clave} | réplica {Replica} | resultado {Resultado}",
                ClavesCache.DashboardResumen, Environment.MachineName, resultado);
        return resultado;
    }

    public ResultadoEliminacionCache InvalidarResumen()
    {
        ResultadoEliminacionCache resultado = Ejecutar("EVAL generación/DEL", ClavesCache.DashboardResumen,
            baseDatos => (long)baseDatos.ScriptEvaluate(InvalidarResumenConGeneracion, ClavesResumen(),
                [NuevaGeneracion()]) == 1 ? ResultadoEliminacionCache.Eliminada : ResultadoEliminacionCache.Inexistente,
            ResultadoEliminacionCache.Error, ResultadoEliminacionCache.NoDisponible);
        RegistrarInvalidacion(ClavesCache.DashboardResumen, resultado);
        return resultado;
    }

    public TimeSpan? TiempoRestanteResumen() => TiempoRestante(ClavesCache.DashboardResumen);

    private static RedisKey[] ClavesResumen() =>
        [ClavesCache.DashboardResumen, ClavesCache.DashboardGeneracion];

    // No se reutilizan versiones al perder metadatos por reinicio o expulsión.
    private static string NuevaGeneracion() => Guid.NewGuid().ToString("N");

    public ResultadoLecturaCache Obtener(string clave)
    {
        var error = new ResultadoLecturaCache(EstadoLecturaCache.Error);
        return Ejecutar(
            "GET",
            clave,
            baseDatos =>
            {
                RedisValue valor = baseDatos.StringGet(clave);
                return valor.IsNull
                    ? new ResultadoLecturaCache(EstadoLecturaCache.Miss)
                    : new ResultadoLecturaCache(EstadoLecturaCache.Hit, (string?)valor);
            },
            valorPorDefecto: error,
            valorSinConexion: error);
    }

    public bool Guardar(string clave, string valor, TimeSpan expiracion)
    {
        return Ejecutar(
            "SET",
            clave,
            baseDatos => baseDatos.StringSet(clave, valor, expiracion),
            valorPorDefecto: false,
            valorSinConexion: false);
    }

    public ResultadoEliminacionCache Eliminar(string clave)
    {
        ResultadoEliminacionCache resultado = Ejecutar(
            "DEL",
            clave,
            baseDatos => baseDatos.KeyDelete(clave)
                ? ResultadoEliminacionCache.Eliminada
                : ResultadoEliminacionCache.Inexistente,
            valorPorDefecto: ResultadoEliminacionCache.Error,
            valorSinConexion: ResultadoEliminacionCache.NoDisponible);

        RegistrarInvalidacion(clave, resultado);
        return resultado;
    }

    private void RegistrarInvalidacion(string clave, ResultadoEliminacionCache resultado)
    {
        bool fallo = resultado is ResultadoEliminacionCache.Error or ResultadoEliminacionCache.NoDisponible;
        _logger.Log(fallo ? LogLevel.Warning : LogLevel.Information,
            "INVALIDACION -> Redis DEL | clave {Clave} | réplica {Replica} | resultado {Resultado} | {Detalle}",
            clave, Environment.MachineName, resultado,
            fallo ? "No se confirmó la invalidación; pueden persistir datos anteriores hasta su expiración."
                : "Redis confirmó el resultado de DEL.");
    }

    public TimeSpan? TiempoRestante(string clave)
    {
        return Ejecutar(
            "TTL",
            clave,
            baseDatos => baseDatos.KeyTimeToLive(clave),
            valorPorDefecto: null,
            valorSinConexion: null);
    }

    private T Ejecutar<T>(
        string comando,
        string clave,
        Func<IDatabase, T> operacion,
        T valorPorDefecto,
        T valorSinConexion)
    {
        try
        {
            if (!_redis.IsConnected)
            {
                _logger.LogWarning(
                    "Redis sin conexión | operación {Comando} | clave {Clave} | réplica {Replica}.",
                    comando, clave, Environment.MachineName);
                return valorSinConexion;
            }

            return operacion(_redis.GetDatabase());
        }
        catch (Exception error) when (error is RedisException or TimeoutException or ObjectDisposedException)
        {
            _logger.LogWarning(
                "Falló Redis | operación {Comando} | clave {Clave} | réplica {Replica} | resultado Error | tipo {TipoError}.",
                comando, clave, Environment.MachineName, error.GetType().Name);
            return valorPorDefecto;
        }
    }
}
