using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Api;
using TallerMecanico.Sesion;
using TallerMecanico.Pages.Ordenes;
using ProductosPage = TallerMecanico.Pages.Productos.IndexModel;
using MecanicosPage = TallerMecanico.Pages.Mecanicos.IndexModel;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ProductosMecanicosHttpTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClientesApi_UtilizanRutasCrudYDtoSinEnviarAuditoria(bool productos)
    {
        var api = PrepararApi();
        if (productos)
        {
            var cliente = new ProductosApi(api.Cliente());
            Assert.Single(await cliente.ObtenerAsync("Filtro & Aceite"));
            Assert.NotNull(await cliente.ObtenerPorIdAsync(7));
            Assert.Null(await cliente.ObtenerPorIdAsync(99));
            Assert.Equal(10, Assert.Single(await cliente.SugerirAsync("fil")).Stock);
            Assert.Empty((await cliente.CrearAsync(ProductoFormulario())).Errores);
            Assert.Empty((await cliente.ActualizarAsync(ProductoFormulario())).Errores);
            Assert.True((await cliente.EliminarAsync(7)).Exito);
        }
        else
        {
            var cliente = new MecanicosApi(api.Cliente());
            Assert.Single(await cliente.ObtenerAsync("José & Pérez"));
            Assert.NotNull(await cliente.ObtenerPorIdAsync(8));
            Assert.Null(await cliente.ObtenerPorIdAsync(99));
            Assert.Single(await cliente.SugerirAsync("jose"));
            Assert.Empty(await cliente.CrearAsync(MecanicoFormulario()));
            Assert.True((await cliente.ActualizarAsync(8, MecanicoFormulario())).Actualizado);
            Assert.True((await cliente.EliminarAsync(8)).Exito);
        }
        string ruta = productos ? "api/productos" : "api/mecanicos";
        Assert.Contains(api.Llamadas, llamada => llamada.StartsWith($"GET {ruta}?terminoBusqueda=") && llamada.Contains("%26"));
        Assert.Contains($"POST {ruta}", api.Llamadas);
        Assert.Contains($"PUT {ruta}/{(productos ? 7 : 8)}", api.Llamadas);
        Assert.Contains($"DELETE {ruta}/{(productos ? 7 : 8)}", api.Llamadas);
        Assert.Equal(2, api.Cuerpos.Count);
        foreach (string cuerpo in api.Cuerpos)
        {
            using var json = JsonDocument.Parse(cuerpo);
            Assert.False(json.RootElement.TryGetProperty("creadoPor", out _));
            if (!productos) Assert.False(json.RootElement.TryGetProperty("id", out _));
        }
    }

    [Theory]
    [InlineData(true, "Administrador")]
    [InlineData(true, "Recepcionista")]
    [InlineData(false, "Administrador")]
    [InlineData(false, "Recepcionista")]
    public async Task LecturaDeAmbosRoles_ConservaBusquedaSeleccionYSugerencias(bool productos, string rol)
    {
        var api = PrepararApi();
        if (productos)
        {
            var (pagina, _, _, _) = CrearProductos(api, rol);
            pagina.TerminoBusqueda = " Filtro ";
            Assert.IsType<PageResult>(await pagina.OnGetBuscarAsync());
            var producto = Assert.Single(pagina.Productos);
            Assert.Equal("autor_servidor", producto.CreadoPor);
            Assert.Equal(new DateTime(2026, 1, 2), producto.FechaCreacion);
            Assert.Equal(10, producto.Stock);
            Assert.Contains("GET api/productos?terminoBusqueda=Filtro", api.Llamadas);
            pagina.ProductoSeleccionadoId = 7; await pagina.OnGetAsync();
            Assert.Contains("GET api/productos/7", api.Llamadas);
            Assert.IsType<JsonResult>(await pagina.OnGetSugerenciasAsync("fil"));
            Assert.Equal("no-store", pagina.Response.Headers.CacheControl);
        }
        else
        {
            var (pagina, _, _, _) = CrearMecanicos(api, rol);
            pagina.TerminoBusqueda = "José";
            Assert.IsType<PageResult>(await pagina.OnGetAsync());
            Assert.Single(pagina.Mecanicos);
            Assert.Equal(rol == "Administrador", pagina.PuedeAdministrar);
            Assert.Contains(api.Llamadas, llamada => llamada.StartsWith("GET api/mecanicos?terminoBusqueda="));
            pagina.MecanicoSeleccionadoId = 8; await pagina.OnGetAsync();
            Assert.Contains("GET api/mecanicos/8", api.Llamadas);
            Assert.IsType<JsonResult>(await pagina.OnGetSugerenciasAsync("jose"));
            Assert.Equal("no-store", pagina.Response.Headers.CacheControl);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Guardar_ErroresDeCampoConservanDatosYModal(bool productos, bool actualizar)
    {
        var api = PrepararApi(); string metodo = actualizar ? "PUT" : "POST";
        if (productos)
        {
            var formulario = ProductoFormulario(); formulario.Nombre = "Nombre normalizado";
            api.Json($"{metodo} api/productos" + (actualizar ? "/7" : ""), new ResultadoFormulario<ProductoFormViewModel>(
                formulario, new Dictionary<string, string> { ["Codigo"] = "Código repetido.", ["Precio"] = "Precio inválido." }));
            var (pagina, _, _, _) = CrearProductos(api); pagina.ProductoInput = ProductoFormulario(); pagina.ProductoId = 7;
            Assert.IsType<PageResult>(actualizar ? await pagina.OnPostActualizarAsync() : await pagina.OnPostCrearAsync());
            Assert.Equal("Código repetido.", Assert.Single(pagina.ModelState["ProductoInput.Codigo"]!.Errors).ErrorMessage);
            Assert.Equal("Precio inválido.", Assert.Single(pagina.ModelState["ProductoInput.Precio"]!.Errors).ErrorMessage);
            Assert.Equal("Nombre normalizado", pagina.ProductoInput.Nombre);
            Assert.Equal(actualizar ? "editar" : "crear", pagina.FormularioActivo);
        }
        else
        {
            var errores = new Dictionary<string, string> { ["Ci"] = "CI repetido.", ["ComplementoCi"] = "Formato inválido." };
            if (actualizar) api.Json("PUT api/mecanicos/8", new ResultadoMecanico(false, errores));
            else api.Json("POST api/mecanicos", errores);
            var (pagina, _, _, _) = CrearMecanicos(api); pagina.MecanicoId = 8; pagina.MecanicoInput = MecanicoFormulario();
            Assert.IsType<PageResult>(actualizar ? await pagina.OnPostActualizarAsync() : await pagina.OnPostCrearAsync());
            Assert.Equal("CI repetido.", Assert.Single(pagina.ModelState["MecanicoInput.Ci"]!.Errors).ErrorMessage);
            Assert.Equal("Formato inválido.", Assert.Single(pagina.ModelState["MecanicoInput.ComplementoCi"]!.Errors).ErrorMessage);
            Assert.Equal("José", pagina.MecanicoInput.Nombres);
            Assert.Equal(actualizar ? "editar" : "crear", pagina.FormularioActivo);
            Assert.False(pagina.ModelState.ContainsKey(string.Empty));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Error400_SeMuestraComoValidacionSinPerderFormulario(bool productos)
    {
        var api = PrepararApi(); api.Error("POST api/" + (productos ? "productos" : "mecanicos"), HttpStatusCode.BadRequest, "Revisa los campos.");
        PageModel pagina;
        if (productos) { var (producto, _, _, _) = CrearProductos(api); pagina = producto; Assert.IsType<PageResult>(await producto.OnPostCrearAsync()); }
        else { var (mecanico, _, _, _) = CrearMecanicos(api); pagina = mecanico; Assert.IsType<PageResult>(await mecanico.OnPostCrearAsync()); }
        Assert.Equal("Revisa los campos.", Assert.Single(pagina.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MecanicoNoEncontrado_ConservaMensajeDeEdicion(bool respuesta404)
    {
        var api = PrepararApi();
        if (respuesta404) api.Error("PUT api/mecanicos/8", HttpStatusCode.NotFound);
        else api.Json("PUT api/mecanicos/8", new ResultadoMecanico(false, new Dictionary<string, string>()));
        var (pagina, _, _, _) = CrearMecanicos(api); pagina.MecanicoId = 8;
        Assert.IsType<PageResult>(await pagina.OnPostActualizarAsync());
        Assert.Equal("El mecánico seleccionado ya no existe.", Assert.Single(pagina.ModelState[string.Empty]!.Errors).ErrorMessage);
        Assert.Equal("editar", pagina.FormularioActivo);
    }

    [Theory]
    [InlineData("crear")]
    [InlineData("editar")]
    [InlineData("eliminar")]
    public async Task Mecanicos_RecepcionistaNoEscribeAunqueTengaBorradorValido(string operacion)
    {
        var api = PrepararApi(); var (pagina, borradores, _, _) = CrearMecanicos(api, "Recepcionista");
        pagina.OrdenBorradorId = borradores.Guardar(new OrdenFormViewModel());
        IActionResult resultado = operacion switch
        {
            "crear" => await pagina.OnPostCrearAsync(),
            "editar" => await pagina.OnPostActualizarAsync(),
            _ => await pagina.OnPostEliminarAsync()
        };
        Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(api.Llamadas);
    }

    [Theory]
    [InlineData("sin-borrador")]
    [InlineData("inexistente")]
    [InlineData("vencido")]
    [InlineData("otro-usuario")]
    public async Task Productos_RecepcionistaNecesitaBorradorPropioVigente(string caso)
    {
        var api = PrepararApi(); var (pagina, borradores, usuario, contexto) = CrearProductos(api, "Recepcionista");
        if (caso == "inexistente") pagina.OrdenBorradorId = Guid.NewGuid();
        if (caso is "vencido" or "otro-usuario")
        {
            pagina.OrdenBorradorId = borradores.Guardar(new OrdenFormViewModel());
            if (caso == "otro-usuario") usuario.Username = "otro";
            else contexto.Session.SetString($"Ordenes:Borrador:{pagina.OrdenBorradorId:N}", JsonSerializer.Serialize(new
            { Usuario = usuario.Username, VenceUtc = DateTime.UtcNow.AddMinutes(-1), Formulario = new OrdenFormViewModel() }));
        }
        Assert.IsType<ForbidResult>(await pagina.OnPostCrearAsync());
        Assert.Empty(api.Llamadas);
    }

    [Theory]
    [InlineData("editar")]
    [InlineData("eliminar")]
    public async Task Productos_RecepcionistaNoEditaNiEliminaDesdeOrden(string operacion)
    {
        var api = PrepararApi(); var (pagina, borradores, _, _) = CrearProductos(api, "Recepcionista");
        pagina.OrdenBorradorId = borradores.Guardar(new OrdenFormViewModel());
        Assert.IsType<ForbidResult>(operacion == "editar" ? await pagina.OnPostActualizarAsync() : await pagina.OnPostEliminarAsync());
        Assert.Empty(api.Llamadas);
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(true, "Invitado")]
    [InlineData(false, "")]
    [InlineData(false, "Invitado")]
    public async Task Lectura_SinSesionORolNoConsultaLaApi(bool productos, string rol)
    {
        var api = PrepararApi();
        IActionResult resultado = productos ? await CrearProductos(api, rol).Pagina.OnGetAsync() : await CrearMecanicos(api, rol).Pagina.OnGetAsync();
        if (rol == "") Assert.IsType<ChallengeResult>(resultado); else Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(api.Llamadas);
    }

    [Theory]
    [InlineData(true, 401)]
    [InlineData(true, 403)]
    [InlineData(false, 401)]
    [InlineData(false, 403)]
    public async Task RechazoDeLaApi_Conserva401Y403(bool productos, int estado)
    {
        var api = PrepararApi(); api.Error("POST api/" + (productos ? "productos" : "mecanicos"), (HttpStatusCode)estado);
        IActionResult resultado = productos ? await CrearProductos(api).Pagina.OnPostCrearAsync() : await CrearMecanicos(api).Pagina.OnPostCrearAsync();
        if (estado == 401) Assert.IsType<ChallengeResult>(resultado); else Assert.IsType<ForbidResult>(resultado);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Administrador_ActualizaYEliminaConMensajesYFiltros(bool productos)
    {
        var api = PrepararApi();
        if (productos)
        {
            var (pagina, _, _, _) = CrearProductos(api); pagina.ProductoInput = ProductoFormulario(); pagina.ProductoId = 7;
            pagina.TerminoBusqueda = "Filtro"; pagina.ProductoSeleccionadoId = 7;
            var editada = Assert.IsType<RedirectToPageResult>(await pagina.OnPostActualizarAsync());
            Assert.Equal("Filtro", editada.RouteValues!["TerminoBusqueda"]);
            Assert.Equal("Producto actualizado correctamente.", pagina.TempData["ProductoExito"]);
            Assert.IsType<RedirectToPageResult>(await pagina.OnPostEliminarAsync());
            Assert.Equal("Producto eliminado correctamente.", pagina.TempData["ProductoExito"]);
        }
        else
        {
            var (pagina, _, _, _) = CrearMecanicos(api); pagina.MecanicoInput = MecanicoFormulario(); pagina.MecanicoId = 8;
            pagina.TerminoBusqueda = "José"; pagina.MecanicoSeleccionadoId = 8;
            var editada = Assert.IsType<RedirectToPageResult>(await pagina.OnPostActualizarAsync());
            Assert.Equal("José", editada.RouteValues!["terminoBusqueda"]);
            Assert.Equal("Mecánico actualizado correctamente.", pagina.MensajeExito);
            Assert.IsType<RedirectToPageResult>(await pagina.OnPostEliminarAsync());
            Assert.Equal("Mecánico eliminado correctamente.", pagina.MensajeExito);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EliminarRelacionado_ConservaElMensajeDelServidor(bool productos)
    {
        var api = PrepararApi(); api.Error("DELETE api/" + (productos ? "productos/7" : "mecanicos/8"), HttpStatusCode.Conflict, "Tiene órdenes asociadas.");
        if (productos)
        {
            var (pagina, _, _, _) = CrearProductos(api); pagina.ProductoId = 7;
            Assert.IsType<RedirectToPageResult>(await pagina.OnPostEliminarAsync());
            Assert.Equal("Tiene órdenes asociadas.", pagina.TempData["ProductoError"]);
        }
        else
        {
            var (pagina, _, _, _) = CrearMecanicos(api); pagina.MecanicoId = 8;
            Assert.IsType<RedirectToPageResult>(await pagina.OnPostEliminarAsync());
            Assert.Equal("Tiene órdenes asociadas.", pagina.MensajeError);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BindingInvalido_NoEnviaEscrituras(bool productos)
    {
        var api = PrepararApi();
        if (productos)
        {
            var (pagina, _, _, _) = CrearProductos(api); pagina.ModelState.AddModelError("ProductoInput.Stock", "Número inválido.");
            Assert.IsType<PageResult>(await pagina.OnPostCrearAsync());
        }
        else
        {
            var (pagina, _, _, _) = CrearMecanicos(api); pagina.ModelState.AddModelError("MecanicoInput.Ci", "Dato inválido.");
            Assert.IsType<PageResult>(await pagina.OnPostCrearAsync());
        }
        Assert.DoesNotContain(api.Llamadas, llamada => llamada.StartsWith("POST ") || llamada.StartsWith("PUT "));
    }

    [Fact]
    public async Task ProductoDesdeOrden_RecuperaIdSiLaRespuestaSoloTraeFormulario()
    {
        var api = PrepararApi(); var formulario = ProductoFormulario(); formulario.Id = 0;
        api.Json("POST api/productos", new ResultadoFormulario<ProductoFormViewModel>(formulario, new Dictionary<string, string>()));
        var (pagina, borradores, _, _) = CrearProductos(api, "Recepcionista"); pagina.ProductoInput = ProductoFormulario();
        pagina.OrdenBorradorId = borradores.Guardar(new OrdenFormViewModel { MecanicoId = 8 });
        var resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostCrearAsync());
        Assert.Equal("/Ordenes/Crear", resultado.PageName);
        Assert.Equal(7, resultado.RouteValues!["ProductoNuevoId"]);
        Assert.Equal(8, borradores.Obtener(pagina.OrdenBorradorId.Value)!.MecanicoId);
        Assert.Contains("GET api/productos?terminoBusqueda=FIL-001", api.Llamadas);
    }

    private static ApiFalsa PrepararApi()
    {
        var api = new ApiFalsa();
        var producto = new ProductoDto { Id = 7, Codigo = "FIL-001", Nombre = "Filtro", Precio = 25, Stock = 10, CreadoPor = "autor_servidor", FechaCreacion = new DateTime(2026, 1, 2) };
        var mecanico = new MecanicoDto { Id = 8, Ci = "1234567", Nombres = "José", PrimerApellido = "Prueba" };
        api.Json("GET api/productos", new[] { producto }); api.Json("GET api/productos/7", producto);
        api.Json("GET api/productos/sugerencias", new[] { new SugerenciaProducto(7, "Filtro", "FIL-001") { Stock = 10 } });
        api.Json("GET api/mecanicos", new[] { mecanico }); api.Json("GET api/mecanicos/8", mecanico);
        api.Json("GET api/mecanicos/sugerencias", new[] { new SugerenciaMecanico(8, "José Prueba", "CI: 1234567") });
        api.Json("POST api/productos", new ResultadoFormulario<ProductoFormViewModel>(ProductoFormulario(), new Dictionary<string, string>()));
        api.Json("PUT api/productos/7", new ResultadoFormulario<ProductoFormViewModel>(ProductoFormulario(), new Dictionary<string, string>()));
        api.Json("DELETE api/productos/7", new { });
        api.Json("POST api/mecanicos", new Dictionary<string, string>());
        api.Json("PUT api/mecanicos/8", new ResultadoMecanico(true, new Dictionary<string, string>()));
        api.Json("DELETE api/mecanicos/8", new { });
        return api;
    }

    private static ProductoFormViewModel ProductoFormulario() => new() { Id = 7, Codigo = "FIL-001", Nombre = "Filtro", Precio = 25, Stock = 10 };
    private static MecanicoInputModel MecanicoFormulario() => new() { Ci = "1234567", Nombres = "José", PrimerApellido = "Prueba", Celular = "71234567" };
    private static (DefaultHttpContext Contexto, BorradorOrdenSesion Borradores, UsuarioPrueba Usuario) CrearSesion(string rol)
    {
        var contexto = new DefaultHttpContext { Session = new SesionPrueba(), User = new ClaimsPrincipal(rol == "" ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Role, rol)], "Prueba")) };
        var usuario = new UsuarioPrueba { Rol = rol, EstaAutenticado = rol != "" };
        return (contexto, new BorradorOrdenSesion(new HttpContextAccessor { HttpContext = contexto }, usuario), usuario);
    }
    private static (ProductosPage Pagina, BorradorOrdenSesion Borradores, UsuarioPrueba Usuario, DefaultHttpContext Contexto) CrearProductos(ApiFalsa api, string rol = "Administrador")
    {
        var (contexto, borradores, usuario) = CrearSesion(rol);
        return (new ProductosPage(new ProductosApi(api.Cliente()), borradores) { PageContext = new PageContext { HttpContext = contexto }, TempData = new TempDataDictionary(contexto, new Temporales()) }, borradores, usuario, contexto);
    }
    private static (MecanicosPage Pagina, BorradorOrdenSesion Borradores, UsuarioPrueba Usuario, DefaultHttpContext Contexto) CrearMecanicos(ApiFalsa api, string rol = "Administrador")
    {
        var (contexto, borradores, usuario) = CrearSesion(rol);
        return (new MecanicosPage(new MecanicosApi(api.Cliente()), borradores) { PageContext = new PageContext { HttpContext = contexto }, TempData = new TempDataDictionary(contexto, new Temporales()) }, borradores, usuario, contexto);
    }
    private sealed class Temporales : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
