using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TallerMecanico.Api;
using TallerMecanico.Configuracion;
using TallerMecanico.Sesion;
using PaginaVehiculos = TallerMecanico.Pages.Vehiculos.IndexModel;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Frontend")]
public sealed class VehiculosModalTests
{
    [Fact]
    public async Task ModalesReales_BuscadorDeClientesPlaceholdersYBorradores()
    {
        var api = new ApiFalsa();
        ClienteDto[] clientes = [
            new() { Id = 1, Ci = "1111111", Nombres = "Ana", PrimerApellido = "Arce" },
            new() { Id = 2, Ci = "2222222", ComplementoCi = "1A", Nombres = "Ana", PrimerApellido = "Arce" },
            new() { Id = 3, Ci = "3333333", Nombres = "Lucia", PrimerApellido = "Mamani" }
        ];
        VehiculoDto[] vehiculos = [
            new() { Id = 7, Placa = "123ABC", Marca = "Toyota", Modelo = "Corolla", ClienteId = 1 },
            new() { Id = 8, Placa = "456DEF", Marca = "Toyota", Modelo = "Hilux", ClienteId = 2 }
        ];
        api.Json("GET api/clientes", clientes);
        api.Json("GET api/clientes/sugerencias", new[] {
            new SugerenciaCliente(1, "Arce Ana", "CI: 1111111"),
            new SugerenciaCliente(2, "Arce Ana", "CI: 2222222-1A")
        });
        api.Json("GET api/vehiculos", vehiculos);
        foreach (var vehiculo in vehiculos) api.Json($"GET api/vehiculos/{vehiculo.Id}", vehiculo);
        api.Json("GET api/vehiculos/catalogo", new Dictionary<string, string[]> { ["Toyota"] = ["Corolla", "Hilux"] });
        api.Json("POST api/vehiculos", new ResultadoFormulario<VehiculoFormViewModel>(
            new() { Placa = "123ABC", Marca = "Toyota", Modelo = "Corolla", ClienteId = 2,
                Kilometraje = 45000, Observaciones = "Revisar frenos" },
            new Dictionary<string, string> { ["Placa"] = "Esta placa ya está registrada." }));
        api.Json("POST api/clientes", new ResultadoFormulario<ClienteFormViewModel>(
            new() { Id = 3 }, new Dictionary<string, string>()));
        api.Json("PUT api/vehiculos/8", new ResultadoFormulario<VehiculoFormViewModel>(
            new() { Id = 8, ClienteId = 3 }, new Dictionary<string, string>()));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PaginaVehiculos).Assembly.GetName().Name,
            ContentRootPath = MySqlAisladoFixture.RaizFrontend, EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddRazorPages().AddApplicationPart(typeof(PaginaVehiculos).Assembly);
        builder.Services.AddSingleton(ConfiguracionTaller.Instancia);
        builder.Services.AddSingleton(new VehiculosApi(api.Cliente()));
        builder.Services.AddSingleton(new ClientesApi(api.Cliente()));
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<TallerMecanico.Seguridad.ICurrentUser>(new UsuarioPrueba());
        builder.Services.AddScoped<IBorradorVehiculoPort, BorradorVehiculoSesion>();
        builder.Services.AddScoped<IBorradorOrden, BorradorOrdenSesion>();
        await using var app = builder.Build();
        app.UseStaticFiles();
        app.UseSession();
        app.Use(async (contexto, siguiente) =>
        {
            contexto.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Name, "recepcion_prueba"), new Claim(ClaimTypes.Role, "Recepcionista")
            ], "Prueba"));
            await siguiente();
        });
        app.MapRazorPages();
        await app.StartAsync();

        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        string html = WebUtility.HtmlDecode(await http.GetStringAsync("/Vehiculos"));
        foreach (string modal in new[] { "crear", "editar" })
        {
            Assert.Contains($"id=\"{modal}-cliente-autocompletado-texto\"", html);
            Assert.Contains($"id=\"{modal}-cliente-autocompletado-id\"", html);
        }
        Assert.Contains("Busca por CI, nombres o apellidos...", html);
        using var respuesta = await http.GetAsync("/Vehiculos?handler=SugerenciasClientes&termino=222");
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        var sugerencias = JsonSerializer.Deserialize<SugerenciaCliente[]>(await respuesta.Content.ReadAsStringAsync(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("CI: 2222222-1A", sugerencias![1].TextoSecundario);
        Assert.Contains("GET api/clientes/sugerencias?termino=222", api.Llamadas);

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
        {
            var inicio = new ProcessStartInfo("node")
            {
                WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            inicio.ArgumentList.Add("tests/vehiculos_clientes_modal_ui.cjs");
            inicio.Environment["TALLER_VEHICULOS_MODAL_URL"] = app.Urls.Single();
            using var proceso = Process.Start(inicio)!;
            Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), errores = proceso.StandardError.ReadToEndAsync();
            using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try { await proceso.WaitForExitAsync(limite.Token); }
            catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
            Assert.True(proceso.ExitCode == 0, await salida + await errores);
            var guardado = JsonSerializer.Deserialize<VehiculoFormViewModel>(api.Cuerpos.Last(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Equal(3, guardado!.ClienteId);
            Assert.Equal(8, guardado.Id);
        }
    }
}
