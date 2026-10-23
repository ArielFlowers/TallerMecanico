using System.Reflection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Infraestructura.Cache;
using TallerMecanico.Aplicacion.Servicios;

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

    [Fact]
    public void Resumen_PublicacionCondicionalRechazaGeneracionAnterior()
    {
        var adapter = CrearAdapter();
        LecturaResumenDashboard lectura = adapter.ObtenerResumen();
        string? anterior = lectura.Generacion;
        adapter.InvalidarResumen();
        Assert.NotEqual(anterior, _redis.Generacion);
        Assert.Equal(EstadoPublicacionResumen.RechazadaPorGeneracion,
            adapter.PublicarResumen(lectura, "anterior", TimeSpan.FromSeconds(60)));
        Assert.True(_redis.Valor.IsNull);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("RechazadaPorGeneracion"));
    }

    [Fact]
    public void Resumen_DosMiss_NoSobrescribenLaPrimeraPublicacion()
    {
        var adapter = CrearAdapter();
        var lecturaA = adapter.ObtenerResumen();
        var lecturaB = adapter.ObtenerResumen();
        Assert.Equal(EstadoPublicacionResumen.Publicada, adapter.PublicarResumen(lecturaA, "primero", TimeSpan.FromSeconds(60)));
        Assert.Equal(EstadoPublicacionResumen.EntradaExistente, adapter.PublicarResumen(lecturaB, "segundo", TimeSpan.FromSeconds(60)));
        Assert.Equal("primero", _redis.Valor.ToString());
    }

    [Fact]
    public void Resumen_GeneracionPerdida_RechazaEscritorAnteriorInclusoTrasRecrear()
    {
        var adapter = CrearAdapter();
        var anterior = adapter.ObtenerResumen();
        _redis.Generacion = null;
        Assert.Equal(EstadoPublicacionResumen.RechazadaPorGeneracion,
            adapter.PublicarResumen(anterior, "anterior", TimeSpan.FromSeconds(60)));
        _redis.Valor = "entrada-sin-generacion";
        var nueva = adapter.ObtenerResumen();
        Assert.NotEqual(anterior.Generacion, nueva.Generacion);
        Assert.Equal(EstadoLecturaCache.Miss, nueva.Lectura.Estado);
        Assert.Equal(EstadoPublicacionResumen.RechazadaPorGeneracion,
            adapter.PublicarResumen(anterior, "anterior", TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void Resumen_EntradaInvalida_NoSobrescribeReparacionConcurrente()
    {
        _redis.Valor = "invalido";
        var adapter = CrearAdapter();
        var anterior = adapter.ObtenerResumen();
        Assert.Equal(EstadoPublicacionResumen.Publicada, adapter.PublicarResumen(anterior, "corregido", TimeSpan.FromSeconds(60)));
        Assert.Equal(EstadoPublicacionResumen.EntradaExistente, adapter.PublicarResumen(anterior, "segunda-correccion", TimeSpan.FromSeconds(60)));
        Assert.Equal("corregido", _redis.Valor.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resumen_PublicacionFallida_NoEsRechazoConcurrenteNiExito(bool conectado)
    {
        var adapter = CrearAdapter();
        var lectura = adapter.ObtenerResumen();
        _redis.Conectado = conectado;
        _redis.FalloSet = new TimeoutException("secreto-no-registrar");
        Assert.Equal(conectado ? EstadoPublicacionResumen.Error : EstadoPublicacionResumen.NoDisponible,
            adapter.PublicarResumen(lectura, "contenido", TimeSpan.FromSeconds(60)));
        Assert.True(_redis.Valor.IsNull);
        Assert.DoesNotContain(_logger.Mensajes, mensaje => mensaje.Contains("secreto-no-registrar") || mensaje.Contains("PUBLICACION RECHAZADA"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resumen_InvalidacionFallida_NoConfirmaAvanceNiEliminacion(bool conectado)
    {
        _redis.Conectado = conectado;
        _redis.FalloDel = new TimeoutException("secreto-no-registrar");
        string? anterior = _redis.Generacion;
        Assert.Equal(conectado ? ResultadoEliminacionCache.Error : ResultadoEliminacionCache.NoDisponible,
            CrearAdapter().InvalidarResumen());
        Assert.Equal(anterior, _redis.Generacion);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("hasta su expiración"));
    }

    [Fact]
    public void Resumen_Deshabilitado_NoSimulaLecturaNiPublicacion()
    {
        var cache = new CacheDistribuidaDeshabilitada();
        var lectura = cache.ObtenerResumen();
        Assert.Equal(EstadoLecturaCache.Deshabilitado, lectura.Lectura.Estado);
        Assert.Null(lectura.Generacion);
        Assert.Equal(EstadoPublicacionResumen.Deshabilitado, cache.PublicarResumen(lectura, "contenido", TimeSpan.FromSeconds(60)));
        Assert.Equal(ResultadoEliminacionCache.Deshabilitado, cache.InvalidarResumen());
    }
}

internal sealed class ConexionRedisPrueba
{
    public string? Generacion { get; set; } = "generacion-prueba";
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
        ((ProxyRedisPrueba)baseDatos).Responder = (nombre, argumentos) => nombre switch
        {
            nameof(IDatabase.StringGet) => Leer(),
            nameof(IDatabase.StringSet) => Guardar(),
            nameof(IDatabase.KeyTimeToLive) => TimeSpan.FromSeconds(30),
            nameof(IDatabase.KeyDelete) => Eliminar(),
            nameof(IDatabase.ScriptEvaluate) => EjecutarScript((string)argumentos![0]!, (RedisValue[])argumentos[2]!),
            _ => throw new NotSupportedException(nombre)
        };
        Conexion = DispatchProxy.Create<IConnectionMultiplexer, ProxyRedisPrueba>();
        ((ProxyRedisPrueba)Conexion).Responder = (nombre, _) => nombre switch
        {
            "get_IsConnected" => Conectado,
            nameof(IConnectionMultiplexer.GetDatabase) => baseDatos,
            _ => throw new NotSupportedException(nombre)
        };
    }

    private RedisResult EjecutarScript(string script, RedisValue[] argumentos)
    {
        if (script.Contains("return {generacion"))
        {
            RedisValue contenido = Leer();
            if (Generacion is null) { Generacion = argumentos[0].ToString(); Valor = RedisValue.Null; contenido = Valor; }
            return RedisResult.Create(new[] { RedisResult.Create((RedisValue)Generacion), RedisResult.Create(contenido) });
        }
        if (script.Contains("return redis.call('DEL'"))
        {
            bool eliminado = Eliminar();
            Generacion = argumentos[0].ToString();
            Valor = RedisValue.Null;
            return RedisResult.Create((RedisValue)(eliminado ? 1 : 0));
        }
        Escrituras++;
        if (FalloSet is not null) throw FalloSet;
        if (Generacion != argumentos[0].ToString()) return RedisResult.Create((RedisValue)0);
        bool igual = argumentos[3] == "0" ? Valor.IsNull : Valor == argumentos[4];
        if (!igual) return RedisResult.Create((RedisValue)2);
        Valor = argumentos[1];
        return RedisResult.Create((RedisValue)1);
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
    public Func<string, object?[]?, object?> Responder { get; set; } = (_, _) => throw new NotSupportedException();
    protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos) => Responder(metodo!.Name, argumentos);
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
