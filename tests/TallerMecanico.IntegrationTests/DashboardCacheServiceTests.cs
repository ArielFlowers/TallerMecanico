using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public void PrimeraConsulta_EsMissConsultaMySqlYGuardaEnRedis()
    {
        var servicio = CrearServicio();

        ResultadoDashboard resultado = servicio.Obtener();

        Assert.Equal(EstadoCache.Miss, resultado.Estado);
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
    public void ValorCorrupto_SeTrataComoMiss()
    {
        _cache.Guardar(ClavesCache.DashboardResumen, "no es json", TimeSpan.FromSeconds(30));
        var servicio = CrearServicio();

        ResultadoDashboard resultado = servicio.Obtener();

        Assert.Equal(EstadoCache.Miss, resultado.Estado);
        Assert.Equal(3, resultado.Dashboard.MecanicosDisponibles);
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

    private DashboardCacheService CrearServicio()
    {
        return new DashboardCacheService(
            new DashboardService(_servicios, _vehiculos, _mecanicos),
            _cache,
            new OpcionesCacheDashboard { Expiracion = TimeSpan.FromSeconds(30) },
            TimeProvider.System,
            NullLogger<DashboardCacheService>.Instance);
    }

    private sealed class CacheEnMemoria : ICacheDistribuidaPort
    {
        public Dictionary<string, string> Valores { get; } = [];

        public Dictionary<string, TimeSpan> Expiraciones { get; } = [];

        public bool Disponible { get; set; } = true;

        public bool EstaDisponible => Disponible;

        public string? Obtener(string clave) =>
            Disponible && Valores.TryGetValue(clave, out string? valor) ? valor : null;

        public bool Guardar(string clave, string valor, TimeSpan expiracion)
        {
            if (!Disponible)
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
