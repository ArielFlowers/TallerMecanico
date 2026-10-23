using StackExchange.Redis;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Infraestructura.Cache;

// H2: adaptador de Redis para el puerto de caché distribuida.
// Usa SET clave valor EX segundos (expiración absoluta), por lo que
// los valores se pueden inspeccionar con redis-cli GET clave.
public sealed class RedisCacheAdapter : ICacheDistribuidaPort
{
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

        bool fallo = resultado is ResultadoEliminacionCache.Error or ResultadoEliminacionCache.NoDisponible;
        _logger.Log(fallo ? LogLevel.Warning : LogLevel.Information,
            "INVALIDACION -> Redis DEL | clave {Clave} | réplica {Replica} | resultado {Resultado} | {Detalle}",
            clave, Environment.MachineName, resultado,
            fallo ? "No se confirmó la invalidación; pueden persistir datos anteriores hasta su expiración."
                : "Redis confirmó el resultado de DEL.");
        return resultado;
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
