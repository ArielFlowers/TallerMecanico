using StackExchange.Redis;
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

    public string? Obtener(string clave)
    {
        return Ejecutar(
            "GET",
            clave,
            baseDatos => (string?)baseDatos.StringGet(clave),
            valorPorDefecto: null);
    }

    public bool Guardar(string clave, string valor, TimeSpan expiracion)
    {
        return Ejecutar(
            "SET",
            clave,
            baseDatos => baseDatos.StringSet(clave, valor, expiracion),
            valorPorDefecto: false);
    }

    public bool Eliminar(string clave)
    {
        return Ejecutar(
            "DEL",
            clave,
            baseDatos => baseDatos.KeyDelete(clave),
            valorPorDefecto: false);
    }

    public TimeSpan? TiempoRestante(string clave)
    {
        return Ejecutar(
            "TTL",
            clave,
            baseDatos => baseDatos.KeyTimeToLive(clave),
            valorPorDefecto: null);
    }

    private T Ejecutar<T>(
        string comando,
        string clave,
        Func<IDatabase, T> operacion,
        T valorPorDefecto)
    {
        if (!_redis.IsConnected)
        {
            _logger.LogWarning(
                "Redis no disponible; se omite {Comando} {Clave}.",
                comando,
                clave);
            return valorPorDefecto;
        }

        try
        {
            return operacion(_redis.GetDatabase());
        }
        catch (Exception error) when (error is RedisException or TimeoutException)
        {
            _logger.LogWarning(
                error,
                "Falló {Comando} {Clave} en Redis; se continúa con MySQL.",
                comando,
                clave);
            return valorPorDefecto;
        }
    }
}
