using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TallerMecanico.Api;
using TallerMecanico.Configuracion;
using TallerMecanico.Contratos.Ordenes;
using TallerMecanico.Pages.Ordenes;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class OrdenesVistasTests
{
    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    public async Task ListadoYDetalle_OcultanIdYConservanLasAcciones(string rol)
    {
        const int id = 8123;
        var api = new ApiFalsa();
        api.Json("GET api/ordenes", new[]
        {
            new OrdenResumenDto(id, new DateTime(2026, 11, 13), "1234ABC", "Ana Prueba",
                "José Prueba", "Cambio de aceite", 250m, EstadoOrden.Activa, "admin_prueba")
        });
        api.Json($"GET api/ordenes/{id}", new OrdenDetalleDto
        {
            Id = id, Placa = "1234ABC", NombreCliente = "Ana Prueba", NombreMecanico = "José Prueba",
            Fecha = new DateTime(2026, 11, 13), FechaCreacion = new DateTime(2026, 11, 13, 12, 0, 0),
            Total = 250m, Productos = [new("FILTRO", "Filtro de aceite", 1, 10m, 10m)]
        });
        api.Archivo($"GET api/ordenes/{id}/comprobante", "%PDF-prueba"u8.ToArray());
        api.Json("GET api/ordenes/sugerencias", new[] { new SugerenciaOrden(id, $"Orden #{id}", "1234ABC · Ana Prueba") });
        api.Json($"POST api/ordenes/{id}/anular", new ResultadoOrdenDto(true, id, 250m, false,
            new Dictionary<string, string>()));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(IndexModel).Assembly.GetName().Name,
            ContentRootPath = MySqlAisladoFixture.RaizFrontend,
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddRazorPages().AddApplicationPart(typeof(IndexModel).Assembly);
        builder.Services.AddSingleton(ConfiguracionTaller.Instancia);
        builder.Services.AddSingleton(new OrdenesApi(api.Cliente()));
        await using var app = builder.Build();
        app.Use(async (contexto, siguiente) =>
        {
            contexto.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "usuario_prueba"), new Claim(ClaimTypes.Role, rol)], "Prueba"));
            if (contexto.Request.Query.ContainsKey("confirmacion"))
                contexto.RequestServices.GetRequiredService<ITempDataDictionaryFactory>()
                    .GetTempData(contexto)["OrdenCreadaId"] = id;
            await siguiente();
        });
        app.MapRazorPages();
        await app.StartAsync();
        using var cliente = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        string html = WebUtility.HtmlDecode(await cliente.GetStringAsync("/Ordenes?confirmacion=1"));
        string tabla = Regex.Match(html, "<table.*?</table>", RegexOptions.Singleline).Value;
        Assert.Equal(9, Regex.Matches(tabla, "<th scope=\"col\">").Count);
        Assert.Equal(9, Regex.Matches(tabla, "<td(?:>| )").Count);
        Assert.DoesNotContain("ordenes-id", tabla);
        string texto = Regex.Replace(html, "<[^>]*>", "");
        Assert.DoesNotContain(id.ToString(), texto);
        Assert.Contains("Orden guardada.", texto);
        Assert.Contains("Ana Prueba", texto);
        string rutaDetalle = Regex.Match(html, "data-detalle-url=\"([^\"]+)\"").Groups[1].Value;
        var urlDetalle = new Uri(cliente.BaseAddress!, rutaDetalle);
        Assert.Equal("/Ordenes", urlDetalle.AbsolutePath);
        var parametrosDetalle = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(urlDetalle.Query);
        Assert.Equal("Detalle", parametrosDetalle["handler"].ToString());
        Assert.Equal(id.ToString(), parametrosDetalle["id"].ToString());
        Assert.Contains($"data-comprobante-url=\"/Ordenes/Comprobante?id={id}\"", html);
        Assert.Contains("data-comprobante-creado", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains(">Refrescar</button>", html);
        Assert.Contains(">Limpiar filtros</a>", html);
        Assert.Equal(rol == "Administrador", html.Contains($"data-orden-id=\"{id}\""));

        var sugerencias = System.Text.Json.JsonSerializer.Deserialize<SugerenciaOrden[]>(
            await cliente.GetStringAsync("/Ordenes?handler=Sugerencias&termino=123"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var sugerencia = Assert.Single(sugerencias!);
        Assert.Equal(id, sugerencia.Id);
        Assert.Equal("1234ABC · Ana Prueba", sugerencia.TextoPrincipal);
        Assert.Empty(sugerencia.TextoSecundario);

        string detalle = WebUtility.HtmlDecode(await cliente.GetStringAsync(rutaDetalle));
        Assert.Contains("Información general de la orden</h3>", detalle);
        Assert.DoesNotContain(id.ToString(), detalle);
        Assert.Contains("Filtro de aceite", detalle);
        Assert.Contains("Ana Prueba", detalle);
        using var pdf = await cliente.GetAsync($"/Ordenes/Comprobante?id={id}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Contains($"GET api/ordenes/{id}", api.Llamadas);
        Assert.Contains($"GET api/ordenes/{id}/comprobante", api.Llamadas);

        if (rol == "Administrador")
        {
            string token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            using var respuesta = await cliente.PostAsync($"/Ordenes?handler=Anular&id={id}",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
            respuesta.EnsureSuccessStatusCode();
            string confirmacion = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
            Assert.Contains("Orden anulada correctamente. Se restituyó el stock.", confirmacion);
            Assert.DoesNotContain($"orden #{id}", confirmacion, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"POST api/ordenes/{id}/anular", api.Llamadas);
        }
    }
}
