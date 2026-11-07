using TallerMecanico.Aplicacion.CasosUso.Ordenes;
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

[Trait("Area", "API")]
[Trait("Tipo", "Navegador")]
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
            aislada.AgregarUsuario(new UsuarioSemilla
            {
                Username = rol, Rol = rol, PasswordHash = hash, EmailVerificado = true, CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
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
                foreach (string destino in new[] { "Cliente", "Vehiculo", "Producto" })
                {
                    using var peticion = new HttpRequestMessage(HttpMethod.Post, "/Ordenes/Crear?handler=Nuevo" + destino);
                    peticion.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    using var denegada = await anonimo.SendAsync(peticion);
                    Assert.Equal(HttpStatusCode.Unauthorized, denegada.StatusCode);
                }
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
                    await VerificarControlesDeAlta(cliente);
                    foreach (string tipo in tipos)
                    {
                        using var respuesta = await cliente.GetAsync($"/Ordenes/Crear?handler=Sugerencias{tipo}&termino=prue");
                        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                        Assert.True(respuesta.Headers.CacheControl?.NoStore);
                        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                        Assert.Equal(tipo == "Servicios" ? 0 : 10, json.RootElement.GetArrayLength());
                        foreach (var registro in json.RootElement.EnumerateArray())
                            Assert.Equal(tipo == "Productos"
                                ? new[] { "id", "textoPrincipal", "textoSecundario", "stock" }
                                : new[] { "id", "textoPrincipal", "textoSecundario" }, registro.EnumerateObject().Select(propiedad => propiedad.Name));
                        using var iniciales = JsonDocument.Parse(await cliente.GetStringAsync($"/Ordenes/Crear?handler=Sugerencias{tipo}"));
                        Assert.Equal(tipo switch { "Vehiculos" => 15, "Mecanicos" => 14, "Servicios" => 1, _ => 0 }, iniciales.RootElement.GetArrayLength());
                        foreach (string termino in new[] { "a", "%%", "__", "' OR 1=1 --" })
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
                            using var respuestaModulo = await cliente.SendAsync(peticion);
                            Assert.Equal(modulo == "Mecanicos" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, respuestaModulo.StatusCode);
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

    private static async Task VerificarControlesDeAlta(HttpClient cliente)
    {
        string html = await cliente.GetStringAsync("/Ordenes/Crear");
        foreach (string destino in new[] { "Cliente", "Vehiculo", "Producto" })
        {
            string idFormulario = "orden-nuevo-" + destino.ToLowerInvariant();
            Assert.Matches($"<button[^>]+form=\"{idFormulario}\"[^>]*>", html);
            string formulario = Regex.Match(html, $"<form\\b(?=[^>]*id=\"{idFormulario}\")[^>]*>.*?</form>", RegexOptions.Singleline).Value;
            Assert.Contains("method=\"post\"", formulario);
            Assert.Contains("__RequestVerificationToken", formulario);
            string accion = WebUtility.HtmlDecode(Regex.Match(formulario, "action=\"([^\"]+)\"").Groups[1].Value);
            Assert.Contains("handler=Nuevo" + destino, accion);
            var campos = Regex.Matches(formulario, "<input[^>]*>").Select(campo => new KeyValuePair<string, string>(
                WebUtility.HtmlDecode(Regex.Match(campo.Value, "name=\"([^\"]+)\"").Groups[1].Value),
                WebUtility.HtmlDecode(Regex.Match(campo.Value, "value=\"([^\"]*)\"").Groups[1].Value)))
                .Where(campo => !string.IsNullOrEmpty(campo.Key)).ToList();
            campos.RemoveAll(campo => campo.Key is "Formulario.VehiculoId" or "Formulario.MecanicoId");
            campos.AddRange(new Dictionary<string, string>
            {
                ["Formulario.VehiculoId"] = "1", ["Formulario.MecanicoId"] = "1",
                ["Formulario.ServicioIds"] = "1", ["Formulario.CantidadesServicios[1]"] = "1",
                ["Formulario.Lineas[0].ProductoId"] = "1", ["Formulario.Lineas[0].Cantidad"] = "2"
            });
            using var contenido = new FormUrlEncodedContent(campos);
            using var respuesta = await cliente.PostAsync(accion, contenido);
            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            var ubicacion = new Uri(cliente.BaseAddress!, respuesta.Headers.Location!);
            Assert.Equal($"/{(destino == "Cliente" ? "Clientes" : destino == "Vehiculo" ? "Vehiculos" : "Productos")}", ubicacion.AbsolutePath);
            string borrador = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(ubicacion.Query)["OrdenBorradorId"].ToString();
            Assert.True(Guid.TryParse(borrador, out _));
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(ubicacion)).StatusCode);
            string restaurada = await cliente.GetStringAsync("/Ordenes/Crear?BorradorId=" + borrador);
            Assert.Contains("123ABC", restaurada);
            Assert.Contains("José Muñoz", WebUtility.HtmlDecode(restaurada));
            Assert.Matches("name=\"Formulario.Lineas\\[0\\].Cantidad\"[^>]*value=\"2\"", restaurada);
            Assert.Contains("data-servicio-id=\"1\"", restaurada);
            Assert.Matches("data-total-estimado>Bs. 50[.,]00", restaurada);
            using var invalida = await cliente.PostAsync(accion, new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        }
    }

    internal static ProcessStartInfo PrepararServidor(MySqlAisladoFixture aislada, string url, string certificado, string password)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizFrontend, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(AppsPrueba.DllFrontend);
        inicio.ArgumentList.Add("--urls"); inicio.ArgumentList.Add(url);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ConnectionStrings__MySqlConnection"] = aislada.CadenaConexion;
        inicio.Environment["Kestrel__Certificates__Default__Path"] = certificado;
        inicio.Environment["Kestrel__Certificates__Default__Password"] = password;
        aislada.ConfigurarFrontend(inicio);
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
