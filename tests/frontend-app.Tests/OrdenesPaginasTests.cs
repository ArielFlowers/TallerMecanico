using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Api;
using TallerMecanico.Contratos.Dashboard;
using TallerMecanico.Sesion;
using TallerMecanico.Contratos.Ordenes;
using TallerMecanico.Pages.Ordenes;
using PaginaInicio = TallerMecanico.Pages.IndexModel;
using PaginaOrdenes = TallerMecanico.Pages.Ordenes.IndexModel;

namespace TallerMecanico.IntegrationTests;

// Páginas de órdenes con el Servicio 1 simulado: no levantan procesos ni usan la base.
[Trait("Area", "Backend")]
public sealed class OrdenesPaginasTests
{
    private static readonly DateTime FechaServicio = new(2026, 10, 18);

    [Fact]
    public async Task Get_MuestraLaFechaQueDevuelveElServicio()
    {
        var (pagina, api, _) = CrearPagina();

        await pagina.OnGetAsync();

        Assert.Equal(FechaServicio, pagina.FechaOrden);
        Assert.Contains("GET api/ordenes/fecha-actual", api.Llamadas);
    }

    [Fact]
    public async Task Get_BorradorConservaServiciosYCantidades()
    {
        var (pagina, _, borradores) = CrearPagina();
        var formulario = new OrdenFormViewModel { ServicioIds = [1], CantidadesServicios = new() { [1] = 3 } };
        pagina.BorradorId = borradores.Guardar(formulario);

        await pagina.OnGetAsync();

        var servicio = Assert.Single(pagina.ServiciosSeleccionados);
        Assert.Equal(3, servicio.Cantidad);
        Assert.Equal(90m, pagina.CostoServiciosSeleccionados);
    }

    [Fact]
    public async Task Guardar_TokenInvalido_NoLlamaALaApiYRenuevaElToken()
    {
        var (pagina, api, _) = CrearPagina();
        Guid vencido = Guid.NewGuid();
        pagina.Formulario = new OrdenFormViewModel { VehiculoId = 1, MecanicoId = 1, ServicioIds = [1], Token = vencido };

        Assert.IsType<PageResult>(await pagina.OnPostGuardarAsync());

        Assert.DoesNotContain("POST api/ordenes", api.Llamadas);
        Assert.NotEqual(vencido, pagina.Formulario.Token);
        Assert.Contains(pagina.ModelState[string.Empty]!.Errors, error => error.ErrorMessage.Contains("venció"));
    }

    [Fact]
    public async Task Guardar_ServicioNoDisponible_MuestraErrorYConservaElFormulario()
    {
        var (pagina, api, _) = CrearPagina();
        api.Error("POST api/ordenes", HttpStatusCode.ServiceUnavailable);
        await pagina.OnGetAsync();
        pagina.Formulario.VehiculoId = 1;
        pagina.Formulario.MecanicoId = 1;
        pagina.Formulario.ServicioIds = [1];

        Assert.IsType<PageResult>(await pagina.OnPostGuardarAsync());

        Assert.Contains("POST api/ordenes", api.Llamadas);
        Assert.Contains(pagina.ModelState[string.Empty]!.Errors, error => error.ErrorMessage.Contains("No se pudo guardar"));
        Assert.DoesNotContain(pagina.ModelState[string.Empty]!.Errors, error => error.ErrorMessage.Contains("Detalle interno"));
    }

