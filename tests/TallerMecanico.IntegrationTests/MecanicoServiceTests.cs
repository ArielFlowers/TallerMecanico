using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using Microsoft.Extensions.DependencyInjection;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class MecanicoServiceTests
{
    [Fact]
    public void AltaDesdeOrden_AbreElFormularioYVuelveConElBorrador()
    {
        var repositorio = new RepositorioPrueba();
        var borradores = new BorradoresPrueba();
        Guid orden = borradores.Guardar(new OrdenFormViewModel());
        var servicio = new MecanicoService(repositorio, repositorio, new ValidacionMecanicos(), new UsuarioPrueba());
        var pagina = new TallerMecanico.Pages.Mecanicos.IndexModel(servicio, borradores)
        {
            OrdenBorradorId = orden,
            MecanicoInput = MecanicosTests.FormularioValido(),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TemporalesPrueba())
        };

        pagina.OnGet();
        Assert.Equal("crear", pagina.FormularioActivo);

        var redireccion = Assert.IsType<RedirectToPageResult>(pagina.OnPostCrear());
        Assert.Equal("/Ordenes/Crear", redireccion.PageName);
        Assert.Equal(orden, redireccion.RouteValues!["BorradorId"]);
        Assert.Single(repositorio.Registros);
        Assert.NotNull(borradores.Obtener(orden));
    }

    private sealed class BorradoresPrueba : IBorradorOrden
    {
        private readonly Dictionary<Guid, OrdenFormViewModel> _borradores = [];

        public Guid Guardar(OrdenFormViewModel formulario)
        {
            Guid id = Guid.NewGuid();
            _borradores[id] = formulario;
            return id;
        }

        public OrdenFormViewModel? Obtener(Guid id) => _borradores.GetValueOrDefault(id);

        public void Eliminar(Guid id) => _borradores.Remove(id);
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    [Fact]
    public void Contratos_ConservanValidacionDuplicadosAuditoriaYCache()
    {
        var repositorio = new RepositorioPrueba();
        var cache = new CachePrueba();
        var servicio = new MecanicoService(repositorio, repositorio, new ValidacionMecanicos(), new UsuarioPrueba(), cache);
        var invalido = MecanicosTests.FormularioValido("1");
        Assert.NotEmpty(servicio.Crear(invalido));
        Assert.Empty(repositorio.Registros);
        Assert.Equal(0, cache.Invalidaciones);

        var entrada = MecanicosTests.FormularioValido();
        Assert.Empty(servicio.Crear(entrada));
        Assert.Equal(1, cache.Invalidaciones);
        var creado = Assert.Single(servicio.Obtener());
        Assert.False(string.IsNullOrWhiteSpace(creado.CreadoPor));
        Assert.Single(servicio.Crear(entrada));
        Assert.Equal(1, cache.Invalidaciones);
        entrada.Celular = "71234568";
        var actualizado = servicio.Actualizar(creado.Id, entrada);
        Assert.True(actualizado.Actualizado);
        Assert.Empty(actualizado.Errores);
        var editado = Assert.Single(servicio.Obtener());
        Assert.Equal(creado.CreadoPor, editado.CreadoPor);
        Assert.Equal(creado.FechaCreacion, editado.FechaCreacion);
        Assert.NotNull(editado.FechaModificacion);
        Assert.Equal(1, cache.Invalidaciones);
        Assert.Null(servicio.Eliminar(editado.Id));
        Assert.Empty(repositorio.Registros);
        Assert.Equal(2, cache.Invalidaciones);
        Assert.Equal("El mecánico seleccionado ya no existe.", servicio.Eliminar(editado.Id));
        Assert.Equal(2, cache.Invalidaciones);
    }

    [Fact]
    public void Eliminar_RelacionadoConservaMensajeYNoInvalidaCache()
    {
        var repositorio = new RepositorioPrueba();
        var cache = new CachePrueba();
        var servicio = new MecanicoService(repositorio, repositorio, new ValidacionMecanicos(), cache: cache);
        repositorio.Add(new Mecanico { Ci = "1234567" });
        repositorio.ErrorEliminar = new RegistroRelacionadoException(new InvalidOperationException("Conflicto de prueba"));

        Assert.Equal("No se puede eliminar el mecánico porque tiene órdenes asociadas.", servicio.Eliminar(1));
        Assert.Single(repositorio.Registros);
        Assert.Equal(0, cache.Invalidaciones);
        var otroError = new InvalidOperationException("Fallo no relacionado");
        repositorio.ErrorEliminar = otroError;
        Assert.Same(otroError, Assert.Throws<InvalidOperationException>(() => servicio.Eliminar(1)));
        Assert.Equal(0, cache.Invalidaciones);
    }

    [Fact]
    public async Task Consultas_UtilizanElContratoInyectadoSinConversiones()
    {
        var repositorio = new RepositorioPrueba();
        var consultas = new RepositorioPrueba();
        var servicio = new MecanicoService(repositorio, consultas, new ValidacionMecanicos());
        consultas.Add(new Mecanico { Nombres = "Juan", Ci = "1234567" });

        Assert.Empty(servicio.Obtener());
        Assert.Single(servicio.Obtener("Juan"));
        await servicio.SugerirAsync("  Juan   Pérez  ", incluirEspecialidad: false);
        Assert.Equal("Juan Pérez", consultas.TerminoSugerencia);
        Assert.False(consultas.IncluirEspecialidad);
        Assert.Null(repositorio.TerminoSugerencia);
        Assert.Single(servicio.Crear(MecanicosTests.FormularioValido()));
        Assert.Empty(repositorio.Registros);
    }

    [Fact]
    public void FactoryMethod_DiComparteAmbosContratosPorScope()
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:MySqlConnection"] = "Server=127.0.0.1;Port=1;Database=no_utilizada;" }).Build());
        servicios.AddScoped<DatabaseConnectionFactory, MySqlConnectionFactory>();
        servicios.AddScoped<CreadorMecanico>();
        int creaciones = 0;
        servicios.AddScoped<IRepository<Mecanico>>(provider =>
        {
            creaciones++;
            return provider.GetRequiredService<CreadorMecanico>().CrearRepositorio();
        });
        servicios.AddScoped<IConsultaMecanicosPort>(provider =>
            (IConsultaMecanicosPort)provider.GetRequiredService<IRepository<Mecanico>>());
        servicios.AddScoped<ValidacionMecanicos>();
        servicios.AddScoped<MecanicoService>();
        using var provider = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        using var otroScope = provider.CreateScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<IRepository<Mecanico>>();
        Assert.IsType<MecanicoRepository>(repositorio);
        Assert.Same(repositorio, scope.ServiceProvider.GetRequiredService<IConsultaMecanicosPort>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<MecanicoService>(), scope.ServiceProvider.GetRequiredService<MecanicoService>());
        Assert.Equal(1, creaciones);
        Assert.NotSame(repositorio, otroScope.ServiceProvider.GetRequiredService<IRepository<Mecanico>>());
        Assert.Equal(2, creaciones);
    }

    private sealed class RepositorioPrueba : IRepository<Mecanico>, IConsultaMecanicosPort
    {
        public List<Mecanico> Registros { get; } = [];
        public Exception? ErrorEliminar { get; set; }
        public string? TerminoSugerencia { get; private set; }
        public bool IncluirEspecialidad { get; private set; }
        public List<Mecanico> GetAll() => Registros;
        public Mecanico? GetById(int id) => Registros.SingleOrDefault(mecanico => mecanico.Id == id);
        public void Add(Mecanico mecanico) { mecanico.Id = Registros.Count + 1; Registros.Add(mecanico); }
        public void Update(Mecanico mecanico) { Registros.RemoveAll(existente => existente.Id == mecanico.Id); Registros.Add(mecanico); }
        public void Delete(int id) { if (ErrorEliminar is not null) throw ErrorEliminar; Registros.RemoveAll(mecanico => mecanico.Id == id); }
        public int Count() => Registros.Count;
        public List<Mecanico> Search(string terminoBusqueda) => Registros.Where(mecanico => mecanico.Nombres.Contains(terminoBusqueda)).ToList();
        public bool ExistsByCi(string ci, string complementoCi, int idExcluido = 0) =>
            Registros.Any(mecanico => mecanico.Ci == ci && mecanico.ComplementoCi == complementoCi && mecanico.Id != idExcluido);
        public Task<IReadOnlyList<SugerenciaMecanico>> SugerirAsync(string termino, CancellationToken cancellationToken = default, bool incluirEspecialidad = true)
        {
            TerminoSugerencia = termino;
            IncluirEspecialidad = incluirEspecialidad;
            return Task.FromResult<IReadOnlyList<SugerenciaMecanico>>([]);
        }
    }

    private sealed class CachePrueba : ICacheResumenDashboardPort
    {
        public int Invalidaciones { get; private set; }
        public ResultadoEliminacionCache InvalidarResumen() { Invalidaciones++; return ResultadoEliminacionCache.Eliminada; }
        public LecturaResumenDashboard ObtenerResumen() => throw new NotSupportedException();
        public EstadoPublicacionResumen PublicarResumen(LecturaResumenDashboard lectura, string valor, TimeSpan expiracion) => throw new NotSupportedException();
        public TimeSpan? TiempoRestanteResumen() => null;
    }
}
