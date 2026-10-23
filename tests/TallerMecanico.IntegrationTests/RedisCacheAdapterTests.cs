using System.Reflection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Infraestructura.Cache;

namespace TallerMecanico.IntegrationTests;

public sealed class RedisCacheAdapterTests
{
    private readonly ConexionRedisPrueba _redis = new();
    private readonly RegistradorCachePrueba<RedisCacheAdapter> _logger = new();

    [Fact]
    public void GetSinClave_EsMiss()
    {
        Assert.Equal(EstadoLecturaCache.Miss, CrearAdapter().Obtener("clave-prueba").Estado);
    }

    [Theory]
    [InlineData("contenido")]
    [InlineData("")]
    public void GetConValor_InclusoVacio_EsLecturaHit(string valor)
    {
        _redis.Valor = valor;

        ResultadoLecturaCache resultado = CrearAdapter().Obtener("clave-prueba");

        Assert.Equal(EstadoLecturaCache.Hit, resultado.Estado);
        Assert.Equal(valor, resultado.Valor);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetFallaAunqueIsConnectedSeaTrue_EsErrorDiagnosticado(bool timeout)
    {
        _redis.FalloGet = timeout ? new TimeoutException("Password=no-publicar")
            : new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Password=no-publicar");

        ResultadoLecturaCache resultado = CrearAdapter().Obtener("clave-prueba");

        Assert.Equal(EstadoLecturaCache.Error, resultado.Estado);
        Assert.Null(resultado.Valor);
        Assert.Equal(1, _redis.Lecturas);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("GET") && mensaje.Contains("clave-prueba")
            && mensaje.Contains(Environment.MachineName) && mensaje.Contains(_redis.FalloGet.GetType().Name));
        Assert.DoesNotContain(_logger.Mensajes, mensaje => mensaje.Contains("Password=no-publicar"));
    }

    [Fact]
    public void SinConexion_NoEsMissNiDeshabilitado()
    {
        _redis.Conectado = false;

        Assert.Equal(EstadoLecturaCache.Error, CrearAdapter().Obtener("clave-prueba").Estado);
        Assert.Equal(0, _redis.Lecturas);
        Assert.NotEmpty(_logger.Mensajes);
    }

    [Fact]
    public void SetConTimeout_DevuelveFalloSinPropagarExcepcion()
    {
        _redis.FalloSet = new TimeoutException("Password=no-publicar");

        Assert.False(CrearAdapter().Guardar("clave-prueba", "contenido", TimeSpan.FromSeconds(60)));
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("SET") && mensaje.Contains("TimeoutException"));
        Assert.DoesNotContain(_logger.Mensajes, mensaje => mensaje.Contains("Password=no-publicar"));
    }

    [Fact]
    public void CacheDeshabilitada_ExplicitaSuEstado()
    {
        Assert.Equal(EstadoLecturaCache.Deshabilitado,
            new CacheDistribuidaDeshabilitada().Obtener("clave-prueba").Estado);
    }

    [Theory]
    [InlineData(true, ResultadoEliminacionCache.Eliminada)]
    [InlineData(false, ResultadoEliminacionCache.Inexistente)]
    public void Del_DistingueClaveEliminadaDeInexistente(bool eliminada, ResultadoEliminacionCache esperado)
    {
        _redis.ClaveEliminada = eliminada;
        Assert.Equal(esperado, CrearAdapter().Eliminar("clave-prueba"));
        Assert.Equal(1, _redis.Eliminaciones);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("DEL") && mensaje.Contains(esperado.ToString()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DelFallido_NoEsClaveInexistenteYAdvierteExpiracion(bool timeout)
    {
        _redis.FalloDel = timeout ? new TimeoutException("Password=no-publicar")
            : new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Password=no-publicar");
        Assert.Equal(ResultadoEliminacionCache.Error, CrearAdapter().Eliminar("clave-prueba"));
        Assert.Contains(_logger.Niveles, nivel => nivel == LogLevel.Warning);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("hasta su expiración"));
        Assert.DoesNotContain(_logger.Mensajes, mensaje => mensaje.Contains("Password=no-publicar"));
    }

    [Fact]
    public void DelSinConexion_NoEjecutaComandoNiInformaExito()
    {
        _redis.Conectado = false;
        Assert.Equal(ResultadoEliminacionCache.NoDisponible, CrearAdapter().Eliminar("clave-prueba"));
        Assert.Equal(0, _redis.Eliminaciones);
        Assert.Equal(ResultadoEliminacionCache.Deshabilitado,
            new CacheDistribuidaDeshabilitada().Eliminar("clave-prueba"));
    }

    private RedisCacheAdapter CrearAdapter() => new(_redis.Conexion, _logger);
}

internal sealed class ConexionRedisPrueba
{
    public bool Conectado { get; set; } = true;
    public RedisValue Valor { get; set; } = RedisValue.Null;
    public Exception? FalloGet { get; set; }
    public Exception? FalloSet { get; set; }
    public Exception? FalloDel { get; set; }
    public bool ClaveEliminada { get; set; } = true;
    public int Eliminaciones { get; private set; }
    public Action? AntesDeEliminar { get; set; }
    public int Lecturas { get; private set; }
    public int Escrituras { get; private set; }
    public IConnectionMultiplexer Conexion { get; }

    public ConexionRedisPrueba()
    {
        IDatabase baseDatos = DispatchProxy.Create<IDatabase, ProxyRedisPrueba>();
        ((ProxyRedisPrueba)baseDatos).Responder = nombre => nombre switch
        {
            nameof(IDatabase.StringGet) => Leer(),
            nameof(IDatabase.StringSet) => Guardar(),
            nameof(IDatabase.KeyTimeToLive) => TimeSpan.FromSeconds(30),
            nameof(IDatabase.KeyDelete) => Eliminar(),
            _ => throw new NotSupportedException(nombre)
        };
        Conexion = DispatchProxy.Create<IConnectionMultiplexer, ProxyRedisPrueba>();
        ((ProxyRedisPrueba)Conexion).Responder = nombre => nombre switch
        {
            "get_IsConnected" => Conectado,
            nameof(IConnectionMultiplexer.GetDatabase) => baseDatos,
            _ => throw new NotSupportedException(nombre)
        };
    }

    private RedisValue Leer()
    {
        Lecturas++;
        if (FalloGet is not null) throw FalloGet;
        return Valor;
    }

    private bool Guardar()
    {
        Escrituras++;
        if (FalloSet is not null) throw FalloSet;
        return true;
    }

    private bool Eliminar()
    {
        Eliminaciones++;
        AntesDeEliminar?.Invoke();
        if (FalloDel is not null) throw FalloDel;
        return ClaveEliminada;
    }
}

public class ProxyRedisPrueba : DispatchProxy
{
    public Func<string, object?> Responder { get; set; } = _ => throw new NotSupportedException();
    protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos) => Responder(metodo!.Name);
}

internal sealed class RegistradorCachePrueba<T> : ILogger<T>
{
    public List<string> Mensajes { get; } = [];
    public List<LogLevel> Niveles { get; } = [];
    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? error,
        Func<TState, Exception?, string> formato)
    {
        Mensajes.Add(formato(estado, error));
        Niveles.Add(nivel);
    }
}
