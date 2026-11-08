using TallerMecanico.Api;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Sesion;
using TallerMecanico.Pages.Ordenes;
using PaginaClientes = TallerMecanico.Pages.Clientes.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class ClientesAutocompletadoTests
{
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

    internal static ClienteFormViewModel Formulario(string ci = "7812345", string complemento = "") => new()
    {
        Ci = ci, ComplementoCi = complemento, Nombres = "María Fernanda", PrimerApellido = "Pérez",
        SegundoApellido = "De La Cruz", Celular = "71234567"
    };

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
