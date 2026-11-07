using TallerMecanico.Api;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Sesion;
using PaginaVehiculos = TallerMecanico.Pages.Vehiculos.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class VehiculosAutocompletadoTests
{
    [Trait("Area", "Backend")]
    [Fact]
    public async Task RegistrarClienteYRetomar_ConservanSeleccionExactaFormularioYOrden()
    {
        var (pagina, _, vehiculos, clientes, borradores, _) = CrearPagina();
        vehiculos.Add(new VehiculoDto { Id = 7, Placa = "123ABC", ClienteId = 1 });
        vehiculos.Add(new VehiculoDto { Id = 8, Placa = "123DEF", ClienteId = 1 });
        clientes.Add(new ClienteDto { Id = 1, Nombres = "Ana", PrimerApellido = "Arce" });
        pagina.Formulario = Formulario(1);
        pagina.Formulario.Observaciones = "Conservar datos";
        pagina.Formulario.EsPlacaExtranjera = true;
        pagina.Buscar = "123ABC";
        pagina.VehiculoSeleccionadoId = 7;
        pagina.OrdenBorradorId = Guid.NewGuid();
        var redireccion = Assert.IsType<RedirectToPageResult>(pagina.OnPostRegistrarCliente("crear"));
        Assert.Equal("/Clientes/Index", redireccion.PageName);
        Guid id = Assert.IsType<Guid>(redireccion.RouteValues!["BorradorVehiculoId"]);
        Assert.Equal(7, borradores.Obtener(id)!.VehiculoSeleccionadoId);
        Assert.True(borradores.AsignarCliente(id, 1));

        pagina.Formulario = new();
        pagina.Buscar = null;
        pagina.VehiculoSeleccionadoId = null;
        Assert.IsType<PageResult>(await pagina.OnGetRetomarAsync(id));
        Assert.Equal(7, pagina.VehiculoSeleccionadoId);
        Assert.Equal("123ABC", pagina.Buscar);
        Assert.Equal("Conservar datos", pagina.Formulario.Observaciones);
        Assert.True(pagina.Formulario.EsPlacaExtranjera);
        Assert.Equal(1, pagina.Formulario.ClienteId);
        Assert.Equal(7, Assert.Single(pagina.Vehiculos).Id);
        Assert.Equal("crear", pagina.ModalAbierto);
    }

    [Trait("Area", "Backend")]
    [Fact]
    public async Task GuardarDesdeOrden_ConservaRetornoYAsociacionConCliente()
    {
        var (pagina, api, _, clientes, _, ordenes) = CrearPagina();
        clientes.Add(new ClienteDto { Id = 1 });
        Guid orden = ordenes.Guardar(new OrdenFormViewModel { MecanicoId = 12 });
        pagina.OrdenBorradorId = orden;
        pagina.Formulario = Formulario(1);
        var redireccion = Assert.IsType<RedirectToPageResult>(await pagina.OnPostCreateAsync());
        Assert.Equal("/Ordenes/Crear", redireccion.PageName);
        Assert.Equal(orden, redireccion.RouteValues!["BorradorId"]);
        Assert.Equal(9, redireccion.RouteValues["VehiculoNuevoId"]);
        Assert.Equal(1, JsonSerializer.Deserialize<VehiculoFormViewModel>(Assert.Single(api.Cuerpos), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.ClienteId);
        Assert.Equal(12, ordenes.Obtener(orden)!.MecanicoId);
    }

    [Trait("Area", "Backend")]
    [Fact]
    public void BorradoresAnteriores_SinSeleccionSeSiguenLeyendo()
    {
        var anterior = JsonSerializer.Deserialize<BorradorVehiculo>("""
            {"Formulario":{"Placa":"123ABC"},"Buscar":"Toyota","Modal":"crear"}
            """);
        Assert.Null(anterior!.VehiculoSeleccionadoId);
        Assert.Equal("Toyota", anterior.Buscar);
    }

    internal static VehiculoFormViewModel Formulario(int cliente, string placa = "123ABC") => new()
    { Placa = placa, Marca = "Toyota", Modelo = "Corolla", Kilometraje = 1200, ClienteId = cliente };

    private static (PaginaVehiculos Pagina, ApiFalsa Api, List<VehiculoDto> Vehiculos, List<ClienteDto> Clientes, BorradorVehiculoSesion Borradores, BorradorOrdenSesion Ordenes) CrearPagina()
    {
        var contexto = new DefaultHttpContext { Session = new SesionPrueba() };
        var acceso = new HttpContextAccessor { HttpContext = contexto };
        var usuario = new UsuarioPrueba();
        var vehiculos = new List<VehiculoDto>();
        var clientes = new List<ClienteDto>();
        var api = new ApiFalsa();
        api.Json("GET api/clientes", clientes);
        api.Json("GET api/vehiculos", vehiculos);
        api.Json("GET api/vehiculos/7", new VehiculoDto { Id = 7, Placa = "123ABC", ClienteId = 1 });
        api.Json("GET api/vehiculos/catalogo", new Dictionary<string, string[]> { ["Toyota"] = ["Corolla"] });
        var formulario = Formulario(1); formulario.Id = 9;
        api.Json("POST api/vehiculos", new ResultadoFormulario<VehiculoFormViewModel>(formulario, new Dictionary<string, string>()));
        var borradores = new BorradorVehiculoSesion(acceso, usuario);
        var pagina = new PaginaVehiculos(new VehiculosApi(api.Cliente()), new ClientesApi(api.Cliente()), borradores)
        {
            PageContext = new PageContext { HttpContext = contexto }, TempData = new TempDataDictionary(contexto, new TemporalesPrueba())
        };
        return (pagina, api, vehiculos, clientes, borradores, new BorradorOrdenSesion(acceso, usuario));
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
