using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Sesion;
using PaginaProductos = TallerMecanico.Pages.Productos.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class ProductosAutocompletadoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    public async Task TerminosCortosYLargos_NoConsultanInventario(string? termino)
    {
        var puerto = new ProductosPrueba();
        var servicio = new ProductoService(puerto, new ValidacionProductos(), new UsuarioPrueba());
        Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Empty(await servicio.SugerirAsync(new string('a', 101)));
        Assert.Equal(0, puerto.Consultas);
        Assert.Equal(0, puerto.Escrituras);
    }

    [Fact]
    public async Task Sugerencias_NormalizanEspaciosYNoInvocanOtrasOperaciones()
    {
        var puerto = new ProductosPrueba();
        var servicio = new ProductoService(puerto, new ValidacionProductos(), new UsuarioPrueba());
        await servicio.SugerirAsync("  fILtro    aCEIte  ");
        Assert.Equal("fILtro aCEIte", puerto.Termino);
        Assert.Equal(1, puerto.Consultas);
        Assert.Equal(0, puerto.Escrituras);
    }

    [Fact]
    public void CrearDesdeOrden_ConservaBorradorYPermisosDeRecepcionista()
    {
        var (pagina, puerto, borradores) = CrearPagina("Recepcionista");
        Guid orden = borradores.Guardar(new OrdenFormViewModel { MecanicoId = 12 });
        pagina.OrdenBorradorId = orden;
        pagina.ProductoInput = Formulario();
        var resultado = Assert.IsType<RedirectToPageResult>(pagina.OnPostCrear());
        Assert.Equal("/Ordenes/Crear", resultado.PageName);
        Assert.Equal(orden, resultado.RouteValues!["BorradorId"]);
        Assert.Equal(puerto.Datos.Single().Id, resultado.RouteValues["ProductoNuevoId"]);
        Assert.Equal(12, borradores.Obtener(orden)!.MecanicoId);

        pagina.OrdenBorradorId = Guid.NewGuid();
        Assert.IsType<ForbidResult>(pagina.OnPostCrear());
        Assert.IsType<ForbidResult>(pagina.OnPostActualizar());
        Assert.IsType<ForbidResult>(pagina.OnPostEliminar());
        Assert.Single(puerto.Datos);
    }

    [Fact]
    public void Administrador_CrudConservaFiltrosYContextoSinCambiarElRetornoEspecial()
    {
        var (pagina, puerto, _) = CrearPagina("Administrador");
        pagina.ProductoInput = Formulario();
        pagina.TerminoBusqueda = "Filtro";
        Assert.IsType<RedirectToPageResult>(pagina.OnPostCrear());
        int id = puerto.Datos.Single().Id;
        pagina.ProductoId = id;
        pagina.ProductoSeleccionadoId = id;
        pagina.OrdenBorradorId = Guid.NewGuid();
        pagina.ProductoInput.Nombre = "Filtro Actualizado";
        foreach (var resultado in new[] { pagina.OnPostActualizar(), pagina.OnPostEliminar() })
        {
            var redireccion = Assert.IsType<RedirectToPageResult>(resultado);
            Assert.Equal(id, redireccion.RouteValues![nameof(pagina.ProductoSeleccionadoId)]);
            Assert.Equal("Filtro", redireccion.RouteValues[nameof(pagina.TerminoBusqueda)]);
            Assert.Equal(pagina.OrdenBorradorId, redireccion.RouteValues[nameof(pagina.OrdenBorradorId)]);
        }
    }

    [MySqlAisladoFact]
    public async Task MySql_PrefijoCodigoNombresCompuestosCajaYTildes()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Crear(servicio, "FIL-001", "Filtro De Aceite");
        Crear(servicio, "FIL-002", "Filtro De Aceite", stock: 0);
        Crear(servicio, "X-FIL-003", "Bujía Iridio");
        Crear(servicio, "OTR-004", "Sensor De Presión De Aceite");
        Assert.Equal(2, (await servicio.SugerirAsync("fil")).Count);
        foreach (string termino in new[] { "FILTRO", "filtro acei", "acei filtro", "filtro   de" })
            Assert.Equal(2, (await servicio.SugerirAsync(termino)).Count);
        Assert.Single(await servicio.SugerirAsync("PRESION"));
        Assert.Single(await servicio.SugerirAsync("bujia iri"));
        Assert.Empty(await servicio.SugerirAsync("iltro"));
        Assert.Empty(await servicio.SugerirAsync("filtro iridio"));
        var coincidencias = await servicio.SugerirAsync("FIL-");
        Assert.Equal(2, coincidencias.Select(sugerencia => sugerencia.Id).Distinct().Count());
        Assert.Equal(2, coincidencias.Select(sugerencia => sugerencia.TextoSecundario).Distinct().Count());
        Assert.All(coincidencias, sugerencia => Assert.Equal("Filtro De Aceite", sugerencia.TextoPrincipal));
        Assert.Equal("FIL-001", servicio.ObtenerPorId(coincidencias[0].Id)!.Codigo);
        Assert.Equal(3, servicio.Buscar("FIL").Count);
        Assert.Equal(2, servicio.Buscar("iltro").Count);
        Assert.Null(servicio.ObtenerPorId(int.MaxValue));
    }

    [MySqlAisladoFact]
    public async Task MySql_LimitaDiezYEscapaComodinesSinEscribirInventario()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        for (int indice = 0; indice < 13; indice++) Crear(servicio, $"CAP-{indice:000}", "Repuesto De Motor");
        foreach (string codigo in new[] { "ESP%001", "ESP_001", "ESP!001", "ESP'001" })
            Crear(servicio, codigo, "Repuesto Especial");
        string antes = JsonSerializer.Serialize(servicio.Obtener());
        var repositorio = new ProductoRepository(aislada.Factory);
        var sugerencias = await repositorio.SugerirAsync("CAP");
        Assert.Equal(10, sugerencias.Count);
        Assert.Equal(sugerencias.ToArray(), (await repositorio.SugerirAsync("CAP")).ToArray());
        foreach (string termino in new[] { "ESP%", "ESP_", "ESP!", "ESP'" })
            Assert.Single(await servicio.SugerirAsync(termino));
        foreach (string termino in new[] { "%%", "__", "CAP%", "CAP_", "' OR 1=1 --", "<script>" })
            Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Equal(antes, JsonSerializer.Serialize(servicio.Obtener()));
        Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
    }

    [MySqlAisladoFact]
    public void MySql_CrudValidacionesYAuditoriaContinuanFuncionando()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Crear(servicio, "FIL-001", "Filtro De Aceite");
        var original = servicio.Obtener().Single();
        var formulario = Formulario();
        Assert.Contains(nameof(formulario.Codigo), servicio.Crear(formulario).Errores.Keys);
        formulario.Id = original.Id;
        formulario.Precio = -1;
        Assert.NotEmpty(servicio.Actualizar(formulario).Errores);
        Assert.Equal(original.Precio, servicio.ObtenerPorId(original.Id)!.Precio);
        formulario.Precio = 30;
        formulario.Nombre = "Filtro De Aceite Premium";
        Assert.Empty(servicio.Actualizar(formulario).Errores);
        Assert.Equal(original.Stock, servicio.ObtenerPorId(original.Id)!.Stock);
        Assert.Equal(original.CreadoPor, servicio.ObtenerPorId(original.Id)!.CreadoPor);
        Assert.Null(servicio.Eliminar(original.Id));
        Assert.Empty(servicio.Obtener());
    }

    internal static ProductoService CrearServicio(MySqlAisladoFixture aislada) => new(
        (IProductoPort)new CreadorProducto(aislada.Factory).CrearRepositorio(),
        new ValidacionProductos(), new UsuarioPrueba());

    internal static ProductoFormViewModel Formulario(string codigo = "FIL-001", string nombre = "Filtro De Aceite", int stock = 10) => new()
    {
        Codigo = codigo, Nombre = nombre, Precio = 25, Stock = stock, StockMinimo = 2
    };

    internal static int Crear(ProductoService servicio, string codigo, string nombre, int stock = 10)
    {
        Assert.Empty(servicio.Crear(Formulario(codigo, nombre, stock)).Errores);
        return servicio.Buscar(codigo).Single(producto => producto.Codigo == codigo).Id;
    }

    private static (PaginaProductos Pagina, ProductosPrueba Puerto, BorradorOrdenSesion Borradores) CrearPagina(string rol)
    {
        var contexto = new DefaultHttpContext
        {
            Session = new SesionPrueba(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, rol)], "prueba"))
        };
        var usuario = new UsuarioPrueba { Rol = rol };
        var borradores = new BorradorOrdenSesion(new HttpContextAccessor { HttpContext = contexto }, usuario);
        var puerto = new ProductosPrueba();
        var servicio = new ProductoService(puerto, new ValidacionProductos(), usuario);
        return (new PaginaProductos(servicio, borradores)
        {
            PageContext = new PageContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TemporalesPrueba())
        }, puerto, borradores);
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class ProductosPrueba : IProductoPort
    {
        public List<Producto> Datos { get; } = [];
        public int Consultas { get; private set; }
        public int Escrituras { get; private set; }
        public string? Termino { get; private set; }
        public IReadOnlyList<Producto> GetAll() { Consultas++; return Datos; }
        public Producto? GetById(int id) { Consultas++; return Datos.Find(producto => producto.Id == id); }
        public IReadOnlyList<Producto> Search(string terminoBusqueda) { Consultas++; return Datos.Where(producto => producto.Codigo.Contains(terminoBusqueda)).ToList(); }
        public bool ExistsByCodigo(string codigo, int idExcluido = 0) { Consultas++; return Datos.Any(producto => producto.Codigo == codigo && producto.Id != idExcluido); }
        public Task<IReadOnlyList<SugerenciaProducto>> SugerirAsync(string termino, CancellationToken cancellationToken = default)
        { Consultas++; Termino = termino; return Task.FromResult<IReadOnlyList<SugerenciaProducto>>([]); }
        public void Add(Producto producto) { Escrituras++; producto.Id = Datos.Count + 1; Datos.Add(producto); }
        public void Update(Producto producto) { Escrituras++; Datos.RemoveAll(actual => actual.Id == producto.Id); Datos.Add(producto); }
        public void Delete(int id) { Escrituras++; Datos.RemoveAll(producto => producto.Id == id); }
        public bool Descontar(int productoId, int cantidad) { Escrituras++; throw new NotSupportedException(); }
        public void Restituir(int productoId, int cantidad) { Escrituras++; throw new NotSupportedException(); }
    }
}
