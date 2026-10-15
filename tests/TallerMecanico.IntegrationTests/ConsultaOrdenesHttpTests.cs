using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.IntegrationTests;

public sealed class ConsultaOrdenesHttpTests
{
    [MySqlAisladoFact]
    public async Task ListadoHttps_AutorizaLosRolesYConservaFiltrosAlRefrescar()
    {
        using var aislada = new MySqlAisladoFixture();
        int activa = ConsultaOrdenesMySqlTests.CrearOrden(aislada, new DateTime(2026, 10, 12));
        int anulada = ConsultaOrdenesMySqlTests.CrearOrden(aislada, new DateTime(2026, 10, 12), EstadoOrden.Anulada);
        ConsultaOrdenesMySqlTests.CrearOrden(aislada, new DateTime(2026, 10, 13));
        aislada.Ejecutar("UPDATE Productos SET Precio = 999.99, Nombre = '<script>window.modalInjected = true</script>';");
        aislada.Ejecutar("UPDATE Clientes SET ComplementoCi = '1A'; UPDATE Mecanicos SET ComplementoCi = '2B';");
        string password = $"Ordenes_{Guid.NewGuid():N}";
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        string passwordHash = hasher.GenerarHash(password);
        foreach (string rol in new[] { "Administrador", "Recepcionista" })
        {
            new UsuarioRepository(aislada.Factory).Agregar(new Usuario
            {
                Username = rol, PasswordHash = passwordHash, Rol = rol,
                CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
            });
        }

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string url = $"https://127.0.0.1:{puerto}";
        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var solicitud = new CertificateRequest("CN=localhost", clave, HashAlgorithmName.SHA256);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        solicitud.CertificateExtensions.Add(san.Build());
        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        string rutaCertificado = Path.Combine(Path.GetTempPath(), $"autotaller_ordenes_{Guid.NewGuid():N}.pfx");
        string passwordCertificado = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(rutaCertificado, certificado.Export(X509ContentType.Pfx, passwordCertificado));

        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(Path.Combine(MySqlAisladoFixture.RaizProyecto, "bin/Debug/net10.0/TallerMecanico.dll"));
        inicio.ArgumentList.Add("--urls");
        inicio.ArgumentList.Add(url);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ConnectionStrings__MySqlConnection"] = aislada.CadenaConexion;
        inicio.Environment["Kestrel__Certificates__Default__Path"] = rutaCertificado;
        inicio.Environment["Kestrel__Certificates__Default__Password"] = passwordCertificado;
        using Process servidor = Process.Start(inicio)!;
        Task<string> salida = servidor.StandardOutput.ReadToEndAsync();
        Task<string> errores = servidor.StandardError.ReadToEndAsync();

        try
        {
            using var anonimo = CrearCliente(url);
            bool listo = false;
            for (int intento = 0; intento < 80 && !servidor.HasExited; intento++)
            {
                try { listo = (await anonimo.GetAsync("/favicon.ico")).IsSuccessStatusCode; }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                if (listo) break;
                await Task.Delay(250);
            }
            Assert.True(listo, servidor.HasExited
                ? "El servidor HTTPS de pruebas no inició. " + await salida + await errores
                : "El servidor HTTPS de pruebas no respondió dentro del tiempo esperado.");
            var sinSesion = await anonimo.GetAsync("/Ordenes");
            Assert.Equal(HttpStatusCode.Redirect, sinSesion.StatusCode);
            Assert.Contains("/Auth/Login", sinSesion.Headers.Location!.ToString());
            using var detalleSinSesion = new HttpRequestMessage(HttpMethod.Get, $"/Ordenes?handler=Detalle&id={activa}");
            detalleSinSesion.Headers.Add("X-Requested-With", "XMLHttpRequest");
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.SendAsync(detalleSinSesion)).StatusCode);
            using var pdfSinSesion = new HttpRequestMessage(HttpMethod.Get, $"/Ordenes/Comprobante?id={activa}");
            pdfSinSesion.Headers.Add("X-Requested-With", "XMLHttpRequest");
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.SendAsync(pdfSinSesion)).StatusCode);

            foreach (string rol in new[] { "Administrador", "Recepcionista" })
            {
                var cookies = new CookieContainer();
                using var cliente = CrearCliente(url, cookies);
                await IniciarSesion(cliente, rol, password);
                using var listado = await cliente.GetAsync("/Ordenes");
                Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
                Assert.True(listado.Headers.CacheControl!.NoStore);
                string html = WebUtility.HtmlDecode(await listado.Content.ReadAsStringAsync());
                Assert.Contains("Gestión de Órdenes", html);
                Assert.Contains("Ana María Arce Pérez", html);
                Assert.Contains("José Muñoz Núñez", html);
                Assert.Contains("Bs. 31,00", html);
                Assert.Equal(3, ContarFilas(html));
                Assert.Contains("ordenes-estado-activa", html);
                Assert.Contains("ordenes-estado-anulada", html);
                Assert.Contains("Refrescar", html);
                Assert.Matches("<form[^>]+method=\"get\"", html);
                Assert.Contains($"/Ordenes/Comprobante?id={activa}", html);
                Assert.Contains("target=\"_blank\"", html);

                string filtrado = WebUtility.HtmlDecode(await cliente.GetStringAsync("/Ordenes?Estado=Anulada&Fecha=2026-10-12"));
                Assert.Equal(1, ContarFilas(filtrado));
                Assert.Contains($"<td>#{anulada}</td>", filtrado);
                Assert.DoesNotContain($"<td>#{activa}</td>", filtrado);
                Assert.Matches("<option[^>]+value=\"Anulada\"[^>]+selected", filtrado);
                Assert.Contains("value=\"2026-10-12\"", filtrado);

                string invalido = await cliente.GetStringAsync("/Ordenes?Estado=NoExiste&Fecha=fecha-invalida");
                Assert.Contains("validation-summary-errors", invalido);
                Assert.Equal(0, ContarFilas(invalido));
                string vacio = WebUtility.HtmlDecode(await cliente.GetStringAsync("/Ordenes?Fecha=2026-10-10"));
                Assert.Contains("No se encontraron órdenes", vacio);
                Assert.Equal(0, ContarFilas(vacio));

                string detalleHtml = await cliente.GetStringAsync($"/Ordenes?handler=Detalle&id={activa}");
                Assert.Equal(2, ContarFilas(detalleHtml));
                Assert.DoesNotContain("<script>", detalleHtml);
                Assert.Contains("&lt;script&gt;", detalleHtml);
                string detalleTexto = WebUtility.HtmlDecode(detalleHtml);
                Assert.Contains("31/12/2025 20:00", detalleTexto);
                Assert.Contains("UTC-4", detalleTexto);
                Assert.Contains("Bs. 10,00", detalleTexto);
                Assert.Contains("Bs. 21,00", detalleTexto);
                Assert.Contains("Bs. 31,00", detalleTexto);
                Assert.DoesNotContain("999,99", detalleTexto);
                Assert.Contains("-1A", detalleTexto);
                Assert.Contains("-2B", detalleTexto);
                Assert.DoesNotContain("Anulado por", detalleTexto);
                string anulacion = WebUtility.HtmlDecode(await cliente.GetStringAsync($"/Ordenes?handler=Detalle&id={anulada}"));
                Assert.Contains("admin_prueba", anulacion);
                Assert.Contains("13/10/2026 20:00", anulacion);
                Assert.Equal(2, ContarFilas(anulacion));
                Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/Ordenes?handler=Detalle&id=2147483647")).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/Ordenes?handler=Detalle&id=-1")).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/Ordenes?handler=Detalle&id=abc")).StatusCode);
                using var comprobante = await cliente.GetAsync($"/Ordenes/Comprobante?id={activa}&GeneradoPor=inventado");
                Assert.Equal(HttpStatusCode.OK, comprobante.StatusCode);
                Assert.Equal("application/pdf", comprobante.Content.Headers.ContentType!.MediaType);
                Assert.Equal("inline", comprobante.Content.Headers.ContentDisposition!.DispositionType);
                Assert.Contains($"Comprobante-Orden-{activa}.pdf", comprobante.Content.Headers.ContentDisposition.ToString());
                byte[] pdf = await comprobante.Content.ReadAsByteArrayAsync();
                Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 8));
                using var copia = await cliente.GetAsync($"/Ordenes/Comprobante?id={activa}");
                Assert.Equal(HttpStatusCode.OK, copia.StatusCode);
                using var pdfAnulado = await cliente.GetAsync($"/Ordenes/Comprobante?id={anulada}");
                Assert.Equal(HttpStatusCode.OK, pdfAnulado.StatusCode);
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_PDF_PYTHON")))
                {
                    string texto = SoporteComprobantePdf.ExtraerTexto(pdf);
                    Assert.Contains($"Copia generada por: {rol}", texto);
                    Assert.Contains("Creado por: recepcion_prueba", texto);
                    Assert.Contains("Son Treinta y Un Bolivianos.", texto);
                    Assert.DoesNotContain("inventado", texto);
                    Assert.DoesNotContain("999,99", texto);
                    string textoAnulado = SoporteComprobantePdf.ExtraerTexto(await pdfAnulado.Content.ReadAsByteArrayAsync());
                    Assert.Contains("ANULADA", textoAnulado);
                    Assert.Contains("31,00", textoAnulado);
                }
                Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/Ordenes/Comprobante?id=abc")).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/Ordenes/Comprobante?id=-1")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/Ordenes/Comprobante?id=2147483647")).StatusCode);

                if (rol == "Recepcionista")
                {
                    string rutaFiltrada = "/Ordenes?Estado=Anulada&Fecha=2026-10-12";
                    Assert.Equal(1, ContarFilas(await cliente.GetStringAsync(rutaFiltrada)));
                    ConsultaOrdenesMySqlTests.CrearOrden(aislada, new DateTime(2026, 10, 12), EstadoOrden.Anulada);
                    Assert.Equal(2, ContarFilas(await cliente.GetStringAsync(rutaFiltrada)));
                    Assert.Equal(4, ContarFilas(await cliente.GetStringAsync("/Ordenes")));
                    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
                    {
                        await ComprobarModalEnNavegador(url, cookies, aislada, activa, anulada);
                    }
                }
            }

            Assert.Equal(4, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        }
        finally
        {
            if (!servidor.HasExited) servidor.Kill(entireProcessTree: true);
            await servidor.WaitForExitAsync();
            await salida;
            await errores;
            File.Delete(rutaCertificado);
        }
    }

    private static HttpClient CrearCliente(string url, CookieContainer? cookies = null) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        CookieContainer = cookies ?? new CookieContainer(),
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(5) };

    private static async Task ComprobarModalEnNavegador(
        string url, CookieContainer cookies, MySqlAisladoFixture aislada, int activa, int anulada)
    {
        var inicio = new ProcessStartInfo("node")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add("tests/ordenes_ui.cjs");
        inicio.Environment["TALLER_UI_URL"] = url;
        inicio.Environment["TALLER_UI_DATABASE"] = new MySqlConnector.MySqlConnectionStringBuilder(aislada.CadenaConexion).Database;
        inicio.Environment["TALLER_UI_ACTIVA"] = activa.ToString();
        inicio.Environment["TALLER_UI_ANULADA"] = anulada.ToString();
        inicio.Environment["TALLER_UI_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
            .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
        using Process navegador = Process.Start(inicio)!;
        Task<string> salida = navegador.StandardOutput.ReadToEndAsync();
        Task<string> errores = navegador.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await navegador.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { navegador.Kill(entireProcessTree: true); throw; }
        Assert.True(navegador.ExitCode == 0, await salida + await errores);
    }

    private static async Task IniciarSesion(HttpClient cliente, string username, string password)
    {
        string login = await cliente.GetStringAsync("/Auth/Login");
        string campoToken = Regex.Match(login, "<input[^>]+name=\"__RequestVerificationToken\"[^>]*>").Value;
        string token = WebUtility.HtmlDecode(Regex.Match(campoToken, "value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username, ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token
        });
        using var respuesta = await cliente.PostAsync("/Auth/Login", contenido);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    private static int ContarFilas(string html) => Regex.Matches(html, "<tbody>.*?</tbody>", RegexOptions.Singleline)
        .Select(tabla => Regex.Matches(tabla.Value, "<tr>").Count).Sum();
}
