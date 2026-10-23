using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using TallerMecanico.Infraestructura.Cache;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.IntegrationTests;

public sealed class DashboardCacheServiceTests
{
    private readonly RepositorioConteo<Mecanico> _mecanicos = new(3);
    private readonly RepositorioConteo<Vehiculo> _vehiculos = new(5);
    private readonly RepositorioConteo<Servicio> _servicios = new(7);
    private readonly CacheEnMemoria _cache = new();
    private readonly RegistradorCachePrueba<DashboardCacheService> _logger = new();

    [Fact]
    public void PrimeraConsulta_EsMissConsultaMySqlYGuardaEnRedis()
    {
        var servicio = CrearServicio();

        ResultadoDashboard resultado = servicio.Obtener();

        Assert.Equal(EstadoCache.Miss, resultado.Estado);
        Assert.True(resultado.Almacenado);
        Assert.Equal(3, resultado.Dashboard.MecanicosDisponibles);
        Assert.Equal(5, resultado.Dashboard.VehiculosRegistrados);
        Assert.Equal(7, resultado.Dashboard.ServiciosRegistrados);
        Assert.Equal(1, _mecanicos.Consultas);
        Assert.True(_cache.Valores.ContainsKey(ClavesCache.DashboardResumen));
        Assert.Equal(TimeSpan.FromSeconds(30), _cache.Expiraciones[ClavesCache.DashboardResumen]);
    }

    [Fact]
    public void SegundaConsulta_EsHitYNoTocaMySql()
    {
        var servicio = CrearServicio();
        servicio.Obtener();

        ResultadoDashboard resultado = servicio.Obtener();

        Assert.Equal(EstadoCache.Hit, resultado.Estado);
        Assert.Null(resultado.Almacenado);
        Assert.Equal(5, resultado.Dashboard.VehiculosRegistrados);
        Assert.Equal(1, _mecanicos.Consultas);
        Assert.Equal(1, _vehiculos.Consultas);
        Assert.Equal(1, _servicios.Consultas);
    }

    [Fact]
    public void OtraReplica_LeeElValorGuardadoEnLaCacheCompartida()
    {
        var replicaUno = CrearServicio();
        var replicaDos = CrearServicio();
        replicaUno.Obtener();

        ResultadoDashboard resultado = replicaDos.Obtener();

        Assert.Equal(EstadoCache.Hit, resultado.Estado);
        Assert.Equal(1, _mecanicos.Consultas);
        Assert.Equal(Environment.MachineName, resultado.ReplicaOrigen);
    }

    [Fact]
    public void Invalidar_ProvocaMissYRepoblaDesdeMySql()
    {
        var servicio = CrearServicio();
        servicio.Obtener();
        _vehiculos.Total = 6;

        Assert.True(servicio.Invalidar());
        ResultadoDashboard resultado = servicio.Obtener();

        Assert.Equal(EstadoCache.Miss, resultado.Estado);
        Assert.Equal(6, resultado.Dashboard.VehiculosRegistrados);
        Assert.Equal(2, _vehiculos.Consultas);
    }

    [Fact]
    public void ValorExpirado_VuelveASerMiss()
    {
        var servicio = CrearServicio();
        servicio.Obtener();

        _cache.Expirar(ClavesCache.DashboardResumen);
        ResultadoDashboard resultado = servicio.Obtener();

        Assert.Equal(EstadoCache.Miss, resultado.Estado);
        Assert.Equal(2, _mecanicos.Consultas);
    }

