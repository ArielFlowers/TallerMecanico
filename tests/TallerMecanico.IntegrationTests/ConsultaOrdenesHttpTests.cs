using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Infraestructura.Identity;
using TallerMecanico.Models;

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

            foreach (string rol in new[] { "Administrador", "Recepcionista" })
            {
                using var cliente = CrearCliente(url);
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

                if (rol == "Recepcionista")
                {
                    string rutaFiltrada = "/Ordenes?Estado=Anulada&Fecha=2026-10-12";
                    Assert.Equal(1, ContarFilas(await cliente.GetStringAsync(rutaFiltrada)));
                    ConsultaOrdenesMySqlTests.CrearOrden(aislada, new DateTime(2026, 10, 12), EstadoOrden.Anulada);
                    Assert.Equal(2, ContarFilas(await cliente.GetStringAsync(rutaFiltrada)));
                    Assert.Equal(4, ContarFilas(await cliente.GetStringAsync("/Ordenes")));
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

    private static HttpClient CrearCliente(string url) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        CookieContainer = new CookieContainer(),
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(5) };

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
