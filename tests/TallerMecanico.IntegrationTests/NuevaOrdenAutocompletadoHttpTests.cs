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

public sealed class NuevaOrdenAutocompletadoHttpTests
{
    [MySqlAisladoFact]
    public async Task NuevaOrdenHttps_CuatroConsultasPermisosBorradoresYCreacionReal()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada, masivos: true);
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
        string rutaCertificado = Path.Combine(Path.GetTempPath(), $"nueva_orden_{Guid.NewGuid():N}.pfx");
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
                string[] tipos = ["Vehiculos", "Servicios", "Mecanicos", "Productos"];
                foreach (string tipo in tipos)
                {
                    using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/Ordenes/Crear?handler=Sugerencias{tipo}&termino=prue");
                    peticion.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    using var respuesta = await anonimo.SendAsync(peticion);
                    Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
                }
                foreach (string handler in new[] { "ServicioSeleccionado", "VehiculoSeleccionado" })
                {
                    using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/Ordenes/Crear?handler={handler}&id=1");
                    peticion.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    using var respuesta = await anonimo.SendAsync(peticion);
                    Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
                }
                string antes = NuevaOrdenAutocompletadoTests.Instantanea(aislada);
                CookieContainer? recepcionista = null;
                foreach (string rol in new[] { "Administrador", "Recepcionista" })
                {
                    var cookies = new CookieContainer();
                    using var cliente = NuevoCliente(url, cookies);
                    await IniciarSesion(cliente, rol, password);
                    if (rol == "Recepcionista") recepcionista = cookies;
                    foreach (string tipo in tipos)
                    {
                        using var respuesta = await cliente.GetAsync($"/Ordenes/Crear?handler=Sugerencias{tipo}&termino=prue");
                        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                        Assert.True(respuesta.Headers.CacheControl?.NoStore);
                        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                        Assert.Equal(10, json.RootElement.GetArrayLength());
                        foreach (var registro in json.RootElement.EnumerateArray())
                            Assert.Equal(new[] { "id", "textoPrincipal", "textoSecundario" }, registro.EnumerateObject().Select(propiedad => propiedad.Name));
                        foreach (string termino in new[] { "", "a", "%%", "__", "' OR 1=1 --" })
                            Assert.Equal("[]", await cliente.GetStringAsync($"/Ordenes/Crear?handler=Sugerencias{tipo}&termino=" + Uri.EscapeDataString(termino)));
                    }
                    using var costo = await cliente.GetAsync("/Ordenes/Crear?handler=ServicioSeleccionado&id=1");
                    Assert.Equal(HttpStatusCode.OK, costo.StatusCode);
                    using var jsonCosto = JsonDocument.Parse(await costo.Content.ReadAsStringAsync());
                    Assert.Equal(25m, jsonCosto.RootElement.GetProperty("costo").GetDecimal());
                    if (rol == "Recepcionista")
                        foreach (string modulo in new[] { "Mecanicos", "Servicios" })
                        {
                            using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/{modulo}?handler=Sugerencias&termino=prue");
                            peticion.Headers.Add("X-Requested-With", "XMLHttpRequest");
                            using var denegada = await cliente.SendAsync(peticion);
                            Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);
                        }
                }
                Assert.Equal(antes, NuevaOrdenAutocompletadoTests.Instantanea(aislada));
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
                {
                    await VerificarNavegador(url, recepcionista!);
                    Assert.Equal(1L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
                    Assert.Equal(74.60m, Convert.ToDecimal(aislada.Escalar("SELECT Total FROM Ordenes;")));
                    Assert.Equal(8L, Convert.ToInt64(aislada.Escalar("SELECT Stock FROM Productos WHERE Id=1;")));
                    Assert.Equal(7L, Convert.ToInt64(aislada.Escalar("SELECT Stock FROM Productos WHERE Id=2;")));
                    Assert.Equal(4L, Convert.ToInt64(aislada.Escalar("SELECT Stock FROM Productos WHERE Codigo='NVO-001';")));
                    Assert.Equal("Recepcionista", aislada.Escalar("SELECT CreadoPor FROM Ordenes;"));
                }
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

    internal static ProcessStartInfo PrepararServidor(MySqlAisladoFixture aislada, string url, string certificado, string password)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(typeof(OrdenServicioService).Assembly.Location);
        inicio.ArgumentList.Add("--urls"); inicio.ArgumentList.Add(url);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ConnectionStrings__MySqlConnection"] = aislada.CadenaConexion;
        inicio.Environment["Kestrel__Certificates__Default__Path"] = certificado;
        inicio.Environment["Kestrel__Certificates__Default__Password"] = password;
        return inicio;
    }
    internal static HttpClient NuevoCliente(string url, CookieContainer cookies) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, CookieContainer = cookies, ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(5) };

    internal static async Task IniciarSesion(HttpClient cliente, string usuario, string password)
    {
        string html = await cliente.GetStringAsync("/Auth/Login");
        string campo = Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]*>").Value;
        string token = WebUtility.HtmlDecode(Regex.Match(campo, "value=\"([^\"]+)\"").Groups[1].Value);
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["Input.Username"] = usuario, ["Input.Password"] = password });
        using var respuesta = await cliente.PostAsync("/Auth/Login", contenido);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }
    private static async Task VerificarNavegador(string url, CookieContainer cookies)
    {
        var inicio = new ProcessStartInfo("node")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add("tests/nueva_orden_autocompletado_ui.cjs");
        inicio.Environment["TALLER_NUEVA_ORDEN_URL"] = url;
        inicio.Environment["TALLER_NUEVA_ORDEN_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
            .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await proceso.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
        Assert.True(proceso.ExitCode == 0, await salida + await errores);
    }
}
