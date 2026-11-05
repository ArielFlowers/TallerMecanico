using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "API")]
[Trait("Tipo", "Navegador")]
public sealed class AdministracionReportesHttpTests
{
    [MySqlAisladoFact]
    public async Task AdministracionYReportes_FiltrosEstadosExportacionesYControlesAccesibles()
    {
        using var aislada = new MySqlAisladoFixture();
        const string password = "ClavePruebas123!";
        string hash = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions())).GenerarHash(password);
        var usuarios = new UsuarioRepository(aislada.Factory);
        foreach (string nombre in new[] { "admin_prueba", "ana_foco", "bruno_otro", "usuario_inactivo" }
            .Concat(Enumerable.Range(0, 11).Select(i => $"equipo{i:00}")))
        {
            usuarios.Agregar(new Usuario
            {
                Username = nombre, Email = $"{nombre}@example.test", PasswordHash = hash,
                Rol = nombre == "admin_prueba" ? "Administrador" : "Recepcionista",
                Activo = nombre != "usuario_inactivo", EmailVerificado = true,
                CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
            });
        }
        SembrarOrdenes(aislada);

        var temporal = Directory.CreateTempSubdirectory("taller-admin-reportes-");
        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var solicitud = new CertificateRequest("CN=localhost", clave, HashAlgorithmName.SHA256);
        var nombres = new SubjectAlternativeNameBuilder();
        nombres.AddIpAddress(IPAddress.Loopback);
        solicitud.CertificateExtensions.Add(nombres.Build());
        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        string rutaCertificado = Path.Combine(temporal.FullName, "localhost.pfx");
        string claveCertificado = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(rutaCertificado, certificado.Export(X509ContentType.Pfx, claveCertificado));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string url = $"https://127.0.0.1:{puerto}";

        try
        {
            using var servidor = Process.Start(PrepararServidor(aislada, url, rutaCertificado, claveCertificado, temporal.FullName))!;
            Task<string> salida = servidor.StandardOutput.ReadToEndAsync(), errores = servidor.StandardError.ReadToEndAsync();
            try
            {
                var cookies = new CookieContainer();
                using var admin = NuevoCliente(url, cookies);
                bool listo = false;
                for (int intento = 0; intento < 80 && !servidor.HasExited; intento++)
                {
                    try { listo = (await admin.GetAsync("/css/site.css")).IsSuccessStatusCode; }
                    catch (HttpRequestException) { }
                    catch (TaskCanceledException) { }
                    if (listo) break;
                    await Task.Delay(250);
                }
                Assert.True(listo, "El servidor HTTPS aislado no inició.");
                await IniciarSesion(admin, "admin_prueba", password);

                using var retirada = await admin.GetAsync("/Administracion/Solicitudes");
                Assert.Equal(HttpStatusCode.NotFound, retirada.StatusCode);
                string pagina = await admin.GetStringAsync("/Administracion/Usuarios");
                Assert.DoesNotContain("/Administracion/Solicitudes", pagina);
                Assert.DoesNotContain("PasswordHash", pagina);
                Assert.DoesNotContain(hash, pagina);

                using var filtrada = await admin.GetAsync("/Administracion/Usuarios?handler=Resultados&Busqueda=ana_foco");
                Assert.True(filtrada.Headers.CacheControl?.NoStore);
                string fragmento = await filtrada.Content.ReadAsStringAsync();
                Assert.Contains("ana_foco", fragmento);
                Assert.DoesNotContain("bruno_otro", fragmento);
                Assert.DoesNotContain("<html", fragmento, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("No se encontraron usuarios", await admin.GetStringAsync(
                    "/Administracion/Usuarios?handler=Resultados&Busqueda=nadie_xyz_987"));

                string ordenes = WebUtility.HtmlDecode(await admin.GetStringAsync("/Reportes/Ordenes"));
                Assert.Equal(3, Regex.Matches(ordenes, @"class=""estado-orden estado-activa""[^>]*>\s*Activa\s*<").Count);
                Assert.Single(Regex.Matches(ordenes, @"class=""estado-orden estado-anulada""[^>]*>\s*Anulada\s*<"));
                string mecanicos = WebUtility.HtmlDecode(await admin.GetStringAsync(
                    "/Reportes/Mecanicos?Filtro.NombreMecanico=Alberto"));
                Assert.Contains("Alberto Rojas Uno", mecanicos);
                Assert.DoesNotContain("Beatriz Perez Dos", mecanicos);
                await VerificarExportaciones(admin, mecanicos);

                string enlaceAntiguo = WebUtility.HtmlDecode(await admin.GetStringAsync(
                    "/Reportes/Mecanicos?Filtro.NombreMecanico=Alberto&Filtro.FechaDesde=2000-01-01&Filtro.MecanicoId=99999"));
                Assert.Contains("Alberto Rojas Uno", enlaceAntiguo);
                Assert.Matches(@">\s*350[.,]00\s*<", enlaceAntiguo);
                Assert.DoesNotContain("Beatriz Perez Dos", enlaceAntiguo);

                string fueraDeRango = WebUtility.HtmlDecode(await admin.GetStringAsync(
                    "/Reportes/Mecanicos?Filtro.NombreMecanico=Alberto&Filtro.FechaDesde=2040-01-01"));
                Assert.Contains("Alberto Rojas Uno", fueraDeRango);
                Assert.DoesNotMatch(@">\s*350[.,]00\s*<", fueraDeRango);

                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
                {
                    await VerificarNavegador(url, cookies);
                    Assert.False(usuarios.ObtenerPorUsername("ana_foco")!.Activo);
                }
                Assert.Equal(15, usuarios.ContarTotal());
                Assert.Equal(hash, usuarios.ObtenerPorUsername("admin_prueba")!.PasswordHash);
            }
            finally
            {
                if (!servidor.HasExited) servidor.Kill(entireProcessTree: true);
                await servidor.WaitForExitAsync();
                await salida;
                await errores;
            }
        }
        finally { temporal.Delete(recursive: true); }
    }

    private static void SembrarOrdenes(MySqlAisladoFixture db)
    {
        long cliente = db.Ejecutar("""
            INSERT INTO Clientes (Ci, Nombres, PrimerApellido, SegundoApellido, Celular)
            VALUES ('7654321', 'Cliente', 'Reporte', 'Prueba', '71234567');
            """);
        long vehiculo = db.Ejecutar("""
            INSERT INTO Vehiculos (Placa, Marca, Modelo, Kilometraje, ClienteId, EsPlacaExtranjera)
            VALUES ('UI-REPORTES', 'Toyota', 'Corolla', 100, @Cliente, TRUE);
            """, ("@Cliente", cliente));
        foreach (var (ci, nombre, apellido, segundo) in new[]
            { ("8765431", "Alberto", "Rojas", "Uno"), ("8765432", "Beatriz", "Perez", "Dos") })
        {
            long mecanico = db.Ejecutar("""
                INSERT INTO Mecanicos (Ci, Nombres, PrimerApellido, SegundoApellido, Genero, Especialidad, Celular)
                VALUES (@Ci, @Nombre, @Apellido, @Segundo, 'Masculino', 'Sin Especialidad', '71234567');
                """, ("@Ci", ci), ("@Nombre", nombre), ("@Apellido", apellido), ("@Segundo", segundo));
            foreach (var (fecha, total, anulada) in nombre == "Alberto"
                ? new[] { (new DateTime(2001, 1, 15), 100m, false), (new DateTime(2035, 5, 20), 250m, false), (new DateTime(2026, 5, 1), 900m, true) }
                : new[] { (new DateTime(2026, 5, 1), 80m, false) })
            {
                db.Ejecutar("""
                    INSERT INTO Ordenes (VehiculoId, MecanicoId, Fecha, Estado, Total, Token,
                        CreadoPor, FechaCreacion, AnuladoPor, FechaAnulacion)
                    VALUES (@Vehiculo, @Mecanico, @Fecha, @Estado, @Total, @Token,
                        'admin_prueba', UTC_TIMESTAMP(), @AnuladoPor, @FechaAnulacion);
                    """, ("@Vehiculo", vehiculo), ("@Mecanico", mecanico), ("@Fecha", fecha),
                    ("@Estado", anulada ? "Anulada" : "Activa"), ("@Total", total), ("@Token", Guid.NewGuid().ToString("D")),
                    ("@AnuladoPor", anulada ? "admin_prueba" : null), ("@FechaAnulacion", anulada ? fecha.AddHours(1) : null));
            }
        }
    }

    private static async Task VerificarExportaciones(HttpClient admin, string html)
    {
        foreach (string formato in new[] { "Pdf", "Excel" })
        {
            using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(html), ["Filtro.NombreMecanico"] = "Alberto"
            });
            using var respuesta = await admin.PostAsync($"/Reportes/Mecanicos?handler=Exportar{formato}", contenido);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            byte[] archivo = await respuesta.Content.ReadAsByteArrayAsync();
            if (formato == "Pdf")
            {
                Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
                Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(archivo.AsSpan(0, 5)));
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_PDF_PYTHON")))
                {
                    string texto = SoporteComprobantePdf.ExtraerTexto(archivo);
                    Assert.Contains("Alberto Rojas Uno", texto);
                    Assert.DoesNotContain("Beatriz Perez Dos", texto);
                }
            }
            else
            {
                Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", respuesta.Content.Headers.ContentType?.MediaType);
                using var zip = new ZipArchive(new MemoryStream(archivo));
                var textos = new List<string>();
                foreach (var entrada in zip.Entries.Where(e => e.FullName.StartsWith("xl/") && e.FullName.EndsWith(".xml")))
                {
                    using var flujo = entrada.Open();
                    textos.AddRange(XDocument.Load(flujo).Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
                }
                Assert.Contains("Alberto Rojas Uno", textos);
                Assert.DoesNotContain("Beatriz Perez Dos", textos);
            }
        }
    }

    private static ProcessStartInfo PrepararServidor(MySqlAisladoFixture aislada, string url, string certificado, string password, string temporal)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(typeof(AuthenticationService).Assembly.Location);
        inicio.ArgumentList.Add("--urls");
        inicio.ArgumentList.Add(url);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ConnectionStrings__MySqlConnection"] = aislada.CadenaConexion;
        inicio.Environment["Kestrel__Certificates__Default__Path"] = certificado;
        inicio.Environment["Kestrel__Certificates__Default__Password"] = password;
        inicio.Environment["DataProtection__KeysPath"] = Path.Combine(temporal, "keys");
        inicio.Environment["REDIS_HOST"] = "";
        inicio.Environment["Redis__Host"] = "";
        return inicio;
    }

    private static HttpClient NuevoCliente(string url, CookieContainer cookies) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, CookieContainer = cookies, ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(10) };

    private static string Token(string html)
    {
        string token = WebUtility.HtmlDecode(Regex.Match(
            Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]*>").Value,
            "value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        return token;
    }

    private static async Task IniciarSesion(HttpClient cliente, string usuario, string password)
    {
        string html = await cliente.GetStringAsync("/Auth/Login");
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["Input.Username"] = usuario, ["Input.Password"] = password
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
        inicio.ArgumentList.Add("tests/usuarios_reportes_ui.cjs");
        inicio.Environment["TALLER_USUARIOS_REPORTES_URL"] = url;
        inicio.Environment["TALLER_USUARIOS_REPORTES_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
            .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await proceso.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
        Assert.True(proceso.ExitCode == 0, await salida + await errores);
    }
}
