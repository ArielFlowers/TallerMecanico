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

public sealed class OrdenesAutocompletadoHttpTests
{
    [MySqlAisladoFact]
    public async Task OrdenesHttps_BuscadorFiltrosPermisosDetallePdfYSinEscrituras()
    {
        using var aislada = new MySqlAisladoFixture();
        OrdenesAutocompletadoTests.CrearDatos(aislada);
        aislada.Ejecutar("UPDATE Productos SET Precio = 999.99;");
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
        string rutaCertificado = Path.Combine(Path.GetTempPath(), $"ordenes_busqueda_{Guid.NewGuid():N}.pfx");
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
                using var peticion = new HttpRequestMessage(HttpMethod.Get, "/Ordenes?handler=Sugerencias&termino=15");
                peticion.Headers.Add("X-Requested-With", "XMLHttpRequest");
                using var denegada = await anonimo.SendAsync(peticion);
                Assert.Equal(HttpStatusCode.Unauthorized, denegada.StatusCode);
                string antes = OrdenesAutocompletadoTests.Instantanea(aislada);
                CookieContainer? cookiesAdministrador = null;
                string? listadoAdministrador = null;
                foreach (string rol in new[] { "Administrador", "Recepcionista" })
                {
                    var cookies = new CookieContainer();
                    using var cliente = NuevoCliente(url, cookies);
                    await IniciarSesion(cliente, rol, password);
                    using var respuesta = await cliente.GetAsync("/Ordenes?handler=Sugerencias&termino=123&Estado=Activa&Fecha=2026-10-12");
                    Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                    Assert.True(respuesta.Headers.CacheControl?.NoStore);
                    Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
                    using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                    Assert.Equal(new[] { 15, 1 }, json.RootElement.EnumerateArray().Select(orden => orden.GetProperty("id").GetInt32()));
                    foreach (var orden in json.RootElement.EnumerateArray())
                        Assert.Equal(new[] { "id", "textoPrincipal", "textoSecundario" }, orden.EnumerateObject().Select(propiedad => propiedad.Name));
                    foreach (string termino in new[] { "", " ", "1", "%%", "__", "' OR 1=1 --" })
                        Assert.Equal("[]", await cliente.GetStringAsync("/Ordenes?handler=Sugerencias&termino=" + Uri.EscapeDataString(termino)));
                    Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/Ordenes?handler=Sugerencias&termino=15&Estado=Invalido")).StatusCode);
                    string seleccion = "/Ordenes?TerminoBusqueda=Orden%20%231&OrdenSeleccionadaId=1&Estado=Activa&Fecha=2026-10-12";
                    string listado = await cliente.GetStringAsync(seleccion);
                    if (rol == "Administrador")
                    {
                        cookiesAdministrador = cookies;
                        listadoAdministrador = listado;
                    }
                    Assert.Equal(1, ContarFilas(listado));
                    Assert.Equal(rol == "Administrador", listado.Contains("aria-label=\"Anular orden 1\""));
                    Assert.Equal(0, ContarFilas(await cliente.GetStringAsync(seleccion.Replace("Estado=Activa", "Estado=Anulada"))));
                    Assert.Equal(4, ContarFilas(await cliente.GetStringAsync("/Ordenes?TerminoBusqueda=1")));
                    Assert.Equal(1, ContarFilas(await cliente.GetStringAsync("/Ordenes?TerminoBusqueda=15&Estado=Anulada&Fecha=2026-10-12")));
                    Assert.Equal(4, ContarFilas(await cliente.GetStringAsync("/Ordenes")));
                    string detalle = WebUtility.HtmlDecode(await cliente.GetStringAsync("/Ordenes?handler=Detalle&id=1"));
                    Assert.Contains("Ana María Arce Pérez", detalle);
                    Assert.Contains("Bs. 31,00", detalle);
                    Assert.DoesNotContain("999,99", detalle);
                    using var pdf = await cliente.GetAsync("/Ordenes/Comprobante?id=1");
                    Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
                    Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
                    byte[] documento = await pdf.Content.ReadAsByteArrayAsync();
                    Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(documento, 0, 8));
                    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_PDF_PYTHON")))
                    {
                        string texto = SoporteComprobantePdf.ExtraerTexto(documento);
                        Assert.Contains("Son Treinta y Un Bolivianos.", texto);
                        Assert.Contains($"Copia generada por: {rol}", texto);
                    }
                    if (rol == "Recepcionista")
                    {
                        string formulario = await cliente.GetStringAsync("/Ordenes/Crear");
                        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["__RequestVerificationToken"] = Token(formulario), ["Estado"] = "Activa", ["Fecha"] = "2026-10-12",
                            ["OrdenSeleccionadaId"] = "1", ["TerminoBusqueda"] = "Orden #1"
                        });
                        using var anulacion = await cliente.PostAsync("/Ordenes?handler=Anular&id=1", contenido);
                        Assert.Equal(HttpStatusCode.OK, anulacion.StatusCode);
                        Assert.Contains("Solo un administrador", await anulacion.Content.ReadAsStringAsync());
                        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
                            await VerificarNavegador(url, cookies);
                    }
                }
                Assert.Equal(antes, OrdenesAutocompletadoTests.Instantanea(aislada));

                using var administrador = NuevoCliente(url, cookiesAdministrador!);
                using var formularioAnular = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = Token(listadoAdministrador!), ["Estado"] = "Activa", ["Fecha"] = "2026-10-12",
                    ["OrdenSeleccionadaId"] = "1", ["TerminoBusqueda"] = "Orden #1"
                });
                decimal stockAntesDeAnular = Convert.ToDecimal(aislada.Escalar("SELECT SUM(Stock) FROM Productos;"));
                using var anulada = await administrador.PostAsync("/Ordenes?handler=Anular&id=1", formularioAnular);
                Assert.Equal(HttpStatusCode.Redirect, anulada.StatusCode);
                string retorno = anulada.Headers.Location!.ToString();
                Assert.Contains("Estado=Activa", retorno);
                Assert.Contains("Fecha=2026-10-12", retorno);
                Assert.Contains("OrdenSeleccionadaId=1", retorno);
                Assert.Contains("TerminoBusqueda=", retorno);
                Assert.Equal(0, ContarFilas(await administrador.GetStringAsync(retorno)));
                Assert.Equal(stockAntesDeAnular + 2, Convert.ToDecimal(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
                Assert.Equal(31m, Convert.ToDecimal(aislada.Escalar("SELECT Total FROM Ordenes WHERE Id = 1;")));
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

    private static int ContarFilas(string html) => Regex.Matches(html, "data-ver-orden").Count;

    private static ProcessStartInfo PrepararServidor(MySqlAisladoFixture aislada, string url, string certificado, string password)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(typeof(ConsultaOrdenesService).Assembly.Location);
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
    }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(5) };

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

    private static async Task VerificarNavegador(string url, CookieContainer cookies)
    {
        var inicio = new ProcessStartInfo("node")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add("tests/ordenes_autocompletado_ui.cjs");
        inicio.Environment["TALLER_ORDENES_URL"] = url;
        inicio.Environment["TALLER_ORDENES_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
            .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await proceso.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
        Assert.True(proceso.ExitCode == 0, await salida + await errores);
    }
}
