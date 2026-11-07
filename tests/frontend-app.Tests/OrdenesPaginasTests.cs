using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Api;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Contratos.Ordenes;
using TallerMecanico.Infraestructura.Sesion;
using TallerMecanico.Pages.Ordenes;

namespace TallerMecanico.IntegrationTests;

// Páginas de órdenes con el Servicio 1 simulado: no levantan procesos ni usan la base.
[Trait("Area", "Frontend")]
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
