using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using PaginaServicios = TallerMecanico.Pages.Servicios.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class ServiciosAutocompletadoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a")]
    public async Task TerminosCortosYLargos_NoConsultanMySql(string? termino)
    {
        var repositorio = new RepositorioPrueba();
        var servicio = new ServicioService(new CreadorPrueba(repositorio));
        Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Empty(await servicio.SugerirAsync(new string('a', 101)));
        Assert.Null(repositorio.TerminoSugerencias);
    }

    [Fact]
    public async Task Servicio_NormalizaSugerenciasPeroConservaEspaciosInternosDeBusquedaManual()
    {
        var repositorio = new RepositorioPrueba();
        var servicio = new ServicioService(new CreadorPrueba(repositorio));
        using var cancelacion = new CancellationTokenSource();
        await servicio.SugerirAsync("  caMBio   acei  ", cancelacion.Token);
        Assert.Equal("caMBio acei", repositorio.TerminoSugerencias);
        Assert.Equal(cancelacion.Token, repositorio.Cancelacion);
        await servicio.BuscarAsync("  cambio   aceite  ", cancelacion.Token);
        Assert.Equal("cambio   aceite", repositorio.TerminoBusqueda);
        Assert.Equal(cancelacion.Token, repositorio.Cancelacion);
    }

    [Fact]
    public async Task Pagina_UsaConsultaSqlSeleccionExactaYListadoCompletoSinFiltrarEnMemoria()
    {
        var repositorio = new RepositorioPrueba();
        var pagina = new PaginaServicios(new ServicioService(new CreadorPrueba(repositorio)))
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };
        await pagina.OnGetAsync("parcial");
        Assert.Equal("parcial", repositorio.TerminoBusqueda);
        Assert.Equal(0, repositorio.LecturasCompletas);
        Assert.Single(pagina.Servicios);
        pagina.ServicioSeleccionadoId = 8;
        await pagina.OnGetAsync("texto no utilizado");
        Assert.Equal(8, Assert.Single(pagina.Servicios).Id);
        pagina.ServicioSeleccionadoId = -1;
        await pagina.OnGetAsync(null);
        Assert.Empty(pagina.Servicios);
        pagina.ServicioSeleccionadoId = null;
        await pagina.OnGetAsync("  ");
        Assert.Equal(2, pagina.Servicios.Count);
        Assert.Equal(1, repositorio.LecturasCompletas);
        var resultado = await pagina.OnGetSugerenciasAsync("acei");
        Assert.NotNull(resultado.Value);
        Assert.Equal("no-store", pagina.Response.Headers.CacheControl);
    }

    [MySqlAisladoFact]
    public async Task MySql_NombreDescripcionPalabrasCompuestasTildesYBusquedaParcial()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        int aceite = Crear(servicio, "Cambio de Aceite", "Lubricación del motor");
        int electrico = Crear(servicio, "Diagnóstico Electrónico", "Revisión de sensores eléctricos");
        Crear(servicio, "Alineación", "Ajuste del tren delantero");
        Assert.Equal(aceite, Assert.Single(await servicio.SugerirAsync("acei")).Id);
        Assert.Equal(aceite, Assert.Single(await servicio.SugerirAsync("LUBRICA")).Id);
        Assert.Equal(aceite, Assert.Single(await servicio.SugerirAsync("cam mot")).Id);
        Assert.Equal(electrico, Assert.Single(await servicio.SugerirAsync("diagnostico elec")).Id);
        Assert.Equal(electrico, Assert.Single(await servicio.SugerirAsync("revision sens")).Id);
        Assert.Empty(await servicio.SugerirAsync("gnostico"));
        Assert.Empty(await servicio.SugerirAsync("ensores"));
        Assert.Single(await servicio.BuscarAsync("gnostico"));
        Assert.Single(await servicio.BuscarAsync("ensores"));
        Assert.Single(await servicio.BuscarAsync("DIAGNOSTICO"));
        Assert.Single(await servicio.BuscarAsync("del tren"));
        Assert.Equal(3, (await servicio.BuscarAsync(null)).Count);
        Assert.Empty(await servicio.BuscarAsync("noexiste"));
    }

    [MySqlAisladoFact]
    public async Task MySql_LimiteSoloEnSugerenciasOrdenEstableDescripcionBreveYLiteralesEscapados()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        for (int indice = 0; indice < 13; indice++) Crear(servicio, "Diagnóstico General", new string('x', 120));
        var repositorio = new ServicioRepository(aislada.Factory);
        var sugerencias = await repositorio.SugerirAsync("diag");
        Assert.Equal(10, sugerencias.Count);
        Assert.Equal(sugerencias.OrderBy(sugerencia => sugerencia.Id).ToArray(), sugerencias.ToArray());
        Assert.All(sugerencias, sugerencia => Assert.Equal(new string('x', 100) + "…", sugerencia.TextoSecundario));
        Assert.Equal(13, (await servicio.BuscarAsync("gnóstico")).Count);
        Assert.Equal(13, servicio.ObtenerTodos().Count);
        foreach (string termino in new[] { "%%", "__", "!!", "' OR 1=1 --", "<script>" })
        {
            Assert.Empty(await servicio.SugerirAsync(termino));
            Assert.Empty(await servicio.BuscarAsync(termino));
        }
        int literal = Crear(servicio, "Especial", "Descuento 50% con clave A_B! disponible");
        Assert.Equal(literal, Assert.Single(await servicio.BuscarAsync("50%")).Id);
        Assert.Equal(literal, Assert.Single(await servicio.SugerirAsync("50%")).Id);
        Assert.Equal(literal, Assert.Single(await servicio.BuscarAsync("A_B!")).Id);
        Assert.Equal(literal, Assert.Single(await servicio.SugerirAsync("A_B!")).Id);
        Assert.Empty(await repositorio.SugerirAsync(" "));
    }

    [MySqlAisladoFact]
    public async Task MySql_FactoryCrudAuditoriaYLecturasSinEscrituras()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        int id = Crear(servicio, "Cambio de Aceite", "Lubricación del motor");
        Assert.IsAssignableFrom<IConsultaServiciosPort>(new CreadorServicio(aislada.Factory).CrearRepositorio());
        string antes = JsonSerializer.Serialize(servicio.ObtenerTodos());
        await servicio.SugerirAsync("acei");
        await servicio.BuscarAsync("mbi");
        servicio.ObtenerPorId(id);
        Assert.Equal(antes, JsonSerializer.Serialize(servicio.ObtenerTodos()));
        Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        var existente = servicio.ObtenerPorId(id)!;
        DateTime creacion = existente.FechaCreacion;
        existente.Descripcion = "Lubricación y revisión";
        servicio.Actualizar(existente);
        Assert.Equal(creacion, servicio.ObtenerPorId(id)!.FechaCreacion);
        Assert.Equal("recepcion_prueba", servicio.ObtenerPorId(id)!.ModificadoPor);
        servicio.Eliminar(id);
        Assert.Null(servicio.ObtenerPorId(id));
        Assert.Throws<ArgumentException>(() => servicio.Crear(new Servicio()));
        Assert.Throws<InvalidOperationException>(() => servicio.Eliminar(id));
    }

    internal static ServicioService CrearServicio(MySqlAisladoFixture aislada) =>
        new(new CreadorServicio(aislada.Factory), new UsuarioPrueba());

    internal static int Crear(ServicioService servicio, string nombre, string descripcion)
    {
        servicio.Crear(new Servicio { Nombre = nombre, Descripcion = descripcion, Costo = 25, TiempoEstimadoHoras = 1 });
        return servicio.ObtenerTodos().Last(registro => registro.Nombre == nombre).Id;
    }

    private sealed class CreadorPrueba(RepositorioPrueba repositorio) : CreadorServicio(null!)
    {
        public override IRepository<Servicio> CrearRepositorio() => repositorio;
    }

    private sealed class RepositorioPrueba : IRepository<Servicio>, IConsultaServiciosPort
    {
        public List<Servicio> ObtenerCatalogo() => [];
        public Servicio? ObtenerDelCatalogo(int id) => null;
        public Task<IReadOnlyList<SugerenciaServicio>> SugerirCatalogoAsync(string termino, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SugerenciaServicio>>([]);
        public string? TerminoSugerencias { get; private set; }
        public string? TerminoBusqueda { get; private set; }
        public CancellationToken Cancelacion { get; private set; }
        public int LecturasCompletas { get; private set; }
        public List<Servicio> GetAll() { LecturasCompletas++; return [new() { Id = 7 }, new() { Id = 8 }]; }
        public Servicio? GetById(int id) => id == 8 ? new() { Id = 8 } : null;
        public void Add(Servicio entity) => throw new NotSupportedException();
        public void Update(Servicio entity) => throw new NotSupportedException();
        public void Delete(int id) => throw new NotSupportedException();
        public int Count() => 2;
        public Task<IReadOnlyList<Servicio>> BuscarAsync(string termino, CancellationToken cancellationToken = default)
        {
            TerminoBusqueda = termino; Cancelacion = cancellationToken;
            return Task.FromResult<IReadOnlyList<Servicio>>([new() { Id = 7 }]);
        }
        public Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(string termino, CancellationToken cancellationToken = default)
        {
            TerminoSugerencias = termino; Cancelacion = cancellationToken;
            return Task.FromResult<IReadOnlyList<SugerenciaServicio>>([]);
        }
    }
}