    [Fact]
    public async Task Guardar_EnviaLaFechaDelServicioYVuelveAlListado()
    {
        var (pagina, api, _) = CrearPagina();
        api.Json("POST api/ordenes", new ResultadoOrdenDto(true, 15, 90m, false, new Dictionary<string, string>()));
        await pagina.OnGetAsync();
        pagina.Formulario.VehiculoId = 1;
        pagina.Formulario.MecanicoId = 1;
        pagina.Formulario.ServicioIds = [1];

        var redireccion = Assert.IsType<RedirectToPageResult>(await pagina.OnPostGuardarAsync());

        Assert.Equal("./Index", redireccion.PageName);
        Assert.Contains("2026-10-18", Assert.Single(api.Cuerpos));
        Assert.Equal(15, pagina.TempData["OrdenCreadaId"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, StatusCodes.Status200OK)]
    [InlineData(HttpStatusCode.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable, StatusCodes.Status503ServiceUnavailable)]
    public async Task Comprobante_TraduceLaRespuestaDelServicio(HttpStatusCode respuesta, int esperado)
    {
        var api = new ApiFalsa();
        if (respuesta == HttpStatusCode.OK) api.Archivo("GET api/ordenes/15/comprobante", "%PDF-prueba"u8.ToArray());
        else if (respuesta != HttpStatusCode.NotFound) api.Error("GET api/ordenes/15/comprobante", respuesta);
        var pagina = new ComprobanteModel(new OrdenesApi(api.Cliente()), NullLogger<ComprobanteModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

        var resultado = await pagina.OnGetAsync(15);

        if (esperado == StatusCodes.Status200OK)
            Assert.Equal("application/pdf", Assert.IsType<FileContentResult>(resultado).ContentType);
        else
            Assert.Equal(esperado, Assert.IsAssignableFrom<IStatusCodeActionResult>(resultado).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Comprobante_IdInvalido_NoLlamaAlServicio(int id)
    {
        var api = new ApiFalsa();
        var pagina = PaginaComprobante(api);

        Assert.IsType<BadRequestObjectResult>(await pagina.OnGetAsync(id));
        Assert.Empty(api.Llamadas);
    }

    [Fact]
    public async Task Comprobante_SeEntregaEnLineaConElNumeroDeOrden()
    {
        var api = new ApiFalsa();
        api.Archivo("GET api/ordenes/15/comprobante", "%PDF-prueba"u8.ToArray());
        var pagina = PaginaComprobante(api);

        Assert.IsType<FileContentResult>(await pagina.OnGetAsync(15));

        string encabezado = pagina.Response.Headers.ContentDisposition.ToString();
        Assert.Contains("inline", encabezado);
        Assert.Contains("Comprobante-Orden-15.pdf", encabezado);
    }

    [Fact]
    public async Task Comprobante_ServicioRechazaElRol_DevuelveForbid()
    {
        var api = new ApiFalsa();
        api.Error("GET api/ordenes/15/comprobante", HttpStatusCode.Forbidden);

        Assert.IsType<ForbidResult>(await PaginaComprobante(api).OnGetAsync(15));
    }

    [Fact]
    public async Task Listado_RefrescarEnviaLosMismosFiltros()
    {
        var api = new ApiFalsa();
        api.Json("GET api/ordenes", Array.Empty<OrdenResumenDto>());
        var pagina = PaginaListado(api);
        pagina.Estado = EstadoOrden.Anulada;
        pagina.Fecha = new DateOnly(2026, 5, 1);
        pagina.TerminoBusqueda = "  ABC  ";

        Assert.IsType<PageResult>(await pagina.OnGetAsync());
        Assert.IsType<PageResult>(await pagina.OnGetAsync());

        Assert.Equal(2, api.Llamadas.Count);
        Assert.All(api.Llamadas, llamada =>
            Assert.Equal("GET api/ordenes?estado=Anulada&fecha=2026-05-01&terminoBusqueda=ABC", llamada));
    }

    [Fact]
    public async Task Listado_FiltrosInvalidos_NoConsultan()
    {
        var api = new ApiFalsa();
        var pagina = PaginaListado(api);
        pagina.ModelState.AddModelError("Fecha", "Fecha inválida");

        Assert.IsType<PageResult>(await pagina.OnGetAsync());
        Assert.IsType<BadRequestObjectResult>(await pagina.OnGetSugerenciasAsync("15"));

        Assert.Empty(api.Llamadas);
    }

    [Fact]
    public async Task Listado_ServicioNoDisponible_MuestraMensajeSinDetallesTecnicos()
    {
        var api = new ApiFalsa();
        api.Error("GET api/ordenes", HttpStatusCode.ServiceUnavailable);
        var pagina = PaginaListado(api);

        Assert.IsType<PageResult>(await pagina.OnGetAsync());

        Assert.Equal("No se pudo consultar las órdenes. Intenta nuevamente.", pagina.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Detalle_IdInvalido_NoLlamaAlServicio(int id)
    {
        var api = new ApiFalsa();

        Assert.IsType<BadRequestObjectResult>(await PaginaListado(api).OnGetDetalleAsync(id));
        Assert.Empty(api.Llamadas);
    }

    [Fact]
    public async Task Detalle_Inexistente_Devuelve404()
    {
        Assert.IsType<NotFoundObjectResult>(await PaginaListado(new ApiFalsa()).OnGetDetalleAsync(999));
    }

    [Fact]
    public async Task Detalle_ServicioNoDisponible_Devuelve503SinDetallesTecnicos()
    {
        var api = new ApiFalsa();
        api.Error("GET api/ordenes/1", HttpStatusCode.ServiceUnavailable);

        var resultado = Assert.IsType<ObjectResult>(await PaginaListado(api).OnGetDetalleAsync(1));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, resultado.StatusCode);
        Assert.Equal("No se pudo consultar la orden. Intenta nuevamente.", resultado.Value);
    }

    [Fact]
    public async Task Anular_RechazoDelServicio_MuestraElErrorYRecargaElListado()
    {
        var api = new ApiFalsa();
        api.Json("POST api/ordenes/7/anular", new ResultadoOrdenDto(false, null, 0, false,
            new Dictionary<string, string> { [""] = "Solo un administrador puede anular órdenes." }));
        api.Json("GET api/ordenes", Array.Empty<OrdenResumenDto>());
        var pagina = PaginaListado(api);

        Assert.IsType<PageResult>(await pagina.OnPostAnularAsync(7));

        Assert.Equal("Solo un administrador puede anular órdenes.", pagina.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
        Assert.Contains("GET api/ordenes", api.Llamadas);
    }

    [Fact]
    public async Task Inicio_MuestraLosConteosDelServicio()
    {
        var api = new ApiFalsa();
        api.Json("GET api/dashboard", new DashboardViewModel { MecanicosDisponibles = 2, VehiculosRegistrados = 5, ServiciosRegistrados = 10 });
        var pagina = PaginaDeInicio(api);

        Assert.IsType<PageResult>(await pagina.OnGetAsync());

        Assert.Equal(5, pagina.Dashboard.VehiculosRegistrados);
        Assert.Equal(10, pagina.Dashboard.ServiciosRegistrados);
        Assert.Null(pagina.Error);
    }

    [Fact]
    public async Task Inicio_ServicioNoDisponible_MuestraMensaje()
    {
        var api = new ApiFalsa();
        api.Error("GET api/dashboard", HttpStatusCode.ServiceUnavailable);
        var pagina = PaginaDeInicio(api);

        Assert.IsType<PageResult>(await pagina.OnGetAsync());

        Assert.Equal("No se pudo consultar el resumen del taller. Intenta nuevamente.", pagina.Error);
    }

    [Fact]
    public async Task NuevaOrden_SinServicios_NoLlegaAlServicio()
    {
        var (pagina, api, _) = CrearPagina();
        await pagina.OnGetAsync();
        pagina.Formulario.VehiculoId = 1;
        pagina.Formulario.MecanicoId = 1;
        pagina.Formulario.ServicioIds = [];

        Assert.IsType<PageResult>(await pagina.OnPostGuardarAsync());

        Assert.Contains("Formulario.ServicioIds", pagina.ModelState.Keys);
        Assert.DoesNotContain("POST api/ordenes", api.Llamadas);
    }

    [Fact]
    public async Task NuevaOrden_ErrorDelServicio_SeMuestraEnElCampo()
    {
        var (pagina, api, _) = CrearPagina();
        api.Json("POST api/ordenes", new ResultadoOrdenDto(false, null, 0, false,
            new Dictionary<string, string> { ["MecanicoId"] = "El mecánico no existe." }));
        await pagina.OnGetAsync();
        pagina.Formulario.VehiculoId = 1;
        pagina.Formulario.MecanicoId = 1;
        pagina.Formulario.ServicioIds = [1];

        Assert.IsType<PageResult>(await pagina.OnPostGuardarAsync());

        Assert.Equal("El mecánico no existe.", pagina.ModelState["Formulario.MecanicoId"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public async Task NuevaOrden_ProductoSinStock_NoSeAgrega()
    {
        var (pagina, api, _) = CrearPagina();
        api.Json("GET api/productos/5", new ProductoDto { Id = 5, Nombre = "Aceite", Stock = 1 });
        pagina.ProductoId = 5;
        pagina.Cantidad = 2;

        Assert.IsType<PageResult>(await pagina.OnPostAgregarLineaAsync());

        Assert.Contains("Stock insuficiente", pagina.ModelState["Cantidad"]!.Errors.Single().ErrorMessage);
        Assert.Empty(pagina.Formulario.Lineas);
    }

    [Fact]
    public async Task NuevaOrden_BorradorVencido_VuelveAEmpezar()
    {
        var (pagina, _, _) = CrearPagina();
        pagina.BorradorId = Guid.NewGuid();

        Assert.IsType<RedirectToPageResult>(await pagina.OnGetAsync());

        Assert.Equal("El borrador venció. Inicia una orden nueva.", pagina.TempData["OrdenError"]);
    }

    private static ComprobanteModel PaginaComprobante(ApiFalsa api) =>
        new(new OrdenesApi(api.Cliente()), NullLogger<ComprobanteModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

    private static PaginaOrdenes PaginaListado(ApiFalsa api)
    {
        var contexto = new DefaultHttpContext();
        return new PaginaOrdenes(new OrdenesApi(api.Cliente()), NullLogger<PaginaOrdenes>.Instance)
        {
            PageContext = new PageContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TemporalesPrueba())
        };
    }

    private static PaginaInicio PaginaDeInicio(ApiFalsa api) =>
        new(new OrdenesApi(api.Cliente()), NullLogger<PaginaInicio>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

    private static (CrearModel Pagina, ApiFalsa Api, BorradorOrdenSesion Borradores) CrearPagina()
    {
        var contexto = new DefaultHttpContext { Session = new SesionPrueba() };
        var acceso = new HttpContextAccessor { HttpContext = contexto };
        var usuario = new UsuarioPrueba();
        var api = new ApiFalsa();
        api.Json("GET api/ordenes/fecha-actual", FechaServicio);
        api.Json("GET api/servicios/catalogo", new[]
        {
            new ServicioDto { Id = 1, Nombre = "Cambio de aceite", Descripcion = "Servicio de prueba", Costo = 30m, TiempoEstimadoHoras = 1 }
        });
        var cliente = api.Cliente();
        var borradores = new BorradorOrdenSesion(acceso, usuario);
        var pagina = new CrearModel(new OrdenesApi(cliente), new ClientesApi(cliente), new VehiculosApi(cliente),
            new MecanicosApi(cliente), new ProductosApi(cliente), new ServiciosApi(cliente), borradores,
            new TokenOrdenSesion(acceso, usuario, TimeProvider.System))
        {
            PageContext = new PageContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TemporalesPrueba())
        };
        return (pagina, api, borradores);
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
