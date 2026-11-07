using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Api;
using System.Security.Claims;
using PaginaServicios = TallerMecanico.Pages.Servicios.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class ServiciosAutocompletadoTests
{
    [Trait("Area", "Backend")]
    [Fact]
    public async Task Pagina_UsaApiSeleccionExactaYListadoCompletoSinFiltrarEnMemoria()
    {
        var api = new ApiFalsa();
        var servicios = new List<ServicioDto> { new() { Id = 7 } };
        api.Json("GET api/servicios", servicios);
        api.Json("GET api/servicios/8", new ServicioDto { Id = 8 });
        api.Json("GET api/servicios/sugerencias", Array.Empty<SugerenciaServicio>());
        var pagina = new PaginaServicios(new ServiciosApi(api.Cliente()))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Administrador")], "Prueba"))
                }
            }
        };
        await pagina.OnGetAsync("parcial");
        Assert.Contains("GET api/servicios?terminoBusqueda=parcial", api.Llamadas);
        Assert.Single(pagina.Servicios);
        pagina.ServicioSeleccionadoId = 8;
        await pagina.OnGetAsync("texto no utilizado");
        Assert.Equal(8, Assert.Single(pagina.Servicios).Id);
        pagina.ServicioSeleccionadoId = -1;
        await pagina.OnGetAsync(null);
        Assert.Empty(pagina.Servicios);
        pagina.ServicioSeleccionadoId = null;
        servicios.Add(new ServicioDto { Id = 8 });
        await pagina.OnGetAsync("  ");
        Assert.Equal(2, pagina.Servicios.Count);
        Assert.Equal(2, api.Llamadas.Count(llamada => llamada.StartsWith("GET api/servicios?") || llamada == "GET api/servicios"));
        var resultado = Assert.IsType<JsonResult>(await pagina.OnGetSugerenciasAsync("acei"));
        Assert.NotNull(resultado.Value);
        Assert.Equal("no-store", pagina.Response.Headers.CacheControl);
    }
}
