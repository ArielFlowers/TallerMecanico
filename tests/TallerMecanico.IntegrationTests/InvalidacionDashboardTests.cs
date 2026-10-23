using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Cache;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;

namespace TallerMecanico.IntegrationTests;

public sealed class InvalidacionDashboardTests
{
    [Theory]
    [InlineData(ResultadoEliminacionCache.Eliminada, "Redis confirmó la invalidación")]
    [InlineData(ResultadoEliminacionCache.Inexistente, "ya no estaba")]
    [InlineData(ResultadoEliminacionCache.NoDisponible, "No se confirmó")]
    [InlineData(ResultadoEliminacionCache.Error, "Falló la invalidación")]
    [InlineData(ResultadoEliminacionCache.Deshabilitado, "deshabilitada")]
    public void Manual_AdministradorInformaResultadoExactoYRedirige(
        ResultadoEliminacionCache resultado, string mensaje)
    {
        var cache = new CachePrueba(resultado);
        var pagina = CrearPagina(cache, "Administrador");

        Assert.IsType<RedirectToPageResult>(pagina.OnPostInvalidarCache());
        Assert.Contains(mensaje, pagina.MensajeCache);
        Assert.Equal(ClavesCache.DashboardResumen, Assert.Single(cache.ClavesEliminadas));
    }

    [Theory]
    [InlineData("Recepcionista")]
    [InlineData("")]
    public void Manual_SinRolAdministrador_NoEjecutaDel(string rol)
    {
        var cache = new CachePrueba(ResultadoEliminacionCache.Eliminada);
        Assert.IsType<ForbidResult>(CrearPagina(cache, rol).OnPostInvalidarCache());
        Assert.Empty(cache.ClavesEliminadas);
    }

    [Fact]
    public void Automatico_DelFallidoNoRevierteCreacionNiEliminacionConfirmadas()
    {
        var repositorio = new ServiciosPrueba();
        var redis = new ConexionRedisPrueba { FalloDel = new TimeoutException("dato-sensible") };
        var logger = new RegistradorCachePrueba<RedisCacheAdapter>();
        var servicio = new ServicioService(new CreadorServiciosPrueba(repositorio),
            cache: new RedisCacheAdapter(redis.Conexion, logger));
        redis.AntesDeEliminar = () => Assert.Single(repositorio.Registros);

        servicio.Crear(ServicioValido());

        Assert.Single(repositorio.Registros);
        Assert.Equal(1, redis.Eliminaciones);
        redis.AntesDeEliminar = () => Assert.Empty(repositorio.Registros);
        servicio.Eliminar(1);
        Assert.Empty(repositorio.Registros);
        Assert.Equal(2, redis.Eliminaciones);
        Assert.Contains(logger.Niveles, nivel => nivel == LogLevel.Warning);
        Assert.Contains(logger.Mensajes, mensaje => mensaje.Contains("hasta su expiración"));
        Assert.DoesNotContain(logger.Mensajes, mensaje => mensaje.Contains("dato-sensible"));
    }

    [Fact]
    public void Automatico_ValidacionErrorPersistenciaYNoExistente_NoInvalidan()
    {
        var repositorio = new ServiciosPrueba();
        var cache = new CachePrueba(ResultadoEliminacionCache.Eliminada);
        var servicio = new ServicioService(new CreadorServiciosPrueba(repositorio), cache: cache);

        Assert.Throws<ArgumentException>(() => servicio.Crear(new Servicio()));
        Assert.Throws<InvalidOperationException>(() => servicio.Eliminar(1));
        repositorio.FallarEscritura = true;
        Assert.Throws<InvalidOperationException>(() => servicio.Crear(ServicioValido()));
        Assert.Empty(cache.ClavesEliminadas);
    }

    [Fact]
    public void Automatico_EditarPrecioYNombre_NoInvalidaConteos()
    {
        var repositorio = new ServiciosPrueba();
        repositorio.Add(ServicioValido());
        var cache = new CachePrueba(ResultadoEliminacionCache.Eliminada);
        var servicio = new ServicioService(new CreadorServiciosPrueba(repositorio), cache: cache);
        Servicio existente = repositorio.Registros[0];
        existente.Nombre = "Servicio actualizado";
        existente.Costo = 30;

        servicio.Actualizar(existente);

        Assert.Single(repositorio.Registros);
        Assert.Empty(cache.ClavesEliminadas);
    }

    private static Servicio ServicioValido() => new()
    {
        Nombre = "Prueba Redis", Descripcion = "Prueba aislada", Costo = 20, TiempoEstimadoHoras = 1
    };

    private static TallerMecanico.Pages.IndexModel CrearPagina(CachePrueba cache, string rol)
    {
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, rol)], "Prueba"))
        };
        var repositorio = new ServiciosPrueba();
        var dashboard = new DashboardCacheService(new DashboardService(repositorio,
            new RepositorioSinLectura<Vehiculo>(), new RepositorioSinLectura<Mecanico>()),
            cache, new OpcionesCacheDashboard(), TimeProvider.System, NullLogger<DashboardCacheService>.Instance);
        return new TallerMecanico.Pages.IndexModel(dashboard)
        {
            PageContext = new PageContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TempDataPrueba())
        };
    }

    private sealed class CachePrueba(ResultadoEliminacionCache resultado) : ICacheResumenDashboardPort
    {
        public List<string> ClavesEliminadas { get; } = [];
        public LecturaResumenDashboard ObtenerResumen() => throw new NotSupportedException();
        public EstadoPublicacionResumen PublicarResumen(LecturaResumenDashboard lectura, string valor, TimeSpan expiracion) => throw new NotSupportedException();
        public TimeSpan? TiempoRestanteResumen() => null;
        public ResultadoEliminacionCache InvalidarResumen() { ClavesEliminadas.Add(ClavesCache.DashboardResumen); return resultado; }
    }

    private sealed class TempDataPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class CreadorServiciosPrueba(ServiciosPrueba repositorio) : CreadorServicio(null!)
    {
        public override IRepository<Servicio> CrearRepositorio() => repositorio;
    }

    private sealed class ServiciosPrueba : IRepository<Servicio>, IConsultaServiciosPort
    {
        public List<Servicio> Registros { get; } = [];
        public bool FallarEscritura { get; set; }
        public List<Servicio> GetAll() => Registros;
        public Servicio? GetById(int id) => Registros.SingleOrDefault(servicio => servicio.Id == id);
        public void Add(Servicio servicio)
        {
            if (FallarEscritura) throw new InvalidOperationException("Persistencia no confirmada");
            servicio.Id = 1; Registros.Add(servicio);
        }
        public void Update(Servicio servicio) { }
        public void Delete(int id) => Registros.RemoveAll(servicio => servicio.Id == id);
        public int Count() => Registros.Count;
        public Task<IReadOnlyList<Servicio>> BuscarAsync(string termino, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(string termino, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RepositorioSinLectura<T> : IRepository<T>
    {
        public List<T> GetAll() => throw new NotSupportedException();
        public T? GetById(int id) => throw new NotSupportedException();
        public void Add(T entity) => throw new NotSupportedException();
        public void Update(T entity) => throw new NotSupportedException();
        public void Delete(int id) => throw new NotSupportedException();
        public int Count() => throw new NotSupportedException();
    }
}
