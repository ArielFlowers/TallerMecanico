using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class ServiciosAutocompletadoHttpTests
{
    [MySqlAisladoFact]
    public async Task ServiciosHttps_SugerenciasBusquedaSqlCrudYPermisosAdministrativos()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = ServiciosAutocompletadoTests.CrearServicio(aislada);
        int id = ServiciosAutocompletadoTests.Crear(servicio, "Cambio de Aceite", "Lubricación del motor");
        ServiciosAutocompletadoTests.Crear(servicio, "Cambio de Aceite", "Cambio del filtro");
        ServiciosAutocompletadoTests.Crear(servicio, "Diagnóstico Electrónico", "Revisión de sensores");
        for (int indice = 0; indice < 12; indice++)
            ServiciosAutocompletadoTests.Crear(servicio, $"Inspección General {indice:00}", "Revisión del motor");
        string password = Guid.NewGuid().ToString("N") + "_Prueba";
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        string hash = hasher.GenerarHash(password);
        foreach (string rol in new[] { "Administrador", "Recepcionista" })
            new UsuarioRepository(aislada.Factory).Agregar(new Usuario
            {
                Username = rol, Rol = rol, PasswordHash = hash, CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
            });

        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var solicitud = new CertificateRequest("CN=localhost", clave, HashAlgorithmName.SHA256);
        var nombres = new SubjectAlternativeNameBuilder();
        nombres.AddIpAddress(IPAddress.Loopback);
        solicitud.CertificateExtensions.Add(nombres.Build());
        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        string rutaCertificado = Path.Combine(Path.GetTempPath(), $"servicios_busqueda_{Guid.NewGuid():N}.pfx");
        string claveCertificado = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(rutaCertificado, certificado.Export(X509ContentType.Pfx, claveCertificado));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string url = $"https://127.0.0.1:{puerto}";
        try
        {
            using var servidor = Process.Start(PrepararServidor(aislada, url, rutaCertificado, claveCertificado))!;
            Task<string> salida = servidor.StandardOutput.ReadToEndAsync(), errores = servidor.StandardError.ReadToEndAsync();
            try
            {
                using var anonimo = NuevoCliente(url, new CookieContainer());
                bool listo = false;
                for (int intento = 0; intento < 60 && !servidor.HasExited; intento++)
                {
                    try { listo = (await anonimo.GetAsync("/css/site.css")).IsSuccessStatusCode; }
                    catch (HttpRequestException) { }
                    catch (TaskCanceledException) { }
                    if (listo) break;
                    await Task.Delay(250);
                }
                Assert.True(listo, "El servidor HTTPS aislado no inició.");
                await ComprobarDenegacion(anonimo, HttpStatusCode.Unauthorized);
                var cookies = new CookieContainer();
                using var admin = NuevoCliente(url, cookies);
                await IniciarSesion(admin, "Administrador", password);
                using var recepcionista = NuevoCliente(url, new CookieContainer());
                await IniciarSesion(recepcionista, "Recepcionista", password);
                await ComprobarDenegacion(recepcionista, HttpStatusCode.Forbidden);

                string antes = JsonSerializer.Serialize(servicio.ObtenerTodos());
                using var respuesta = await admin.GetAsync("/Servicios?handler=Sugerencias&termino=revision");
                Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                Assert.True(respuesta.Headers.CacheControl?.NoStore);
                Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
                using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                Assert.Equal(10, json.RootElement.GetArrayLength());
                foreach (var registro in json.RootElement.EnumerateArray())
                    Assert.Equal(new[] { "id", "textoPrincipal", "textoSecundario" }, registro.EnumerateObject().Select(propiedad => propiedad.Name));
                foreach (string termino in new[] { "", " ", "a", "' OR 1=1 --", "%%", "__" })
                    Assert.Equal("[]", await admin.GetStringAsync("/Servicios?handler=Sugerencias&termino=" + Uri.EscapeDataString(termino)));
                string listado = await admin.GetStringAsync($"/Servicios?buscar=Cambio&ServicioSeleccionadoId={id}");
                Assert.Equal(1, ContarFilas(listado));
                Assert.Equal(2, ContarFilas(await admin.GetStringAsync("/Servicios?buscar=ambio")));
                Assert.Equal(13, ContarFilas(await admin.GetStringAsync("/Servicios?buscar=evisi")));
                Assert.Equal(15, ContarFilas(await admin.GetStringAsync("/Servicios")));
                Assert.Equal(0, ContarFilas(await admin.GetStringAsync("/Servicios?ServicioSeleccionadoId=-1")));
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
                    await VerificarNavegador(url, cookies);
                Assert.Equal(antes, JsonSerializer.Serialize(servicio.ObtenerTodos()));
                Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));

                string crear = await admin.GetStringAsync("/Servicios/Create");
                using var invalido = await Enviar(admin, "/Servicios/Create", crear, "", "", "0");
                Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
                Assert.Equal(15, servicio.ObtenerTodos().Count);
                using var creado = await Enviar(admin, "/Servicios/Create", crear, "Servicio Nuevo", "Prueba de creación", "35");
                Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
                var nuevo = Assert.Single(servicio.ObtenerTodos(), registro => registro.Nombre == "Servicio Nuevo");
                Assert.Equal("Administrador", nuevo.CreadoPor);
                string editar = await admin.GetStringAsync($"/Servicios/Edit?id={nuevo.Id}");
                using var actualizado = await Enviar(admin, $"/Servicios/Edit?id={nuevo.Id}", editar, "Servicio Editado", "Prueba de edición", "40");
                Assert.Equal(HttpStatusCode.Redirect, actualizado.StatusCode);
                Assert.Equal(40m, servicio.ObtenerPorId(nuevo.Id)!.Costo);
                Assert.Equal("Administrador", servicio.ObtenerPorId(nuevo.Id)!.ModificadoPor);
                string eliminar = await admin.GetStringAsync($"/Servicios/Delete?id={nuevo.Id}");
                using var eliminado = await Enviar(admin, $"/Servicios/Delete?id={nuevo.Id}", eliminar, "", "", "0");
                Assert.Equal(HttpStatusCode.Redirect, eliminado.StatusCode);
                Assert.Null(servicio.ObtenerPorId(nuevo.Id));
            }
            finally
            {
                if (!servidor.HasExited) servidor.Kill(entireProcessTree: true);
                await servidor.WaitForExitAsync();
                await salida;
                await errores;
            }
        }
        finally { File.Delete(rutaCertificado); }
    }

    private static int ContarFilas(string html) => Regex.Matches(html, "class=\"service-name\"").Count;

    private static async Task ComprobarDenegacion(HttpClient cliente, HttpStatusCode esperado)
    {
        foreach (string ruta in new[] { "/Servicios", "/Servicios/Create", "/Servicios?handler=Sugerencias&termino=acei" })
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
            peticion.Headers.Add("X-Requested-With", "XMLHttpRequest");
            using var respuesta = await cliente.SendAsync(peticion);
            Assert.Equal(esperado, respuesta.StatusCode);
        }
    }

    private static ProcessStartInfo PrepararServidor(MySqlAisladoFixture aislada, string url, string certificado, string password)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(typeof(ServicioService).Assembly.Location);
        inicio.ArgumentList.Add("--urls");
        inicio.ArgumentList.Add(url);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ConnectionStrings__MySqlConnection"] = aislada.CadenaConexion;
        inicio.Environment["Kestrel__Certificates__Default__Path"] = certificado;
        inicio.Environment["Kestrel__Certificates__Default__Password"] = password;
        return inicio;
    }

    private static HttpClient NuevoCliente(string url, CookieContainer cookies) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, CookieContainer = cookies, ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(3) };

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(
        Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]*>").Value,
        "value=\"([^\"]+)\"").Groups[1].Value);

    private static async Task IniciarSesion(HttpClient cliente, string usuario, string password)
    {
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(await cliente.GetStringAsync("/Auth/Login")),
            ["Input.Username"] = usuario, ["Input.Password"] = password
        });
        using var respuesta = await cliente.PostAsync("/Auth/Login", contenido);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    private static async Task<HttpResponseMessage> Enviar(HttpClient cliente, string ruta, string html, string nombre, string descripcion, string costo)
    {
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["Formulario.Nombre"] = nombre,
            ["Formulario.Descripcion"] = descripcion, ["Formulario.Costo"] = costo, ["Formulario.TiempoEstimadoHoras"] = "1"
        });
        return await cliente.PostAsync(ruta, contenido);
    }

    private static async Task VerificarNavegador(string url, CookieContainer cookies)
    {
        var inicio = new ProcessStartInfo("node")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add("tests/servicios_autocompletado_ui.cjs");
        inicio.Environment["TALLER_SERVICIOS_URL"] = url;
        inicio.Environment["TALLER_SERVICIOS_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
            .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await proceso.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
        Assert.True(proceso.ExitCode == 0, await salida + await errores);
    }
}
