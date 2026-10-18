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

public sealed class ClientesHttpTests
{
    [MySqlAisladoFact]
    public async Task ClientesHttps_SugerenciasPermisosCrudFiltrosYContextos()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = ClientesAutocompletadoTests.CrearServicio(aislada);
        int id = ClientesAutocompletadoTests.Crear(servicio, ClientesAutocompletadoTests.Formulario("7812345", "1A"));
        ClientesAutocompletadoTests.Crear(servicio, ClientesAutocompletadoTests.Formulario("7812345", "2B"));
        var otro = ClientesAutocompletadoTests.Formulario("1278345");
        otro.Nombres = "Juan Carlos";
        ClientesAutocompletadoTests.Crear(servicio, otro);
        for (int indice = 0; indice < 10; indice++)
        {
            var formulario = ClientesAutocompletadoTests.Formulario($"789{indice:0000}");
            formulario.Nombres = "José Luis";
            ClientesAutocompletadoTests.Crear(servicio, formulario);
        }
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
        string rutaCertificado = Path.Combine(Path.GetTempPath(), $"clientes_{Guid.NewGuid():N}.pfx");
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
            Task<string> salida = servidor.StandardOutput.ReadToEndAsync();
            Task<string> errores = servidor.StandardError.ReadToEndAsync();
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
                using var peticionAnonima = new HttpRequestMessage(HttpMethod.Get, "/Clientes?handler=Sugerencias&termino=78");
                peticionAnonima.Headers.Add("X-Requested-With", "XMLHttpRequest");
                using var denegada = await anonimo.SendAsync(peticionAnonima);
                Assert.Equal(HttpStatusCode.Unauthorized, denegada.StatusCode);

                var cookies = new CookieContainer();
                using var admin = NuevoCliente(url, cookies);
                await IniciarSesion(admin, "Administrador", password);
                using var recepcionista = NuevoCliente(url, new CookieContainer());
                await IniciarSesion(recepcionista, "Recepcionista", password);
                using var permitida = await recepcionista.GetAsync("/Clientes?handler=Sugerencias&termino=78");
                Assert.Equal(HttpStatusCode.OK, permitida.StatusCode);
                string antes = JsonSerializer.Serialize(servicio.Obtener());
                using var sugerencias = await admin.GetAsync("/Clientes?handler=Sugerencias&termino=78");
                Assert.Equal("application/json", sugerencias.Content.Headers.ContentType?.MediaType);
                Assert.True(sugerencias.Headers.CacheControl?.NoStore);
                using var json = JsonDocument.Parse(await sugerencias.Content.ReadAsStringAsync());
                Assert.Equal(10, json.RootElement.GetArrayLength());
                foreach (var registro in json.RootElement.EnumerateArray())
                    Assert.Equal(new[] { "id", "textoPrincipal", "textoSecundario" }, registro.EnumerateObject().Select(propiedad => propiedad.Name));
                foreach (string termino in new[] { "", " ", "a", "' OR 1=1 --", "%%" })
                    Assert.Equal("[]", await admin.GetStringAsync("/Clientes?handler=Sugerencias&termino=" + Uri.EscapeDataString(termino)));
                string seleccion = $"/Clientes?TerminoBusqueda=Mar%C3%ADa&ClienteSeleccionadoId={id}";
                string listado = await admin.GetStringAsync(seleccion);
                Assert.Single(Regex.Matches(listado, "data-editar-cliente\\s"));
                Assert.Contains("data-complemento-ci=\"1A\"", listado);
                Assert.Equal(13, Regex.Matches(await admin.GetStringAsync("/Clientes?TerminoBusqueda=71234567"), "data-editar-cliente\\s").Count);

                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
                    await VerificarNavegador(url, cookies);
                Assert.Equal(antes, JsonSerializer.Serialize(servicio.Obtener()));

