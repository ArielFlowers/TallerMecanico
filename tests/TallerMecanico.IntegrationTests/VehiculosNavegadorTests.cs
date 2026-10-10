using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using MySqlConnector;
using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Infraestructura.Identity;
using TallerMecanico.Models;

namespace TallerMecanico.IntegrationTests;

public sealed class NavegadorFactAttribute : FactAttribute
{
    public NavegadorFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_TEST_MYSQL")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
            Skip = "Configura TALLER_TEST_MYSQL y PLAYWRIGHT_MODULE para probar el navegador.";
    }
}

public sealed class VehiculosNavegadorTests
{
    [NavegadorFact]
    public async Task Vehiculos_RecorridoRealEnEdge()
    {
        using var aislada = new MySqlAisladoFixture();
        string raiz = MySqlAisladoFixture.RaizProyecto;
        foreach (string migracion in new[] { "001_registro_usuarios.sql", "002_control_reenvio_verificacion.sql" })
            aislada.Ejecutar(File.ReadAllText(Path.Combine(raiz, "docs/migraciones", migracion)));
        string password = $"Ec2_{Guid.NewGuid():N}";
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        new UsuarioRepository(aislada.Factory).Agregar(new Usuario
        {
            Username = "ec2_ui", PasswordHash = hasher.GenerarHash(password), Rol = "Recepcionista",
            CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
        });
        var clientes = new ClienteRepository(aislada.Factory);
        clientes.Add(new Cliente { Ci = "1111111", Nombres = "Zoe", PrimerApellido = "Zeballos", SegundoApellido = "Diaz", Celular = "71111111", CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow });
        clientes.Add(new Cliente { Ci = "2222222", Nombres = "Ana", PrimerApellido = "Arce", SegundoApellido = "Lopez", Celular = "72222222", CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow });

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
        string certificadoRuta = Path.Combine(Path.GetTempPath(), $"autotaller_cert_{Guid.NewGuid():N}.pfx");
        string certificadoPassword = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(certificadoRuta, certificado.Export(X509ContentType.Pfx, certificadoPassword));

        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = raiz, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(Path.Combine(raiz, "bin/Debug/net10.0/TallerMecanico.dll"));
        inicio.ArgumentList.Add("--urls");
        inicio.ArgumentList.Add(url);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ConnectionStrings__MySqlConnection"] = aislada.CadenaConexion;
        inicio.Environment["Kestrel__Certificates__Default__Path"] = certificadoRuta;
        inicio.Environment["Kestrel__Certificates__Default__Password"] = certificadoPassword;
        using Process servidor = Process.Start(inicio)!;
        Task<string> salidaServidor = servidor.StandardOutput.ReadToEndAsync();
        Task<string> errorServidor = servidor.StandardError.ReadToEndAsync();
        try
        {
            using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
            using var cliente = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
            bool listo = false;
            for (int intento = 0; intento < 80 && !servidor.HasExited; intento++)
            {
                try { listo = (await cliente.GetAsync(url + "/Auth/Login")).IsSuccessStatusCode; }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                if (listo) break;
                await Task.Delay(250);
            }
            Assert.True(listo, "El servidor HTTPS de pruebas no inició.");
            var inicioNavegador = new ProcessStartInfo("node")
            {
                WorkingDirectory = raiz, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            inicioNavegador.ArgumentList.Add("tests/vehiculos_ui.cjs");
            inicioNavegador.Environment["TALLER_UI_URL"] = url;
            inicioNavegador.Environment["TALLER_UI_DATABASE"] = new MySqlConnectionStringBuilder(aislada.CadenaConexion).Database;
            inicioNavegador.Environment["TALLER_UI_PASSWORD"] = password;
            using Process navegador = Process.Start(inicioNavegador)!;
            Task<string> salida = navegador.StandardOutput.ReadToEndAsync();
            Task<string> error = navegador.StandardError.ReadToEndAsync();
            using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try { await navegador.WaitForExitAsync(limite.Token); }
            catch (OperationCanceledException) { navegador.Kill(entireProcessTree: true); throw; }
            Assert.True(navegador.ExitCode == 0, await salida + await error);
        }
        finally
        {
            if (!servidor.HasExited) servidor.Kill(entireProcessTree: true);
            await servidor.WaitForExitAsync();
            await salidaServidor;
            await errorServidor;
            File.Delete(certificadoRuta);
        }
    }
}
