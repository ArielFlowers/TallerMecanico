using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Api;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Infraestructura.Sesion;
using ClientesPage = TallerMecanico.Pages.Clientes.IndexModel;
using VehiculosPage = TallerMecanico.Pages.Vehiculos.IndexModel;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class CatalogosHttpTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClientesHttp_ReutilizanRutasDtoYMetodosCrud(bool clientes)
    {
        var api = PrepararApi();
        if (clientes)
        {
            var cliente = new ClientesApi(api.Cliente());
            Assert.Single(await cliente.ObtenerAsync("Ana Prueba"));
            Assert.Equal(7, (await cliente.ObtenerPorIdAsync(7))!.Id);
            Assert.Empty(await cliente.SugerirAsync("Ana & Pérez"));
            Assert.Empty((await cliente.CrearAsync(ClienteFormulario())).Errores);
            Assert.Empty((await cliente.ActualizarAsync(ClienteFormulario())).Errores);
            Assert.True((await cliente.EliminarAsync(7)).Exito);
        }
        else
        {
            var vehiculo = new VehiculosApi(api.Cliente());
            Assert.Single(await vehiculo.ObtenerAsync("Toyota Corolla"));
            Assert.Equal(8, (await vehiculo.ObtenerPorIdAsync(8))!.Id);
            Assert.Empty(await vehiculo.SugerirAsync("123 & ABC"));
            Assert.Empty((await vehiculo.CrearAsync(VehiculoFormulario())).Errores);
            Assert.Empty((await vehiculo.ActualizarAsync(VehiculoFormulario())).Errores);
            Assert.True((await vehiculo.EliminarAsync(8)).Exito);
            Assert.Equal(2, (await vehiculo.ObtenerCatalogoAsync()).Count);
        }
        string ruta = clientes ? "api/clientes" : "api/vehiculos";
        Assert.Contains(api.Llamadas, llamada => llamada.StartsWith($"GET {ruta}?terminoBusqueda="));
        Assert.Contains(api.Llamadas, llamada => llamada.Contains("%26"));
        Assert.Contains($"POST {ruta}", api.Llamadas);
        Assert.Contains($"PUT {ruta}/{(clientes ? 7 : 8)}", api.Llamadas);
        Assert.Contains($"DELETE {ruta}/{(clientes ? 7 : 8)}", api.Llamadas);
        Assert.Equal(2, api.Cuerpos.Count);
        using var cuerpo = JsonDocument.Parse(api.Cuerpos[0]);
        Assert.Equal(clientes ? 7 : 8, cuerpo.RootElement.GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Listado_BusquedaYSeleccionExactaUtilizanHttp(bool clientes)
    {
        var api = PrepararApi();
        if (clientes)
        {
            var pagina = CrearClientes(api);
            pagina.TerminoBusqueda = "  Ana  ";
            Assert.IsType<PageResult>(await pagina.OnGetAsync());
            Assert.Single(pagina.Clientes);
            Assert.Contains("GET api/clientes?terminoBusqueda=Ana", api.Llamadas);
            pagina.ClienteSeleccionadoId = 7;
            await pagina.OnGetAsync();
            Assert.Equal(7, Assert.Single(pagina.Clientes).Id);
            Assert.Contains("GET api/clientes/7", api.Llamadas);
            Assert.IsType<JsonResult>(await pagina.OnGetSugerenciasAsync("Ana"));
            Assert.Equal("no-store", pagina.Response.Headers.CacheControl);
        }
        else
        {
            var pagina = CrearVehiculos(api);
            pagina.Buscar = " Toyota ";
            Assert.IsType<PageResult>(await pagina.OnGetAsync());
            Assert.Contains("GET api/vehiculos?terminoBusqueda=Toyota", api.Llamadas);
            Assert.Equal(new[] { "Chevrolet", "Toyota" }, pagina.MarcasOrdenadas);
            Assert.Equal(new[] { "Corolla", "Yaris" }, pagina.ObtenerModelosOrdenados("Toyota"));
            Assert.Contains("GET api/vehiculos/catalogo", api.Llamadas);
            pagina.VehiculoSeleccionadoId = 8;
            await pagina.OnGetBuscarAsync();
            Assert.Equal(8, Assert.Single(pagina.Vehiculos).Id);
            Assert.IsType<JsonResult>(await pagina.OnGetSugerenciasAsync("123"));
            Assert.Equal("no-store", pagina.Response.Headers.CacheControl);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Guardar_ErroresPorCampoConservanFormularioYModal(bool clientes, bool actualizar)
    {
        var api = PrepararApi();
        string metodo = actualizar ? "PUT" : "POST";
        if (clientes)
        {
            var normalizado = ClienteFormulario(); normalizado.Nombres = "Ana Prueba";
            api.Json($"{metodo} api/clientes" + (actualizar ? "/7" : ""),
                new ResultadoFormulario<ClienteFormViewModel>(normalizado, new Dictionary<string, string> { ["Ci"] = "CI repetido." }));
            var pagina = CrearClientes(api); pagina.ClienteInput = ClienteFormulario(); pagina.ClienteId = 7;
            Assert.IsType<PageResult>(actualizar ? await pagina.OnPostActualizarAsync() : await pagina.OnPostCrearAsync());
            Assert.Equal("CI repetido.", Assert.Single(pagina.ModelState["ClienteInput.Ci"]!.Errors).ErrorMessage);
            Assert.Equal("Ana Prueba", pagina.ClienteInput.Nombres);
            Assert.Equal(actualizar ? "editar" : "crear", pagina.FormularioActivo);
        }
        else
        {
            api.Json($"{metodo} api/vehiculos" + (actualizar ? "/8" : ""),
                new ResultadoFormulario<VehiculoFormViewModel>(VehiculoFormulario(), new Dictionary<string, string> { ["Placa"] = "Placa repetida." }));
            var pagina = CrearVehiculos(api); pagina.Formulario = VehiculoFormulario();
            Assert.IsType<PageResult>(actualizar ? await pagina.OnPostUpdateAsync() : await pagina.OnPostCreateAsync());
            Assert.Equal("Placa repetida.", Assert.Single(pagina.ModelState["Formulario.Placa"]!.Errors).ErrorMessage);
            Assert.Equal("123ABC", pagina.Formulario.Placa);
            Assert.Equal(actualizar ? "editar" : "crear", pagina.ModalAbierto);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Guardar_Error400SeMuestraSinPerderLaEntrada(bool clientes)
    {
        var api = PrepararApi();
        api.Error("POST api/" + (clientes ? "clientes" : "vehiculos"), HttpStatusCode.BadRequest, "Revisa los datos.");
        PageModel pagina;
        if (clientes) { var cliente = CrearClientes(api); cliente.ClienteInput = ClienteFormulario(); pagina = cliente; Assert.IsType<PageResult>(await cliente.OnPostCrearAsync()); }
        else { var vehiculo = CrearVehiculos(api); vehiculo.Formulario = VehiculoFormulario(); pagina = vehiculo; Assert.IsType<PageResult>(await vehiculo.OnPostCreateAsync()); }
        Assert.Equal("Revisa los datos.", Assert.Single(pagina.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ErroresDeBinding_NoEnvianLaEscritura(bool clientes)
    {
        var api = PrepararApi();
        if (clientes)
        {
            var pagina = CrearClientes(api); pagina.ModelState.AddModelError("ClienteInput.Ci", "Entrada inválida.");
            Assert.IsType<PageResult>(await pagina.OnPostCrearAsync());
        }
        else
        {
            var pagina = CrearVehiculos(api); pagina.ModelState.AddModelError("Formulario.Kilometraje", "Número inválido.");
            Assert.IsType<PageResult>(await pagina.OnPostCreateAsync());
        }
        Assert.DoesNotContain(api.Llamadas, llamada => llamada.StartsWith("POST ") || llamada.StartsWith("PUT "));
    }

    [Theory]
    [InlineData(true, false, 401)]
    [InlineData(true, true, 401)]
    [InlineData(false, false, 401)]
    [InlineData(false, true, 401)]
    [InlineData(true, false, 403)]
    [InlineData(true, true, 403)]
    [InlineData(false, false, 403)]
    [InlineData(false, true, 403)]
    public async Task PermisosHttp_NoSeConviertenEnExitoNiErrorDeFormulario(bool clientes, bool escritura, int codigo)
    {
        var api = PrepararApi();
        api.Error($"{(escritura ? "POST" : "GET")} api/{(clientes ? "clientes" : "vehiculos")}", (HttpStatusCode)codigo);
        IActionResult resultado;
        if (clientes) { var pagina = CrearClientes(api); resultado = escritura ? await pagina.OnPostCrearAsync() : await pagina.OnGetAsync(); }
        else { var pagina = CrearVehiculos(api); resultado = escritura ? await pagina.OnPostCreateAsync() : await pagina.OnGetAsync(); }
        if (codigo == 401) Assert.IsType<ChallengeResult>(resultado);
        else Assert.IsType<ForbidResult>(resultado);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Eliminar_RelacionesConservanElMensajeYLosFiltros(bool clientes)
    {
        var api = PrepararApi();
        api.Error("DELETE api/" + (clientes ? "clientes/7" : "vehiculos/8"), HttpStatusCode.Conflict, "Tiene relaciones.");
        if (clientes)
        {
            var pagina = CrearClientes(api); pagina.ClienteId = 7; pagina.TerminoBusqueda = "Ana"; pagina.ClienteSeleccionadoId = 7;
            var resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostEliminarAsync());
            Assert.Equal("Tiene relaciones.", pagina.TempData["ClienteError"]);
            Assert.Equal("Ana", resultado.RouteValues!["TerminoBusqueda"]);
            Assert.Equal(7, resultado.RouteValues["ClienteSeleccionadoId"]);
        }
        else
        {
            var pagina = CrearVehiculos(api); pagina.Buscar = "Toyota"; pagina.VehiculoSeleccionadoId = 8; pagina.OrdenBorradorId = Guid.NewGuid();
            var resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostDeleteAsync(8));
            Assert.Equal("Tiene relaciones.", pagina.TempData["VehiculoError"]);
            Assert.Equal("Buscar", resultado.PageHandler);
            Assert.Equal(pagina.OrdenBorradorId, resultado.RouteValues!["OrdenBorradorId"]);
            Assert.Equal(8, resultado.RouteValues["VehiculoSeleccionadoId"]);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ServicioNoDisponible_Responde503SinDetallesInternos(bool clientes)
    {
        var api = PrepararApi(); api.Error("GET api/" + (clientes ? "clientes" : "vehiculos"), HttpStatusCode.InternalServerError);
        var resultado = Assert.IsType<ObjectResult>(clientes ? await CrearClientes(api).OnGetAsync() : await CrearVehiculos(api).OnGetAsync());
        Assert.Equal(503, resultado.StatusCode);
        Assert.DoesNotContain("Detalle interno", resultado.Value!.ToString());
    }

    [Fact]
    public async Task Cliente_BorradorVencidoNoCreaNiPierdeLaEntrada()
    {
        var api = PrepararApi(); var pagina = CrearClientes(api); pagina.BorradorVehiculoId = Guid.NewGuid(); pagina.ClienteInput = ClienteFormulario();
        Assert.IsType<PageResult>(await pagina.OnPostCrearAsync());
        Assert.Contains("venció", Assert.Single(pagina.ModelState[string.Empty]!.Errors).ErrorMessage);
        Assert.DoesNotContain("POST api/clientes", api.Llamadas);
        Assert.Equal("Ana", pagina.ClienteInput.Nombres);
    }

    private static ApiFalsa PrepararApi()
    {
        var api = new ApiFalsa();
        var cliente = new ClienteDto { Id = 7, Nombres = "Ana", PrimerApellido = "Prueba", Ci = "1234567" };
        var vehiculo = new VehiculoDto { Id = 8, Placa = "123ABC", ClienteId = 7, Marca = "Toyota", Modelo = "Corolla" };
        api.Json("GET api/clientes", new[] { cliente }); api.Json("GET api/clientes/7", cliente);
        api.Json("GET api/clientes/sugerencias", Array.Empty<SugerenciaCliente>());
        api.Json("GET api/vehiculos", new[] { vehiculo }); api.Json("GET api/vehiculos/8", vehiculo);
        api.Json("GET api/vehiculos/sugerencias", Array.Empty<SugerenciaVehiculo>());
        api.Json("GET api/vehiculos/catalogo", new Dictionary<string, string[]> { ["Toyota"] = ["Yaris", "Corolla"], ["Chevrolet"] = ["Onix"] });
        api.Json("POST api/clientes", new ResultadoFormulario<ClienteFormViewModel>(ClienteFormulario(), new Dictionary<string, string>()));
        api.Json("PUT api/clientes/7", new ResultadoFormulario<ClienteFormViewModel>(ClienteFormulario(), new Dictionary<string, string>()));
        api.Json("POST api/vehiculos", new ResultadoFormulario<VehiculoFormViewModel>(VehiculoFormulario(), new Dictionary<string, string>()));
        api.Json("PUT api/vehiculos/8", new ResultadoFormulario<VehiculoFormViewModel>(VehiculoFormulario(), new Dictionary<string, string>()));
        api.Json("DELETE api/clientes/7", new { }); api.Json("DELETE api/vehiculos/8", new { });
        return api;
    }

    private static ClienteFormViewModel ClienteFormulario() => new() { Id = 7, Nombres = "Ana", Ci = "1234567" };
    private static VehiculoFormViewModel VehiculoFormulario() => new() { Id = 8, Placa = "123ABC", ClienteId = 7, Marca = "Toyota", Modelo = "Corolla" };
    private static DefaultHttpContext Contexto() => new() { Session = new SesionPrueba() };
    private static ClientesPage CrearClientes(ApiFalsa api)
    {
        var contexto = Contexto(); var acceso = new HttpContextAccessor { HttpContext = contexto }; var usuario = new UsuarioPrueba();
        return new ClientesPage(new ClientesApi(api.Cliente()), new BorradorVehiculoSesion(acceso, usuario), new BorradorOrdenSesion(acceso, usuario))
        { PageContext = new PageContext { HttpContext = contexto }, TempData = new TempDataDictionary(contexto, new Temporales()) };
    }
    private static VehiculosPage CrearVehiculos(ApiFalsa api)
    {
        var contexto = Contexto(); var acceso = new HttpContextAccessor { HttpContext = contexto };
        return new VehiculosPage(new VehiculosApi(api.Cliente()), new ClientesApi(api.Cliente()), new BorradorVehiculoSesion(acceso, new UsuarioPrueba()))
        { PageContext = new PageContext { HttpContext = contexto }, TempData = new TempDataDictionary(contexto, new Temporales()) };
    }
    private sealed class Temporales : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
