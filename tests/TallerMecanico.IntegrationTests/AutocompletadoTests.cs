using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TallerMecanico.Pages.Shared;

namespace TallerMecanico.IntegrationTests;

public sealed class AutocompletadoNavegadorFactAttribute : FactAttribute
{
    public AutocompletadoNavegadorFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
            Skip = "Configura PLAYWRIGHT_MODULE para ejecutar el componente en Edge sin MySQL.";
    }
}

public sealed class AutocompletadoTests
{
    [Fact]
    public void Configuracion_ConservaLimitesYModoPredeterminado()
    {
        var modelo = new BuscadorAutocompletadoModel { Id = "prueba", UrlSugerencias = "/sugerencias" };

        Assert.Equal("filtrar", modelo.Modo);
        Assert.Null(modelo.IdSeleccionado);
        Assert.Null(modelo.NombreCampoId);
        Assert.Equal(2, BuscadorAutocompletadoModel.MinimoCaracteres);
        Assert.Equal(300, BuscadorAutocompletadoModel.EsperaMilisegundos);
        Assert.Equal(10, BuscadorAutocompletadoModel.MaximoSugerencias);
    }

    [AutocompletadoNavegadorFact]
    public async Task PartialReal_ComportamientoAccesibleConRespuestasSimuladasEnEdge()
    {
        string raiz = MySqlAisladoFixture.RaizProyecto;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BuscadorAutocompletadoModel).Assembly.GetName().Name,
            ContentRootPath = raiz, EnvironmentName = "Production"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddRazorPages().AddApplicationPart(typeof(BuscadorAutocompletadoModel).Assembly);
        await using var app = builder.Build();
        app.UseStaticFiles();
        // Este host solo existe dentro de la prueba; no ejecuta Program ni registra persistencia.
        app.MapGet("/", RenderizarPagina);
        await app.StartAsync();
        try
        {
            var inicio = new ProcessStartInfo("node")
            {
                WorkingDirectory = raiz, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            inicio.ArgumentList.Add("tests/autocompletado_ui.cjs");
            inicio.Environment["TALLER_AUTOCOMPLETADO_URL"] = app.Urls.Single();
            using var proceso = Process.Start(inicio)!;
            Task<string> salida = proceso.StandardOutput.ReadToEndAsync();
            Task<string> errores = proceso.StandardError.ReadToEndAsync();
            using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try { await proceso.WaitForExitAsync(limite.Token); }
            catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
            Assert.True(proceso.ExitCode == 0, await salida + await errores);
        }
        finally { await app.StopAsync(); }
    }

    private static async Task RenderizarPagina(HttpContext contexto)
    {
        var filtro = await RenderizarBuscador(contexto, new BuscadorAutocompletadoModel
        {
            Id = "filtro", UrlSugerencias = "/sugerencias?grupo=filtro",
            Etiqueta = "Filtrar registros", NombreCampoId = "RegistroId"
        });
        var seleccion = await RenderizarBuscador(contexto, new BuscadorAutocompletadoModel
        {
            Id = "seleccion", UrlSugerencias = "/sugerencias?grupo=seleccion",
            Modo = BuscadorAutocompletadoModel.ModoSeleccionar, Etiqueta = "Seleccionar registro",
            NombreCampoTexto = "Etiqueta", NombreCampoId = "RegistroSeleccionadoId",
            IdSeleccionado = 42, Texto = "Registro inicial", MensajeSinResultados = "No hay sugerencias de prueba."
        });
        contexto.Response.ContentType = "text/html; charset=utf-8";
        await contexto.Response.WriteAsync($$"""
            <!doctype html><html lang="es"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Prueba aislada de autocompletado</title>
            <link rel="stylesheet" href="/css/site.css">
            <link rel="stylesheet" href="/css/autocompletado.css">
            <style>main{max-width:700px;margin:24px auto;padding:16px}form{margin-bottom:32px}</style>
            <script src="/js/autocompletado.js" defer></script></head><body><main>
            <h1>Buscador compartido</h1><form id="form-filtro">{{filtro}}</form>
            <form id="form-seleccion">{{seleccion}}</form><button id="fuera">Fuera del buscador</button>
            </main></body></html>
            """, Encoding.UTF8);
    }

    private static async Task<string> RenderizarBuscador(HttpContext contexto, BuscadorAutocompletadoModel modelo)
    {
        var servicios = contexto.RequestServices;
        var motor = servicios.GetRequiredService<IRazorViewEngine>();
        var vista = motor.GetView(null, "/Pages/Shared/_BuscadorAutocompletado.cshtml", false);
        Assert.True(vista.Success, string.Join(", ", vista.SearchedLocations ?? []));
        var datos = new ViewDataDictionary<BuscadorAutocompletadoModel>(
            servicios.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = modelo };
        var temporales = new TempDataDictionary(contexto, servicios.GetRequiredService<ITempDataProvider>());
        using var escritor = new StringWriter();
        var accion = new ActionContext(contexto, new RouteData(), new ActionDescriptor());
        await vista.View.RenderAsync(new ViewContext(accion, vista.View, datos, temporales, escritor, new HtmlHelperOptions()));
        return escritor.ToString();
    }
}
