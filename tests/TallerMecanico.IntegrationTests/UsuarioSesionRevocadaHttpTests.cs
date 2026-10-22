using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class UsuarioSesionRevocadaHttpTests
{
    [MySqlAisladoFact]
    public async Task UsuarioDesactivado_PierdeSesionAutenticada()
    {
        using var aislada = new MySqlAisladoFixture();

        string username = "hu3_sesion_" + Guid.NewGuid().ToString("N")[..12];
        string password = "Password123456";
        var hasher = new Argon2idPasswordHasher(
            Options.Create(new Argon2idOptions()));

        int id = checked((int)aislada.Ejecutar("""
            INSERT INTO Usuarios
                (Username, PasswordHash, Email, EmailVerificado,
                 Rol, Activo, CreadoPor, FechaCreacion)
            VALUES
                (@Username, @Hash, @Email, TRUE,
                 'Recepcionista', TRUE, 'pruebas', UTC_TIMESTAMP());
            """,
            ("@Username", username),
            ("@Hash", hasher.GenerarHash(password)),
            ("@Email", username + "@test.com")));

        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var solicitud = new CertificateRequest(
            "CN=localhost", clave, HashAlgorithmName.SHA256);

        var nombres = new SubjectAlternativeNameBuilder();
        nombres.AddIpAddress(IPAddress.Loopback);
        solicitud.CertificateExtensions.Add(nombres.Build());

        using var certificado = solicitud.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1));

        string rutaCertificado = Path.Combine(
            Path.GetTempPath(), $"hu3_sesion_{Guid.NewGuid():N}.pfx");

        string claveCertificado = Guid.NewGuid().ToString("N");

        File.WriteAllBytes(rutaCertificado,
            certificado.Export(X509ContentType.Pfx, claveCertificado));

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        string url = $"https://127.0.0.1:{puerto}";

        try
        {
            var inicio = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = MySqlAisladoFixture.RaizProyecto,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            inicio.ArgumentList.Add(
                typeof(TallerMecanico.Aplicacion.Servicios.ClienteService)
                    .Assembly.Location);

            inicio.ArgumentList.Add("--urls");
            inicio.ArgumentList.Add(url);

            inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            inicio.Environment["ConnectionStrings__MySqlConnection"] =
                aislada.CadenaConexion;
            inicio.Environment["Kestrel__Certificates__Default__Path"] =
                rutaCertificado;
            inicio.Environment["Kestrel__Certificates__Default__Password"] =
                claveCertificado;

            using var servidor = Process.Start(inicio)!;
            Task<string> salida = servidor.StandardOutput.ReadToEndAsync();
            Task<string> errores = servidor.StandardError.ReadToEndAsync();

            try
            {
                var cookies = new CookieContainer();

                using var cliente = new HttpClient(
                    new HttpClientHandler
                    {
                        AllowAutoRedirect = false,
                        CookieContainer = cookies,
                        ServerCertificateCustomValidationCallback =
                            (_, _, _, _) => true
                    })
                {
                    BaseAddress = new Uri(url),
                    Timeout = TimeSpan.FromSeconds(5)
                };

                bool listo = false;

                for (int intento = 0; intento < 60 && !servidor.HasExited; intento++)
                {
                    try
                    {
                        listo = (await cliente.GetAsync("/css/site.css"))
                            .IsSuccessStatusCode;
                    }
                    catch (HttpRequestException) { }
                    catch (TaskCanceledException) { }

                    if (listo) break;
                    await Task.Delay(250);
                }

                Assert.True(listo, "El servidor HTTPS aislado no inicio.");

                string html = await cliente.GetStringAsync("/Auth/Login");

                string campo = Regex.Match(
                    html,
                    "<input[^>]+name=\"__RequestVerificationToken\"[^>]*>"
                ).Value;

                string token = WebUtility.HtmlDecode(
                    Regex.Match(campo, "value=\"([^\"]+)\"")
                        .Groups[1].Value);

                Assert.False(string.IsNullOrWhiteSpace(token));

                using var formulario = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["__RequestVerificationToken"] = token,
                        ["Input.Username"] = username,
                        ["Input.Password"] = password
                    });

                using var login = await cliente.PostAsync(
                    "/Auth/Login", formulario);

                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

                using var antes = await cliente.GetAsync("/Clientes");

                Assert.Equal(HttpStatusCode.OK, antes.StatusCode);

                // Desactivar el usuario en la base exclusivamente aislada.
                aislada.Ejecutar(
                    "UPDATE Usuarios SET Activo = FALSE WHERE Id = @Id;",
                    ("@Id", id));

                // Mismo cliente, misma cookie: debe perder el acceso.
                using var despues = await cliente.GetAsync("/Clientes");

                Assert.Equal(HttpStatusCode.Redirect, despues.StatusCode);
                Assert.Contains(
                    "/Auth/Login?expired=1",
                    despues.Headers.Location?.ToString());

                Assert.True(
                    cookies.GetCookies(new Uri(url))
                        .Cast<Cookie>()
                        .All(c => c.Name != "AutoTaller.Auth"),
                    "La cookie de autenticacion debe quedar invalidada.");
            }
            finally
            {
                if (!servidor.HasExited)
                    servidor.Kill(entireProcessTree: true);

                await servidor.WaitForExitAsync();
                await salida;
                await errores;
            }
        }
        finally
        {
            File.Delete(rutaCertificado);
        }
    }
}