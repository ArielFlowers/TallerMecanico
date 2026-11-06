using TallerMecanico.Api;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Sesion;
using PaginaClientes = TallerMecanico.Pages.Clientes.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class ClientesAutocompletadoTests
{
    [Trait("Area", "Backend")]
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a")]
    public async Task TerminosCortosYLargos_NoConsultanElPuerto(string? termino)
    {
        var puerto = new ClientesPrueba();
        var servicio = new ClienteService(puerto, new VehiculosPrueba(), new ValidacionClientes(), new UsuarioPrueba());
        Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Empty(await servicio.SugerirAsync(new string('a', 101)));
        Assert.Null(puerto.UltimoTerminoSugerencias);
    }

    [Trait("Area", "Backend")]
    [Fact]
    public async Task Servicio_NormalizaSolamenteLosEspaciosDelTermino()
    {
        var puerto = new ClientesPrueba();
        var servicio = new ClienteService(puerto, new VehiculosPrueba(), new ValidacionClientes(), new UsuarioPrueba());
        await servicio.SugerirAsync("  mARía   péREZ  ");
        Assert.Equal("mARía péREZ", puerto.UltimoTerminoSugerencias);
    }

    [Trait("Area", "Backend")]
    [Fact]
    public async Task CrearClienteDesdeOrden_ConservaElRetornoYElIdGenerado()
    {
        var (pagina, clientes, borradoresVehiculo, borradoresOrden) = CrearPagina();
        Guid orden = borradoresOrden.Guardar(new OrdenFormViewModel { MecanicoId = 12 });
        pagina.OrdenBorradorId = orden;
        pagina.ClienteInput = Formulario();
        var resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostCrearAsync());
        Assert.Equal("/Ordenes/Crear", resultado.PageName);
        Assert.Equal(orden, resultado.RouteValues!["BorradorId"]);
        Assert.Equal(7, resultado.RouteValues["ClienteNuevoId"]);
        Assert.Equal(12, borradoresOrden.Obtener(orden)!.MecanicoId);
    }

    [Trait("Area", "Backend")]
    [Fact]
    public async Task CrearClienteDesdeVehiculo_PriorizaRetornoExistenteInclusoConFiltros()
    {
        var (pagina, clientes, vehiculos, ordenes) = CrearPagina();
        Guid orden = ordenes.Guardar(new OrdenFormViewModel());
        Guid vehiculo = vehiculos.Guardar(new VehiculoFormViewModel { Placa = "123ABC" }, "crear", "ABC", orden);
        pagina.BorradorVehiculoId = vehiculo;
        pagina.OrdenBorradorId = orden;
        pagina.ClienteSeleccionadoId = 10;
        pagina.TerminoBusqueda = "María";
        pagina.ClienteInput = Formulario();
        var resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostCrearAsync());
        Assert.Equal("/Vehiculos/Index", resultado.PageName);
        Assert.Equal("Retomar", resultado.PageHandler);
        Assert.Equal(vehiculo, resultado.RouteValues!["borradorId"]);
        Assert.Equal(7, vehiculos.Obtener(vehiculo)!.Formulario.ClienteId);
        Assert.Equal(orden, vehiculos.Obtener(vehiculo)!.OrdenBorradorId);
    }

    [Trait("Area", "Backend")]
    [Fact]
    public async Task ActualizarYEliminar_ConservanFiltrosYContextos()
    {
        var (pagina, clientes, _, _) = CrearPagina();
        var creado = Formulario(); creado.Id = 7;
        pagina.ClienteInput = creado;
        pagina.ClienteId = creado.Id;
        pagina.ClienteSeleccionadoId = creado.Id;
        pagina.TerminoBusqueda = "María Pérez";
        pagina.BorradorVehiculoId = Guid.NewGuid();
        pagina.OrdenBorradorId = Guid.NewGuid();
        foreach (var resultado in new[] { await pagina.OnPostActualizarAsync(), await pagina.OnPostEliminarAsync() })
        {
            var redireccion = Assert.IsType<RedirectToPageResult>(resultado);
            Assert.Equal(pagina.TerminoBusqueda, redireccion.RouteValues![nameof(pagina.TerminoBusqueda)]);
            Assert.Equal(pagina.ClienteSeleccionadoId, redireccion.RouteValues[nameof(pagina.ClienteSeleccionadoId)]);
            Assert.Equal(pagina.BorradorVehiculoId, redireccion.RouteValues[nameof(pagina.BorradorVehiculoId)]);
            Assert.Equal(pagina.OrdenBorradorId, redireccion.RouteValues[nameof(pagina.OrdenBorradorId)]);
        }
    }

    [Trait("Area", "Datos")]
    [MySqlAisladoFact]
    public async Task MySql_PrefijoPalabrasCompuestasTildesYComplementos()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Crear(servicio, Formulario("7812345", "1A"));
        Crear(servicio, Formulario("7812345", "2B"));
        var otro = Formulario("1278345");
        otro.Nombres = "Juan Carlos";
        otro.PrimerApellido = "Muñoz";
        otro.SegundoApellido = "Mercado";
        Crear(servicio, otro);

        Assert.Equal(2, (await servicio.SugerirAsync("78")).Count);
        foreach (string termino in new[] { "MARIA", "fer", "perez", "maria per", "de la cru", "maria cruz" })
            Assert.Equal(2, (await servicio.SugerirAsync(termino)).Count);
        Assert.Empty(await servicio.SugerirAsync("aria"));
        Assert.Empty(await servicio.SugerirAsync("2345"));
        Assert.Single(await servicio.SugerirAsync("carlos munoz"));
        var porComplemento = Assert.Single(await servicio.SugerirAsync("7812345-1a"));
        Assert.Equal("CI: 7812345-1A", porComplemento.TextoSecundario);
        Assert.NotEqual(porComplemento.Id, Assert.Single(await servicio.SugerirAsync("7812345-2B")).Id);
        Assert.Equal(porComplemento.Id, servicio.ObtenerPorId(porComplemento.Id)!.Id);
    }

    [Trait("Area", "Datos")]
    [MySqlAisladoFact]
    public async Task MySql_LimitaDiezEscapaEntradasYNoEscribe()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        for (int indice = 0; indice < 13; indice++) Crear(servicio, Formulario($"78{indice:00000}"));
        string antes = JsonSerializer.Serialize(servicio.Obtener());
        var repositorio = new ClienteRepository(aislada.Factory);
        var resultados = await repositorio.SugerirAsync("78");
        Assert.Equal(10, resultados.Count);
        Assert.Equal(10, resultados.Select(sugerencia => sugerencia.Id).Distinct().Count());
        Assert.Equal(resultados.ToArray(), (await repositorio.SugerirAsync("78")).ToArray());
        foreach (string termino in new[] { "%%", "__", "!!", "78%", "78_", "' OR 1=1 --", "<script>" })
            Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Equal(antes, JsonSerializer.Serialize(servicio.Obtener()));
    }

    [Trait("Area", "Datos")]
    [MySqlAisladoFact]
    public async Task MySql_BusquedaManualYApellidoVacioMantienenComportamientoAnterior()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Crear(servicio, Formulario("7812345", "1A"));
        Crear(servicio, Formulario("1278345", "2B"));
        Assert.Equal(2, servicio.Buscar("78").Count);
        Assert.Equal(2, servicio.Buscar("71234567").Count);
        Assert.Equal(2, servicio.Buscar("Fernanda Pérez").Count);
        Assert.Single(servicio.Buscar("2B"));
        var repositorio = new ClienteRepository(aislada.Factory);
        repositorio.Add(new TallerMecanico.Dominio.Entidades.Cliente
        {
            Ci = "5566778", Nombres = "Juan Carlos", PrimerApellido = "Muñoz", SegundoApellido = "",
            Celular = "71234567", CreadoPor = "pruebas", FechaCreacion = DateTime.Now
        });
        Assert.Equal("Juan Carlos Muñoz", Assert.Single(await servicio.SugerirAsync("juan munoz")).TextoPrincipal);
        Assert.Equal(3, servicio.Obtener().Count);
        Assert.Null(servicio.ObtenerPorId(int.MaxValue));
    }

    internal static ClienteService CrearServicio(MySqlAisladoFixture aislada) => new(
        (TallerMecanico.Aplicacion.Puertos.IClientePort)new CreadorCliente(aislada.Factory).CrearRepositorio(),
        new VehiculoRepository(aislada.Factory), new ValidacionClientes(), new UsuarioPrueba());

    internal static ClienteFormViewModel Formulario(string ci = "7812345", string complemento = "") => new()
    {
        Ci = ci, ComplementoCi = complemento, Nombres = "María Fernanda", PrimerApellido = "Pérez",
        SegundoApellido = "De La Cruz", Celular = "71234567"
    };

    internal static int Crear(ClienteService servicio, ClienteFormViewModel formulario)
    {
        var resultado = servicio.Crear(formulario);
        Assert.Empty(resultado.Errores);
        return resultado.Formulario.Id;
    }

    private static (PaginaClientes Pagina, ApiFalsa Api, BorradorVehiculoSesion Vehiculos, BorradorOrdenSesion Ordenes) CrearPagina()
    {
        var contexto = new DefaultHttpContext { Session = new SesionPrueba() };
        var acceso = new HttpContextAccessor { HttpContext = contexto };
        var usuario = new UsuarioPrueba();
        var vehiculos = new BorradorVehiculoSesion(acceso, usuario);
        var ordenes = new BorradorOrdenSesion(acceso, usuario);
        var clientes = new ApiFalsa();
        var formulario = Formulario(); formulario.Id = 7;
        clientes.Json("GET api/clientes", new[] { new ClienteDto { Id = 7, Nombres = "María", PrimerApellido = "Pérez" } });
        clientes.Json("POST api/clientes", new ResultadoFormulario<ClienteFormViewModel>(formulario, new Dictionary<string, string>()));
        clientes.Json("PUT api/clientes/7", new ResultadoFormulario<ClienteFormViewModel>(formulario, new Dictionary<string, string>()));
        clientes.Json("DELETE api/clientes/7", new { });
        var pagina = new PaginaClientes(new ClientesApi(clientes.Cliente()), vehiculos, ordenes)
        {
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TemporalesPrueba())
        };
        return (pagina, clientes, vehiculos, ordenes);
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
