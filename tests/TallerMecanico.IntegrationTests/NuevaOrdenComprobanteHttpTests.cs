using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class NuevaOrdenComprobanteHttpTests
{
    [MySqlAisladoFact]
    public async Task Navegador_PostTradicionalFechaAutomaticaComprobanteErroresBloqueoYRecarga()
    {
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")),
            "Esta prueba requiere PLAYWRIGHT_MODULE y Microsoft Edge.");
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
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
        string ruta = Path.Combine(Path.GetTempPath(), $"orden_comprobante_{Guid.NewGuid():N}.pfx");
        string passwordCertificado = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(ruta, certificado.Export(X509ContentType.Pfx, passwordCertificado));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string url = $"https://127.0.0.1:{puerto}";
        try
        {
            using var servidor = Process.Start(NuevaOrdenAutocompletadoHttpTests.PrepararServidor(aislada, url, ruta, passwordCertificado))!;
            Task<string> salida = servidor.StandardOutput.ReadToEndAsync(), errores = servidor.StandardError.ReadToEndAsync();
            try
            {
                using var anonimo = NuevaOrdenAutocompletadoHttpTests.NuevoCliente(url, new CookieContainer());
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
                var cookiesPorRol = new Dictionary<string, object>();
                foreach (string rol in new[] { "Administrador", "Recepcionista" })
                {
                    var cookies = new CookieContainer();
                    using var cliente = NuevaOrdenAutocompletadoHttpTests.NuevoCliente(url, cookies);
                    await NuevaOrdenAutocompletadoHttpTests.IniciarSesion(cliente, rol, password);
                    cookiesPorRol[rol] = cookies.GetCookies(new Uri(url)).Cast<Cookie>()
                        .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }).ToArray();
                }
                var inicio = new ProcessStartInfo("node")
                {
                    WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                inicio.ArgumentList.Add("tests/nueva_orden_fecha_comprobante_ui.cjs");
                inicio.Environment["TALLER_NUEVA_ORDEN_URL"] = url;
                inicio.Environment["TALLER_ORDEN_COOKIES_POR_ROL"] = JsonSerializer.Serialize(cookiesPorRol);
                using var navegador = Process.Start(inicio)!;
                Task<string> salidaNavegador = navegador.StandardOutput.ReadToEndAsync(), erroresNavegador = navegador.StandardError.ReadToEndAsync();
                using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(4));
                try { await navegador.WaitForExitAsync(limite.Token); }
                catch (OperationCanceledException) { navegador.Kill(entireProcessTree: true); throw; }
                Assert.True(navegador.ExitCode == 0, await salidaNavegador + await erroresNavegador);
                Assert.Equal(4L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
                Assert.Equal(6L, Convert.ToInt64(aislada.Escalar("SELECT Stock FROM Productos WHERE Id=1;")));
                Assert.Equal(10L, Convert.ToInt64(aislada.Escalar("SELECT Stock FROM Productos WHERE Id=2;")));
                Assert.Equal(150m, Convert.ToDecimal(aislada.Escalar("SELECT SUM(Total) FROM Ordenes;")));
                Assert.Equal(2L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes WHERE CreadoPor='Administrador';")));
                DateTime fechaBolivia = TimeProvider.System.GetUtcNow().ToOffset(TimeSpan.FromHours(-4)).Date;
                Assert.Equal(4L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes WHERE Fecha=@Fecha;", ("@Fecha", fechaBolivia))));
                Assert.Equal(4L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;")));

                using var sinPermiso = new HttpRequestMessage(HttpMethod.Get, "/Ordenes/Comprobante?id=1");
                sinPermiso.Headers.Add("X-Requested-With", "XMLHttpRequest");
                using var denegada = await anonimo.SendAsync(sinPermiso);
                Assert.Equal(HttpStatusCode.Unauthorized, denegada.StatusCode);
            }
            finally
            {
                if (!servidor.HasExited) servidor.Kill(entireProcessTree: true);
                await servidor.WaitForExitAsync();
                await salida;
                await errores;
            }
        }
        finally { File.Delete(ruta); }
    }
}