    [Fact]
    public void ValorCorrupto_NoSeTrataComoHitNiMiss()
    {
        _cache.Guardar(ClavesCache.DashboardResumen, "no es json", TimeSpan.FromSeconds(30));
        var servicio = CrearServicio();

        ResultadoDashboard resultado = servicio.Obtener();

        Assert.Equal(EstadoCache.EntradaInvalida, resultado.Estado);
        Assert.Equal(3, resultado.Dashboard.MecanicosDisponibles);
        Assert.True(resultado.Almacenado);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("ENTRADA INVALIDA -> MySQL"));
        Assert.DoesNotContain(_logger.Mensajes, mensaje => mensaje.Contains("HIT ->") || mensaje.Contains("MISS ->")
            || mensaje.Contains("lectura Hit"));
    }

    [Fact]
    public void RedisNoDisponible_ConsultaMySqlSinFallar()
    {
        _cache.Disponible = false;
        var servicio = CrearServicio();

        ResultadoDashboard primera = servicio.Obtener();
        ResultadoDashboard segunda = servicio.Obtener();

        Assert.Equal(EstadoCache.NoDisponible, primera.Estado);
        Assert.Equal(EstadoCache.NoDisponible, segunda.Estado);
        Assert.Equal(7, segunda.Dashboard.ServiciosRegistrados);
        Assert.Equal(2, _servicios.Consultas);
        Assert.Null(segunda.TiempoRestante);
    }

    [Fact]
    public void MissConSetFallido_ConservaMissSinAfirmarAlmacenamiento()
    {
        _cache.PermitirGuardar = false;

        ResultadoDashboard resultado = CrearServicio().Obtener();

        Assert.Equal(EstadoCache.Miss, resultado.Estado);
        Assert.False(resultado.Almacenado);
        Assert.Null(resultado.TiempoRestante);
        Assert.Equal(1, _mecanicos.Consultas);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("MISS -> MySQL | SET fallido"));
        Assert.DoesNotContain(_logger.Mensajes, mensaje => mensaje.Contains("MISS -> MySQL -> Redis"));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void GetFallido_ConservaErrorIndependientementeDelSet(bool timeout, bool guardar)
    {
        var redis = new ConexionRedisPrueba
        {
            FalloGet = timeout ? new TimeoutException("secreto-no-registrar")
                : new StackExchange.Redis.RedisConnectionException(
                    StackExchange.Redis.ConnectionFailureType.UnableToConnect, "secreto-no-registrar"),
            FalloSet = guardar ? null : new TimeoutException("secreto-no-registrar")
        };
        var adapter = new RedisCacheAdapter(redis.Conexion, NullLogger<RedisCacheAdapter>.Instance);

        ResultadoDashboard resultado = CrearServicio(adapter).Obtener();

        Assert.Equal(EstadoCache.NoDisponible, resultado.Estado);
        Assert.Equal(guardar, resultado.Almacenado);
        Assert.Equal(1, redis.Lecturas);
        Assert.Equal(1, redis.Escrituras);
        Assert.Equal(1, _mecanicos.Consultas);
        Assert.Equal(1, _vehiculos.Consultas);
        Assert.Equal(1, _servicios.Consultas);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("ERROR GET -> MySQL")
            && mensaje.Contains("lectura Error") && mensaje.Contains(guardar ? "escritura Exitosa" : "escritura Fallida"));
        Assert.DoesNotContain(_logger.Mensajes, mensaje => mensaje.Contains("MISS") || mensaje.Contains("HIT")
            || mensaje.Contains("secreto-no-registrar"));
    }

    [Fact]
    public void CacheDeshabilitada_NoSimulaMissNiIntentaGuardar()
    {
        ResultadoDashboard resultado = CrearServicio(new CacheDistribuidaDeshabilitada()).Obtener();

        Assert.Equal(EstadoCache.Deshabilitado, resultado.Estado);
        Assert.Null(resultado.Almacenado);
        Assert.Null(resultado.TiempoRestante);
        Assert.Equal(1, _mecanicos.Consultas);
        Assert.Equal(1, _vehiculos.Consultas);
        Assert.Equal(1, _servicios.Consultas);
        Assert.Contains(_logger.Mensajes, mensaje => mensaje.Contains("DESHABILITADO -> MySQL")
            && mensaje.Contains("escritura NoIntentada"));
        Assert.DoesNotContain(_logger.Mensajes, mensaje => mensaje.Contains("MISS") || mensaje.Contains("HIT"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{\"Dashboard\":null,\"Replica\":\"A\",\"GeneradoEn\":\"2026-10-22T12:00:00Z\"}")]
    [InlineData("{\"Dashboard\":{},\"Replica\":\"A\",\"GeneradoEn\":\"2026-10-22T12:00:00Z\"}")]
    public void EntradaIncompleta_ConsultaMySqlSinInformarMiss(string json)
    {
        _cache.Valores[ClavesCache.DashboardResumen] = json;

        ResultadoDashboard resultado = CrearServicio().Obtener();

        Assert.Equal(EstadoCache.EntradaInvalida, resultado.Estado);
        Assert.Equal(1, _mecanicos.Consultas);
        Assert.Equal(1, _vehiculos.Consultas);
        Assert.Equal(1, _servicios.Consultas);
    }

    [Theory]
    [InlineData("MecanicosDisponibles")]
    [InlineData("VehiculosRegistrados")]
    [InlineData("ServiciosRegistrados")]
    public void ConteoObligatorioAusente_NoSeConvierteEnCeroValido(string propiedad)
    {
        var entrada = EntradaValida();
        ((Dictionary<string, object?>)entrada["Dashboard"]!).Remove(propiedad);
        _cache.Valores[ClavesCache.DashboardResumen] = JsonSerializer.Serialize(entrada);

        Assert.Equal(EstadoCache.EntradaInvalida, CrearServicio().Obtener().Estado);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.5)]
    [InlineData(2147483648L)]
    [InlineData("3")]
    [InlineData(null)]
    [InlineData(true)]
    public void ConteoInvalido_NoEsHit(object? conteo)
    {
        var entrada = EntradaValida();
        ((Dictionary<string, object?>)entrada["Dashboard"]!)["MecanicosDisponibles"] = conteo;
        _cache.Valores[ClavesCache.DashboardResumen] = JsonSerializer.Serialize(entrada);

        Assert.Equal(EstadoCache.EntradaInvalida, CrearServicio().Obtener().Estado);
    }

    [Theory]
    [InlineData("Replica", null)]
    [InlineData("Replica", " ")]
    [InlineData("Replica", 1)]
    [InlineData("GeneradoEn", null)]
    [InlineData("GeneradoEn", "fecha inválida")]
    [InlineData("GeneradoEn", "0001-01-01T00:00:00Z")]
    public void MetadatosInvalidos_NoSonHit(string propiedad, object? valor)
    {
        var entrada = EntradaValida();
        entrada[propiedad] = valor;
        _cache.Valores[ClavesCache.DashboardResumen] = JsonSerializer.Serialize(entrada);

        Assert.Equal(EstadoCache.EntradaInvalida, CrearServicio().Obtener().Estado);
    }

    [Fact]
    public void ConteosCeroYFormatoExistente_SonHitSinConsultarMySql()
    {
        _cache.Valores[ClavesCache.DashboardResumen] = JsonSerializer.Serialize(EntradaValida());

        ResultadoDashboard resultado = CrearServicio().Obtener();

        Assert.Equal(EstadoCache.Hit, resultado.Estado);
        Assert.Equal(0, resultado.Dashboard.MecanicosDisponibles);
        Assert.Equal(0, resultado.Dashboard.VehiculosRegistrados);
        Assert.Equal(0, resultado.Dashboard.ServiciosRegistrados);
        Assert.Equal("replica-origen", resultado.ReplicaOrigen);
        Assert.Equal(0, _mecanicos.Consultas);
        Assert.Equal(0, _vehiculos.Consultas);
        Assert.Equal(0, _servicios.Consultas);
    }

    private static Dictionary<string, object?> EntradaValida() => new()
    {
        ["Dashboard"] = new Dictionary<string, object?>
        {
            [nameof(DashboardViewModel.MecanicosDisponibles)] = 0,
            [nameof(DashboardViewModel.VehiculosRegistrados)] = 0,
            [nameof(DashboardViewModel.ServiciosRegistrados)] = 0
        },
        ["Replica"] = "replica-origen",
        ["GeneradoEn"] = "2026-10-22T12:00:00Z"
    };

    private DashboardCacheService CrearServicio(ICacheDistribuidaPort? cache = null)
    {
        return new DashboardCacheService(
            new DashboardService(_servicios, _vehiculos, _mecanicos),
            cache ?? _cache,
            new OpcionesCacheDashboard { Expiracion = TimeSpan.FromSeconds(30) },
            TimeProvider.System,
            _logger);
    }

    private sealed class CacheEnMemoria : ICacheDistribuidaPort
    {
        public Dictionary<string, string> Valores { get; } = [];

        public Dictionary<string, TimeSpan> Expiraciones { get; } = [];

        public bool Disponible { get; set; } = true;
        public bool PermitirGuardar { get; set; } = true;

        public bool EstaDisponible => Disponible;

        public ResultadoLecturaCache Obtener(string clave)
        {
            if (!Disponible) return new(EstadoLecturaCache.Error);
            return Valores.TryGetValue(clave, out string? valor)
                ? new(EstadoLecturaCache.Hit, valor)
                : new(EstadoLecturaCache.Miss);
        }

        public bool Guardar(string clave, string valor, TimeSpan expiracion)
        {
            if (!Disponible || !PermitirGuardar)
            {
                return false;
            }

            Valores[clave] = valor;
            Expiraciones[clave] = expiracion;
            return true;
        }

        public bool Eliminar(string clave) =>
            Disponible && Valores.Remove(clave);

        public TimeSpan? TiempoRestante(string clave) =>
            Disponible && Expiraciones.TryGetValue(clave, out TimeSpan expiracion) ? expiracion : null;

        public void Expirar(string clave)
        {
            Valores.Remove(clave);
            Expiraciones.Remove(clave);
        }
    }

    private sealed class RepositorioConteo<T>(int total) : IRepository<T>
    {
        public int Total { get; set; } = total;

        public int Consultas { get; private set; }

        public int Count()
        {
            Consultas++;
            return Total;
        }

        public List<T> GetAll() => throw new NotSupportedException();

        public T? GetById(int id) => throw new NotSupportedException();

        public void Add(T entity) => throw new NotSupportedException();

        public void Update(T entity) => throw new NotSupportedException();

        public void Delete(int id) => throw new NotSupportedException();
    }
}
