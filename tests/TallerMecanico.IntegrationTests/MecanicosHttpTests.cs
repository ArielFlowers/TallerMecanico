using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;

namespace TallerMecanico.IntegrationTests;

public sealed class MecanicosHttpTests
{
    [MySqlAisladoFact]
    public async Task FormularioHttps_CrudYAutocompletadoConservanFiltrosYPermisos()
    {
        using var aislada = new MySqlAisladoFixture();
        string password = $"Prueba_{Guid.NewGuid():N}";
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        new UsuarioRepository(aislada.Factory).Agregar(new Usuario
        {
            Username = "admin_prueba", Rol = "Administrador",
            PasswordHash = hasher.GenerarHash(password), CreadoPor = "pruebas",
            FechaCreacion = DateTime.UtcNow
        });

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string url = $"https://127.0.0.1:{puerto}";
        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var solicitud = new CertificateRequest("CN=localhost", clave, HashAlgorithmName.SHA256);
        var nombres = new SubjectAlternativeNameBuilder();
        nombres.AddIpAddress(IPAddress.Loopback);
        solicitud.CertificateExtensions.Add(nombres.Build());
        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        string rutaCertificado = Path.Combine(Path.GetTempPath(), $"mecanicos_{Guid.NewGuid():N}.pfx");
        string passwordCertificado = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(rutaCertificado, certificado.Export(X509ContentType.Pfx, passwordCertificado));

        try
        {
            using var servidor = Process.Start(PrepararServidor(aislada, url, rutaCertificado, passwordCertificado))!;
            Task<string> salida = servidor.StandardOutput.ReadToEndAsync();
            Task<string> errores = servidor.StandardError.ReadToEndAsync();
            var cookies = new CookieContainer();
            using var cliente = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CookieContainer = cookies,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(5) };

            try
            {
                await EsperarServidor(cliente, servidor, salida, errores);
                await IniciarSesion(cliente, password);
                string formulario = await cliente.GetStringAsync("/Mecanicos");
                string segundoApellido = Regex.Match(formulario,
                    "<input[^>]+id=\"MecanicoInput_SegundoApellido\"[^>]*>").Value;
                Assert.NotEmpty(segundoApellido);
                Assert.DoesNotMatch(@"\brequired(?:\s|=|/?>)", segundoApellido);
                Assert.DoesNotContain("data-val-required", segundoApellido);
                Assert.Contains("Opcional", Regex.Match(formulario,
                    "<label[^>]+for=\"MecanicoInput_SegundoApellido\".*?</div>", RegexOptions.Singleline).Value);
                string primerApellido = Regex.Match(formulario,
                    "<input[^>]+id=\"MecanicoInput_PrimerApellido\"[^>]*>").Value;
                Assert.Matches(@"\brequired(?:\s|=|/?>)", primerApellido);

                using var creado = await EnviarFormulario(cliente, formulario, "Crear", "", 0);
                Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
                int id = Convert.ToInt32(aislada.Escalar("SELECT Id FROM Mecanicos;"));
                Assert.Equal("", aislada.Escalar("SELECT SegundoApellido FROM Mecanicos;"));

                formulario = await cliente.GetStringAsync("/Mecanicos");
                using var conApellido = await EnviarFormulario(cliente, formulario, "Actualizar", "   mUÑOZ   ", id);
                Assert.Equal(HttpStatusCode.Redirect, conApellido.StatusCode);
                Assert.Equal("Muñoz", aislada.Escalar("SELECT SegundoApellido FROM Mecanicos;"));

                formulario = await cliente.GetStringAsync("/Mecanicos");
                using var sinApellido = await EnviarFormulario(cliente, formulario, "Actualizar", "", id);
                Assert.Equal(HttpStatusCode.Redirect, sinApellido.StatusCode);
                Assert.Equal("", aislada.Escalar("SELECT SegundoApellido FROM Mecanicos;"));

                string listado = await cliente.GetStringAsync("/Mecanicos?TerminoBusqueda=P%C3%A9rez");
                Assert.Contains("data-editar-mecanico", listado);
                Assert.Contains("data-segundo-apellido=\"\"", listado);

                using var invalido = await EnviarFormulario(cliente, listado, "Actualizar", "Pérez2", id);
                Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
                Assert.Contains("El segundo apellido solo puede contener letras y espacios.",
                    WebUtility.HtmlDecode(await invalido.Content.ReadAsStringAsync()));
                Assert.Equal("", aislada.Escalar("SELECT SegundoApellido FROM Mecanicos;"));
                await VerificarBuscador(cliente, aislada, url, password, id, cookies);
            }
            finally
            {
                if (!servidor.HasExited) servidor.Kill(entireProcessTree: true);
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

    private static ProcessStartInfo PrepararServidor(MySqlAisladoFixture aislada, string url, string certificado, string password)
    {
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
        inicio.Environment["Kestrel__Certificates__Default__Path"] = certificado;
        inicio.Environment["Kestrel__Certificates__Default__Password"] = password;
        return inicio;
    }

    private static async Task EsperarServidor(
        HttpClient cliente, Process servidor, Task<string> salida, Task<string> errores)
    {
        for (int intento = 0; intento < 80 && !servidor.HasExited; intento++)
        {
            try
            {
                using var respuesta = await cliente.GetAsync("/Auth/Login");
                if (respuesta.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(250);
        }
        Assert.Fail(servidor.HasExited
            ? "El servidor HTTPS de Mecánicos terminó: " + await salida + await errores
            : "El servidor HTTPS de pruebas de Mecánicos no respondió.");
    }

    private static string ObtenerToken(string html)
    {
        string campo = Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]*>").Value;
        string token = WebUtility.HtmlDecode(Regex.Match(campo, "value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        return token;
    }

    private static async Task IniciarSesion(HttpClient cliente, string password, string username = "admin_prueba")
    {
        string login = await cliente.GetStringAsync("/Auth/Login");
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username, ["Input.Password"] = password,
            ["__RequestVerificationToken"] = ObtenerToken(login)
        });
        using var respuesta = await cliente.PostAsync("/Auth/Login", contenido);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    private static async Task<HttpResponseMessage> EnviarFormulario(
        HttpClient cliente, string html, string handler, string segundoApellido, int id,
        string? termino = null, int? seleccionado = null)
    {
        var campos = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ObtenerToken(html),
            ["MecanicoId"] = id.ToString(), ["MecanicoInput.Ci"] = "1234567",
            ["MecanicoInput.ComplementoCi"] = "", ["MecanicoInput.Nombres"] = "Juan Carlos",
            ["MecanicoInput.PrimerApellido"] = "Pérez", ["MecanicoInput.SegundoApellido"] = segundoApellido,
            ["MecanicoInput.Genero"] = "Masculino", ["MecanicoInput.Especialidad"] = "Motores",
            ["MecanicoInput.Celular"] = "71234567"
        };
        if (termino is not null) campos["TerminoBusqueda"] = termino;
        if (seleccionado.HasValue) campos["MecanicoSeleccionadoId"] = seleccionado.Value.ToString();
        using var contenido = new FormUrlEncodedContent(campos);
        return await cliente.PostAsync($"/Mecanicos?handler={handler}", contenido);
    }

    private static async Task VerificarBuscador(
        HttpClient cliente, MySqlAisladoFixture aislada, string url, string password, int id, CookieContainer cookies)
    {
        var servicio = new MecanicoService(new CreadorMecanico(aislada.Factory), new ValidacionMecanicos());
        MecanicosAutocompletadoTests.Crear(servicio, "1234568", "Juan Carlos", "Pérez", "");
        MecanicosAutocompletadoTests.Crear(servicio, "7812345", "María Fernanda", "Muñoz", "");
        string antes = JsonSerializer.Serialize(servicio.Obtener());
        foreach (string termino in new[] { "", "a", "  " })
            Assert.Equal("[]", await cliente.GetStringAsync("/Mecanicos?handler=Sugerencias&termino=" + Uri.EscapeDataString(termino)));
        using var respuesta = await cliente.GetAsync("/Mecanicos?handler=Sugerencias&termino=123");
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetArrayLength());
        foreach (var sugerencia in json.RootElement.EnumerateArray())
        {
            Assert.Equal(new[] { "id", "textoPrincipal", "textoSecundario" }, sugerencia.EnumerateObject().Select(propiedad => propiedad.Name).ToArray());
            Assert.Contains("CI:", sugerencia.GetProperty("textoSecundario").GetString());
        }

        string seleccion = $"/Mecanicos?TerminoBusqueda=Juan+Carlos+P%C3%A9rez&MecanicoSeleccionadoId={id}";
        string listado = await cliente.GetStringAsync(seleccion);
        Assert.Single(Regex.Matches(listado, "data-editar-mecanico\\s"));
        Assert.Contains($"data-id=\"{id}\"", listado);
        Assert.Contains("Limpiar", listado);
        Assert.Equal(3, Regex.Matches(await cliente.GetStringAsync("/Mecanicos?TerminoBusqueda=Motores"), "data-editar-mecanico\\s").Count);
        Assert.Equal(3, Regex.Matches(await cliente.GetStringAsync("/Mecanicos"), "data-editar-mecanico\\s").Count);
        Assert.DoesNotContain("data-editar-mecanico", await cliente.GetStringAsync("/Mecanicos?MecanicoSeleccionadoId=2147483647"));

        using (var anonimo = NuevoCliente(url))
        {
            using var denegada = await anonimo.GetAsync("/Mecanicos?handler=Sugerencias&termino=Juan");
            Assert.True(denegada.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized);
            Assert.DoesNotContain("textoPrincipal", await denegada.Content.ReadAsStringAsync());
        }
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        new UsuarioRepository(aislada.Factory).Agregar(new Usuario
        {
            Username = "recepcion_prueba", Rol = "Recepcionista", PasswordHash = hasher.GenerarHash(password),
            CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
        });
        using (var recepcionista = NuevoCliente(url))
        {
            await IniciarSesion(recepcionista, password, "recepcion_prueba");
            using var denegada = await recepcionista.GetAsync("/Mecanicos?handler=Sugerencias&termino=Juan");
            Assert.True(denegada.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Forbidden);
            Assert.DoesNotContain("textoPrincipal", await denegada.Content.ReadAsStringAsync());
            if (denegada.StatusCode == HttpStatusCode.Redirect)
                Assert.Contains("AccessDenied", denegada.Headers.Location?.ToString());
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
            await VerificarNavegador(url, cookies);
        Assert.Equal(antes, JsonSerializer.Serialize(servicio.Obtener()));

        using var invalido = await EnviarFormulario(cliente, listado, "Actualizar", "No2", id, "Juan Carlos Pérez", id);
        string conErrores = await invalido.Content.ReadAsStringAsync();
        Assert.Contains($"name=\"MecanicoSeleccionadoId\" value=\"{id}\"", conErrores);
        Assert.Single(Regex.Matches(conErrores, "data-editar-mecanico\\s"));
        using var actualizado = await EnviarFormulario(cliente, listado, "Actualizar", "", id, "Juan Carlos Pérez", id);
        Assert.Equal(HttpStatusCode.Redirect, actualizado.StatusCode);
        Assert.Contains($"mecanicoSeleccionadoId={id}", actualizado.Headers.Location?.ToString());
        Assert.Contains("terminoBusqueda=", actualizado.Headers.Location?.ToString());
        var camposEliminar = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ObtenerToken(listado), ["MecanicoId"] = id.ToString(),
            ["TerminoBusqueda"] = "Juan Carlos Pérez", ["MecanicoSeleccionadoId"] = id.ToString()
        };
        using var contenido = new FormUrlEncodedContent(camposEliminar);
        using var eliminado = await cliente.PostAsync("/Mecanicos?handler=Eliminar", contenido);
        Assert.Equal(HttpStatusCode.Redirect, eliminado.StatusCode);
        Assert.Contains($"mecanicoSeleccionadoId={id}", eliminado.Headers.Location?.ToString());
        Assert.DoesNotContain("data-editar-mecanico", await cliente.GetStringAsync(eliminado.Headers.Location));
        using var creado = await EnviarFormulario(cliente, listado, "Crear", "", 0, "Juan Carlos Pérez", id);
        Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
        Assert.Contains($"mecanicoSeleccionadoId={id}", creado.Headers.Location?.ToString());
        Assert.Equal(3, Regex.Matches(await cliente.GetStringAsync("/Mecanicos"), "data-editar-mecanico\\s").Count);
    }

    private static HttpClient NuevoCliente(string url) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, CookieContainer = new CookieContainer(),
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    }) { BaseAddress = new Uri(url) };

    private static async Task VerificarNavegador(string url, CookieContainer cookies)
    {
        var inicio = new ProcessStartInfo("node")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add("tests/mecanicos_autocompletado_ui.cjs");
        inicio.Environment["TALLER_MECANICOS_URL"] = url;
        inicio.Environment["TALLER_MECANICOS_COOKIES"] = JsonSerializer.Serialize(
            cookies.GetCookies(new Uri(url)).Cast<Cookie>().Select(cookie => new
            {
                name = cookie.Name, value = cookie.Value, url,
                httpOnly = cookie.HttpOnly, secure = cookie.Secure
            }));
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync();
        Task<string> errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await proceso.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
        Assert.True(proceso.ExitCode == 0, await salida + await errores);
    }
}
