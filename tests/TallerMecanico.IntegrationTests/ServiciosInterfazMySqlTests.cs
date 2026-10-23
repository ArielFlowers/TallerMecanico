using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class ServiciosInterfazMySqlTests
{
    [MySqlAisladoFact]
    public async Task Navegador_CantidadesBorradoresModalPdfEHistoricoConPreciosMySql()
    {
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")));
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        aislada.Ejecutar("UPDATE Servicios SET Costo=250 WHERE CodigoCatalogo='CAMBIO_ACEITE';");
        CatalogoServiciosMySqlTests.Cargar(aislada);
        var administracion = ServiciosAutocompletadoTests.CrearServicio(aislada);
        Servicio filtros = administracion.ObtenerCatalogo().Single(servicio => servicio.CodigoCatalogo == "CAMBIO_FILTROS");
        filtros.Costo = 105.50m;
        administracion.Actualizar(filtros);
        string password = Guid.NewGuid().ToString("N") + "_Prueba";
        string hash = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions())).GenerarHash(password);
        new UsuarioRepository(aislada.Factory).Agregar(new Usuario
        {
            Username = "recepcion_servicios", Rol = "Recepcionista", PasswordHash = hash,
            CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
        });
        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var solicitud = new CertificateRequest("CN=localhost", clave, HashAlgorithmName.SHA256);
        var nombres = new SubjectAlternativeNameBuilder();
        nombres.AddIpAddress(IPAddress.Loopback);
        solicitud.CertificateExtensions.Add(nombres.Build());
        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        string ruta = Path.Combine(Path.GetTempPath(), $"servicios_ui_{Guid.NewGuid():N}.pfx");
        string claveCertificado = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(ruta, certificado.Export(X509ContentType.Pfx, claveCertificado));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string url = $"https://127.0.0.1:{puerto}";
        try
        {
            using var servidor = Process.Start(NuevaOrdenAutocompletadoHttpTests.PrepararServidor(aislada, url, ruta, claveCertificado))!;
            Task<string> salida = servidor.StandardOutput.ReadToEndAsync(), errores = servidor.StandardError.ReadToEndAsync();
            try
            {
                var cookies = new CookieContainer();
                using var cliente = NuevaOrdenAutocompletadoHttpTests.NuevoCliente(url, cookies);
                bool listo = false;
                for (int intento = 0; intento < 100 && !servidor.HasExited; intento++)
                {
                    try { listo = (await cliente.GetAsync("/css/site.css")).IsSuccessStatusCode; }
                    catch (HttpRequestException) { }
                    catch (TaskCanceledException) { }
                    if (listo) break;
                    await Task.Delay(250);
                }
                Assert.True(listo, "El servidor aislado de servicios no inició.");
                await NuevaOrdenAutocompletadoHttpTests.IniciarSesion(cliente, "recepcion_servicios", password);
                var inicio = new ProcessStartInfo("node")
                {
                    WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                inicio.ArgumentList.Add("tests/servicios_cantidades_ui.cjs");
                inicio.Environment["TALLER_NUEVA_ORDEN_URL"] = url;
                inicio.Environment["TALLER_NUEVA_ORDEN_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
                    .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
                using var navegador = Process.Start(inicio)!;
                Task<string> salidaNavegador = navegador.StandardOutput.ReadToEndAsync(), erroresNavegador = navegador.StandardError.ReadToEndAsync();
                using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                try { await navegador.WaitForExitAsync(limite.Token); }
                catch (OperationCanceledException) { navegador.Kill(entireProcessTree: true); throw; }
                Assert.True(navegador.ExitCode == 0, await salidaNavegador + await erroresNavegador);

                Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
                Assert.Equal(8, Convert.ToInt32(aislada.Escalar("SELECT Stock FROM Productos WHERE Id=1;")));
                Assert.Equal(10, Convert.ToInt32(aislada.Escalar("SELECT Stock FROM Productos WHERE Id=2;")));
                Assert.Equal(591.50m, Convert.ToDecimal(aislada.Escalar("SELECT Total FROM Ordenes;")));
                int orden = Convert.ToInt32(aislada.Escalar("SELECT Id FROM Ordenes;"));
                filtros.Costo = 999;
                administracion.Actualizar(filtros);
                string parcial = WebUtility.HtmlDecode(await cliente.GetStringAsync($"/Ordenes?handler=Detalle&id={orden}"));
                Assert.Contains("105,50", parcial);
                Assert.Contains("316,50", parcial);
                Assert.DoesNotContain("999,00", parcial);
                byte[] pdf = await cliente.GetByteArrayAsync($"/Ordenes/Comprobante?id={orden}");
                string texto = SoporteComprobantePdf.ExtraerTexto(pdf);
                Assert.Contains("Precio unitario: Bs. 105,50", texto);
                Assert.Contains("Subtotal: Bs. 316,50", texto);
                Assert.Contains("TOTAL: Bs. 591,50", texto);
                Assert.DoesNotContain("999,00", texto);
                Assert.DoesNotContain("949,50", texto);
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
