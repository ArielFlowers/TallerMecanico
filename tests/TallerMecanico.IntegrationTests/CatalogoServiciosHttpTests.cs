using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class CatalogoServiciosHttpTests
{
    [MySqlAisladoFact]
    public async Task RolesYPostReal_OfrecenDiezServiciosYRechazanIdFueraDelCatalogo()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        CatalogoServiciosMySqlTests.Cargar(aislada);
        int antiguo = checked((int)aislada.Ejecutar("INSERT INTO Servicios (Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('Anterior','Servicio fuera de catálogo',1,1);"));
        string password = Guid.NewGuid().ToString("N") + "_Prueba";
        string hash = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions())).GenerarHash(password);
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
        string ruta = Path.Combine(Path.GetTempPath(), $"catalogo_{Guid.NewGuid():N}.pfx");
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
                using var anonimo = NuevaOrdenAutocompletadoHttpTests.NuevoCliente(url, new CookieContainer());
                bool listo = false;
                for (int intento = 0; intento < 100 && !servidor.HasExited; intento++)
                {
                    try { listo = (await anonimo.GetAsync("/css/site.css")).IsSuccessStatusCode; }
                    catch (HttpRequestException) { }
                    catch (TaskCanceledException) { }
                    if (listo) break;
                    await Task.Delay(250);
                }
                Assert.True(listo, "El servidor aislado de catálogo no inició.");
                foreach (string rol in new[] { "Administrador", "Recepcionista" })
                {
                    using var cliente = NuevaOrdenAutocompletadoHttpTests.NuevoCliente(url, new CookieContainer());
                    await NuevaOrdenAutocompletadoHttpTests.IniciarSesion(cliente, rol, password);
                    using var opciones = JsonDocument.Parse(await cliente.GetStringAsync("/Ordenes/Crear?handler=SugerenciasServicios"));
                    Assert.Equal(10, opciones.RootElement.GetArrayLength());
                    using var antiguoSeleccionado = await cliente.GetAsync($"/Ordenes/Crear?handler=ServicioSeleccionado&id={antiguo}");
                    Assert.Equal(HttpStatusCode.NotFound, antiguoSeleccionado.StatusCode);
                    using var administracion = new HttpRequestMessage(HttpMethod.Get, "/Servicios");
                    administracion.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    using var respuesta = await cliente.SendAsync(administracion);
                    Assert.Equal(rol == "Administrador" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, respuesta.StatusCode);
                    string formulario = await cliente.GetStringAsync("/Ordenes/Crear");
                    using var campos = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["__RequestVerificationToken"] = Campo(formulario, "__RequestVerificationToken"),
                        ["Formulario.Token"] = Campo(formulario, "Formulario.Token"),
                        ["Formulario.VehiculoId"] = "1", ["Formulario.MecanicoId"] = "1",
                        ["Formulario.ServicioIds"] = antiguo.ToString(),
                        [$"Formulario.CantidadesServicios[{antiguo}]"] = "1"
                    });
                    using var rechazo = await cliente.PostAsync("/Ordenes/Crear?handler=Guardar", campos);
                    Assert.Equal(HttpStatusCode.OK, rechazo.StatusCode);
                    Assert.Contains("catálogo", WebUtility.HtmlDecode(await rechazo.Content.ReadAsStringAsync()));
                }
                Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
                Assert.Equal(20, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
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

    private static string Campo(string html, string nombre)
    {
        string input = Regex.Match(html, $"<input[^>]+name=\"{Regex.Escape(nombre)}\"[^>]*>").Value;
        string contenido = WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
        Assert.False(string.IsNullOrEmpty(contenido));
        return contenido;
    }
}