                Guid vehiculo = Guid.NewGuid(), orden = Guid.NewGuid();
                string contexto = seleccion + $"&BorradorVehiculoId={vehiculo}&OrdenBorradorId={orden}";
                listado = await admin.GetStringAsync(contexto);
                Assert.Contains($"name=\"BorradorVehiculoId\" value=\"{vehiculo}\"", listado);
                Assert.Contains($"name=\"OrdenBorradorId\" value=\"{orden}\"", listado);
                var formulario = ClientesAutocompletadoTests.Formulario("7812345", "1A");
                formulario.Nombres = "María Elena";
                using var actualizado = await Enviar(admin, listado, "Actualizar", formulario, id, id, null, orden);
                Assert.Equal(HttpStatusCode.Redirect, actualizado.StatusCode);
                Assert.Contains($"ClienteSeleccionadoId={id}", actualizado.Headers.Location?.ToString());
                Assert.Contains(orden.ToString(), actualizado.Headers.Location?.ToString());
                Assert.Equal("María Elena", servicio.ObtenerPorId(id)!.Nombres);
                formulario.Nombres = "Juan2";
                using var invalido = await Enviar(admin, listado, "Actualizar", formulario, id, id, vehiculo, orden);
                Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
                string conErrores = await invalido.Content.ReadAsStringAsync();
                Assert.Contains("Los nombres solo pueden contener letras y espacios.", WebUtility.HtmlDecode(conErrores));
                Assert.Contains($"name=\"ClienteSeleccionadoId\" value=\"{id}\"", conErrores);
                Assert.Equal("María Elena", servicio.ObtenerPorId(id)!.Nombres);

                var nuevo = ClientesAutocompletadoTests.Formulario("5566778");
                using var creado = await Enviar(admin, listado, "Crear", nuevo, 0, id, null, null);
                Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
                Assert.Contains($"ClienteSeleccionadoId={id}", creado.Headers.Location?.ToString());
                using var eliminado = await Enviar(admin, listado, "Eliminar", formulario, id, id, vehiculo, orden);
                Assert.Equal(HttpStatusCode.Redirect, eliminado.StatusCode);
                Assert.Contains($"ClienteSeleccionadoId={id}", eliminado.Headers.Location?.ToString());
                Assert.Contains(vehiculo.ToString(), eliminado.Headers.Location?.ToString());
                Assert.Contains(orden.ToString(), eliminado.Headers.Location?.ToString());
                Assert.Null(servicio.ObtenerPorId(id));
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

    private static ProcessStartInfo PrepararServidor(MySqlAisladoFixture aislada, string url, string certificado, string password)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(typeof(ClienteService).Assembly.Location);
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
    }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(3) };

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(
        Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]*>").Value,
        "value=\"([^\"]+)\"").Groups[1].Value);

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

    private static async Task<HttpResponseMessage> Enviar(HttpClient cliente, string html, string handler,
        TallerMecanico.Aplicacion.Dto.ClienteFormViewModel formulario, int id, int seleccionado, Guid? vehiculo, Guid? orden)
    {
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["ClienteId"] = id.ToString(),
            ["ClienteSeleccionadoId"] = seleccionado.ToString(), ["TerminoBusqueda"] = "María",
            ["BorradorVehiculoId"] = vehiculo?.ToString() ?? "", ["OrdenBorradorId"] = orden?.ToString() ?? "",
            ["ClienteInput.Ci"] = formulario.Ci!, ["ClienteInput.ComplementoCi"] = formulario.ComplementoCi!,
            ["ClienteInput.Nombres"] = formulario.Nombres!, ["ClienteInput.PrimerApellido"] = formulario.PrimerApellido!,
            ["ClienteInput.SegundoApellido"] = formulario.SegundoApellido!, ["ClienteInput.Celular"] = formulario.Celular!
        });
        return await cliente.PostAsync("/Clientes?handler=" + handler, contenido);
    }

    private static async Task VerificarNavegador(string url, CookieContainer cookies)
    {
        var inicio = new ProcessStartInfo("node")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add("tests/clientes_autocompletado_ui.cjs");
        inicio.Environment["TALLER_CLIENTES_URL"] = url;
        inicio.Environment["TALLER_CLIENTES_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
            .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await proceso.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
        Assert.True(proceso.ExitCode == 0, await salida + await errores);
    }
}
