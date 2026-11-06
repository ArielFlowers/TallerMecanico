using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Api;
using TallerMecanico.Pages.Servicios;
using HistorialPage = TallerMecanico.Pages.Historial.IndexModel;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ServiciosHistorialHttpTests
{
    [Fact]
    public async Task Api_UtilizaRutasDtoFiltrosCodificadosYTodosLosMetodosCrud()
    {
        var api = PrepararApi();
        var cliente = new ServiciosApi(api.Cliente());
        Assert.Single(await cliente.ObtenerAsync("Motor & aceite"));
        Assert.Equal(7, (await cliente.ObtenerPorIdAsync(7))!.Id);
        Assert.Null(await cliente.ObtenerPorIdAsync(99));
        Assert.Single(await cliente.SugerirAsync("aceite & motor"));
        var catalogo = Assert.Single(await cliente.ObtenerCatalogoAsync());
        Assert.Equal("CAMBIO_FILTROS", catalogo.CodigoCatalogo);
        Assert.Equal("unidad", catalogo.UnidadCantidad);
        Assert.Equal(100m, catalogo.Costo);
        Assert.Single(await cliente.ObtenerHistorialAsync());
        Assert.Empty((await cliente.CrearAsync(Formulario())).Errores);
        Assert.Empty((await cliente.ActualizarAsync(7, Formulario())).Errores);
        Assert.True((await cliente.EliminarAsync(7)).Exito);

        Assert.Contains("GET api/servicios?terminoBusqueda=Motor%20%26%20aceite", api.Llamadas);
        Assert.Contains("GET api/servicios/sugerencias?termino=aceite%20%26%20motor", api.Llamadas);
        Assert.Contains("GET api/servicios/catalogo", api.Llamadas);
        Assert.Contains("GET api/servicios/historial", api.Llamadas);
        Assert.Contains("POST api/servicios", api.Llamadas);
        Assert.Contains("PUT api/servicios/7", api.Llamadas);
        Assert.Contains("DELETE api/servicios/7", api.Llamadas);
        Assert.Equal(2, api.Cuerpos.Count);
        foreach (string cuerpo in api.Cuerpos)
        {
            using var json = JsonDocument.Parse(cuerpo);
            Assert.Equal("unidad", json.RootElement.GetProperty("unidadCantidad").GetString());
            Assert.Equal(100m, json.RootElement.GetProperty("costo").GetDecimal());
            Assert.False(json.RootElement.TryGetProperty("codigoCatalogo", out _));
            Assert.False(json.RootElement.TryGetProperty("creadoPor", out _));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unidad")]
    public async Task Editar_CargaDtoSinAlterarPrecioTiempoNiUnidad(string? unidad)
    {
        var api = PrepararApi();
        api.Json("GET api/servicios/7", new ServicioDto
        { Id = 7, Nombre = "Cambio de filtros", Descripcion = "Filtro de aceite", Costo = 123.45m, TiempoEstimadoHoras = 1.25m, UnidadCantidad = unidad });
        var pagina = Preparar(new EditModel(new ServiciosApi(api.Cliente())));
        Assert.IsType<PageResult>(await pagina.OnGetAsync(7));
        Assert.Equal(7, pagina.ServicioId);
        Assert.Equal("Cambio de filtros", pagina.Formulario.Nombre);
        Assert.Equal("Filtro de aceite", pagina.Formulario.Descripcion);
        Assert.Equal(123.45m, pagina.Formulario.Costo);
        Assert.Equal(1.25m, pagina.Formulario.TiempoEstimadoHoras);
        Assert.Equal(unidad, pagina.Formulario.UnidadCantidad);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Guardar_ConservaMensajesExitoYRedireccionAlListado(bool editar)
    {
        var api = PrepararApi();
        RedirectToPageResult resultado;
        if (editar)
        {
            var pagina = Preparar(new EditModel(new ServiciosApi(api.Cliente())));
            pagina.Formulario = Formulario();
            resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostAsync(7));
            Assert.Equal("Servicio actualizado correctamente.", pagina.MensajeExito);
        }
        else
        {
            var pagina = Preparar(new CreateModel(new ServiciosApi(api.Cliente())));
            pagina.Formulario = Formulario();
            Assert.IsType<PageResult>(await pagina.OnGetAsync());
            resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostAsync());
            Assert.Equal("Servicio creado correctamente.", pagina.MensajeExito);
        }
        Assert.Equal("./Index", resultado.PageName);
        Assert.Single(api.Llamadas);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidacionesApi_SeMuestranPorCampoYConservanFormulario(bool editar)
    {
        var api = PrepararApi();
        var formulario = Formulario(); formulario.Nombre = "Nombre recibido";
        var errores = new Dictionary<string, string>
        {
            [nameof(ServicioFormViewModel.Nombre)] = "El nombre contiene caracteres no permitidos.",
            [nameof(ServicioFormViewModel.Descripcion)] = "La descripción solo puede contener un espacio entre palabras.",
            [nameof(ServicioFormViewModel.UnidadCantidad)] = "La unidad de cobro de un servicio del catálogo no se puede cambiar.",
            [nameof(ServicioFormViewModel.Costo)] = "El costo debe ser mayor a cero."
        };
        api.Json(editar ? "PUT api/servicios/7" : "POST api/servicios", new ResultadoFormulario<ServicioFormViewModel>(formulario, errores));
        PageModel pagina;
        if (editar)
        {
            var edicion = Preparar(new EditModel(new ServiciosApi(api.Cliente()))); pagina = edicion;
            Assert.IsType<PageResult>(await edicion.OnPostAsync(7));
            Assert.Equal("Nombre recibido", edicion.Formulario.Nombre);
            Assert.Equal("unidad", edicion.Formulario.UnidadCantidad);
            Assert.Equal(7, edicion.ServicioId);
            Assert.Null(edicion.MensajeExito);
        }
        else
        {
            var creacion = Preparar(new CreateModel(new ServiciosApi(api.Cliente()))); pagina = creacion;
            Assert.IsType<PageResult>(await creacion.OnPostAsync());
            Assert.Equal("Nombre recibido", creacion.Formulario.Nombre);
            Assert.Equal(100m, creacion.Formulario.Costo);
            Assert.Null(creacion.MensajeExito);
        }
        foreach (var error in errores)
            Assert.Equal(error.Value, Assert.Single(pagina.ModelState[$"Formulario.{error.Key}"]!.Errors).ErrorMessage);
        Assert.Single(api.Llamadas);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.BadRequest)]
    [InlineData(true, HttpStatusCode.BadRequest)]
    [InlineData(false, HttpStatusCode.Conflict)]
    [InlineData(true, HttpStatusCode.Conflict)]
    public async Task RechazosApi_ConservanMensajeYValoresSinExito(bool editar, HttpStatusCode codigo)
    {
        var api = PrepararApi();
        api.Error(editar ? "PUT api/servicios/7" : "POST api/servicios", codigo, "La unidad de cobro no se puede cambiar.");
        PageModel pagina;
        if (editar)
        {
            var edicion = Preparar(new EditModel(new ServiciosApi(api.Cliente()))); pagina = edicion;
            edicion.Formulario = Formulario();
            Assert.IsType<PageResult>(await edicion.OnPostAsync(7));
            Assert.Equal(100m, edicion.Formulario.Costo);
            Assert.Equal(7, edicion.ServicioId);
        }
        else
        {
            var creacion = Preparar(new CreateModel(new ServiciosApi(api.Cliente()))); pagina = creacion;
            creacion.Formulario = Formulario();
            Assert.IsType<PageResult>(await creacion.OnPostAsync());
            Assert.Equal("unidad", creacion.Formulario.UnidadCantidad);
        }
        Assert.Equal("La unidad de cobro no se puede cambiar.", Assert.Single(pagina.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BindingInvalido_NoEnviaEscrituras(bool editar)
    {
        var api = PrepararApi();
        if (editar)
        {
            var pagina = Preparar(new EditModel(new ServiciosApi(api.Cliente())));
            pagina.ModelState.AddModelError("Formulario.Costo", "Número inválido.");
            Assert.IsType<PageResult>(await pagina.OnPostAsync(7));
        }
        else
        {
            var pagina = Preparar(new CreateModel(new ServiciosApi(api.Cliente())));
            pagina.ModelState.AddModelError("Formulario.Costo", "Número inválido.");
            Assert.IsType<PageResult>(await pagina.OnPostAsync());
        }
        Assert.Empty(api.Llamadas);
    }

    [Fact]
    public void FormularioCompartido_ConservaObligatoriedadYPreciosPositivos()
    {
        var formulario = new ServicioFormViewModel();
        var errores = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(formulario, new ValidationContext(formulario), errores, true));
        Assert.Contains(errores, error => error.MemberNames.Contains(nameof(ServicioFormViewModel.Nombre)));
        Assert.Contains(errores, error => error.MemberNames.Contains(nameof(ServicioFormViewModel.Descripcion)));
        Assert.Contains(errores, error => error.MemberNames.Contains(nameof(ServicioFormViewModel.Costo)));
        Assert.Contains(errores, error => error.MemberNames.Contains(nameof(ServicioFormViewModel.TiempoEstimadoHoras)));
    }

    [Fact]
    public async Task ServicioInexistente_Devuelve404YConservaMensajeAlActualizar()
    {
        var api = PrepararApi();
        Assert.IsType<NotFoundResult>(await Preparar(new EditModel(new ServiciosApi(api.Cliente()))).OnGetAsync(99));
        Assert.IsType<NotFoundResult>(await Preparar(new DeleteModel(new ServiciosApi(api.Cliente()))).OnGetAsync(99));
        var edicion = Preparar(new EditModel(new ServiciosApi(api.Cliente()))); edicion.Formulario = Formulario();
        Assert.IsType<PageResult>(await edicion.OnPostAsync(99));
        Assert.Equal("El servicio solicitado no existe.", Assert.Single(edicion.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Eliminar_ConservaConfirmacionRestriccionesYMensajes(HttpStatusCode codigo)
    {
        const string mensaje = "No se puede eliminar un servicio del catálogo de Nueva Orden.";
        var api = PrepararApi();
        if (codigo != HttpStatusCode.OK) api.Error("DELETE api/servicios/7", codigo, mensaje);
        var pagina = Preparar(new DeleteModel(new ServiciosApi(api.Cliente())));
        Assert.IsType<PageResult>(await pagina.OnGetAsync(7));
        Assert.Equal(100m, pagina.Servicio.Costo);
        Assert.Equal("unidad", pagina.Servicio.UnidadCantidad);
        var resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostAsync(7));
        Assert.Equal("./Index", resultado.PageName);
        Assert.Equal(codigo == HttpStatusCode.OK ? "Servicio eliminado correctamente." : null, pagina.MensajeExito);
        Assert.Equal(codigo == HttpStatusCode.OK ? null : mensaje, pagina.MensajeError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historial_ConsultaApiYConservaCostosHistoricosYFecha(bool vacio)
    {
        var api = PrepararApi();
        if (vacio) api.Json("GET api/servicios/historial", Array.Empty<HistorialCostoServicioDto>());
        var pagina = Preparar(new HistorialPage(new ServiciosApi(api.Cliente())));
        Assert.IsType<PageResult>(await pagina.OnGetAsync());
        Assert.Equal(!vacio, pagina.TieneRegistros);
        Assert.Equal("GET api/servicios/historial", Assert.Single(api.Llamadas));
        if (!vacio)
        {
            var cambio = Assert.Single(pagina.Historial);
            Assert.Equal(90m, cambio.CostoAnterior);
            Assert.Equal(100m, cambio.CostoNuevo);
            Assert.Equal(7, cambio.ServicioId);
            Assert.Equal("Cambio de filtros", cambio.NombreServicio);
            Assert.Equal(new DateTime(2026, 11, 1, 9, 30, 0), cambio.FechaCambio);
        }
    }

    public static IEnumerable<object[]> AccionesRestringidas =>
        from accion in new[] { "listado", "sugerencias", "crearGet", "crear", "editarGet", "editar", "eliminarGet", "eliminar", "historial" }
        from rol in new[] { "", "Recepcionista" }
        select new object[] { accion, rol };

    [Theory]
    [MemberData(nameof(AccionesRestringidas))]
    public async Task PermisosActuales_ExigenAdministradorAntesDeConsultarApi(string accion, string rol)
    {
        var api = PrepararApi();
        var resultado = await EjecutarAsync(api, accion, rol);
        if (rol == "") Assert.IsType<ChallengeResult>(resultado);
        else Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(api.Llamadas);
    }

    public static IEnumerable<object[]> RespuestasFallidas =>
        from accion in new[] { "listado", "sugerencias", "crear", "editar", "eliminar", "historial" }
        from codigo in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError }
        select new object[] { accion, codigo };

    [Theory]
    [MemberData(nameof(RespuestasFallidas))]
    public async Task ErroresRemotos_NoExponenDetallesInternosNiAnuncianExito(string accion, HttpStatusCode codigo)
    {
        var api = PrepararApi();
        string ruta = accion switch
        {
            "sugerencias" => "GET api/servicios/sugerencias",
            "crear" => "POST api/servicios",
            "editar" => "PUT api/servicios/7",
            "eliminar" => "DELETE api/servicios/7",
            "historial" => "GET api/servicios/historial",
            _ => "GET api/servicios"
        };
        api.Error(ruta, codigo);
        var resultado = await EjecutarAsync(api, accion);
        if (codigo == HttpStatusCode.Unauthorized) Assert.IsType<ChallengeResult>(resultado);
        else if (codigo == HttpStatusCode.Forbidden) Assert.IsType<ForbidResult>(resultado);
        else
        {
            var error = Assert.IsType<ObjectResult>(resultado);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
            Assert.DoesNotContain("Detalle interno", Assert.IsType<string>(error.Value));
        }
        Assert.Single(api.Llamadas);
    }

    private static async Task<IActionResult> EjecutarAsync(ApiFalsa api, string accion, string rol = "Administrador")
    {
        var cliente = new ServiciosApi(api.Cliente());
        switch (accion)
        {
            case "listado": return await Preparar(new IndexModel(cliente), rol).OnGetAsync(null);
            case "sugerencias": return await Preparar(new IndexModel(cliente), rol).OnGetSugerenciasAsync("fil");
            case "crearGet": return await Preparar(new CreateModel(cliente), rol).OnGetAsync();
            case "crear":
                var crear = Preparar(new CreateModel(cliente), rol); crear.Formulario = Formulario();
                return await crear.OnPostAsync();
            case "editarGet": return await Preparar(new EditModel(cliente), rol).OnGetAsync(7);
            case "editar":
                var editar = Preparar(new EditModel(cliente), rol); editar.Formulario = Formulario();
                return await editar.OnPostAsync(7);
            case "eliminarGet": return await Preparar(new DeleteModel(cliente), rol).OnGetAsync(7);
            case "eliminar": return await Preparar(new DeleteModel(cliente), rol).OnPostAsync(7);
            case "historial": return await Preparar(new HistorialPage(cliente), rol).OnGetAsync();
            default: throw new ArgumentOutOfRangeException(nameof(accion));
        }
    }

    private static T Preparar<T>(T pagina, string rol = "Administrador") where T : PageModel
    {
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(rol == "" ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Role, rol)], "Prueba"))
        };
        pagina.PageContext = new PageContext { HttpContext = contexto };
        pagina.TempData = new TempDataDictionary(contexto, new Temporales());
        return pagina;
    }

    private static ServicioFormViewModel Formulario() => new()
    { Nombre = "Cambio de filtros", Descripcion = "Filtro de aceite", Costo = 100m, TiempoEstimadoHoras = 1m, UnidadCantidad = "unidad" };

    private static ApiFalsa PrepararApi()
    {
        var api = new ApiFalsa();
        var servicio = new ServicioDto
        { Id = 7, CodigoCatalogo = "CAMBIO_FILTROS", Nombre = "Cambio de filtros", Descripcion = "Filtro de aceite", Costo = 100m, TiempoEstimadoHoras = 1m, UnidadCantidad = "unidad" };
        api.Json("GET api/servicios", new[] { servicio });
        api.Json("GET api/servicios/7", servicio);
        api.Json("GET api/servicios/catalogo", new[] { servicio });
        api.Json("GET api/servicios/sugerencias", new[] { new SugerenciaServicio(7, "Cambio de filtros", "Bs. 100 por unidad") });
        api.Json("GET api/servicios/historial", new[] { new HistorialCostoServicioDto
        { Id = 1, ServicioId = 7, NombreServicio = servicio.Nombre, CostoAnterior = 90m, CostoNuevo = 100m, FechaCambio = new DateTime(2026, 11, 1, 9, 30, 0) } });
        api.Json("POST api/servicios", new ResultadoFormulario<ServicioFormViewModel>(Formulario(), new Dictionary<string, string>()));
        api.Json("PUT api/servicios/7", new ResultadoFormulario<ServicioFormViewModel>(Formulario(), new Dictionary<string, string>()));
        api.Json("DELETE api/servicios/7", new { });
        return api;
    }

    private sealed class Temporales : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
