using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class VehiculosAutocompletadoHttpTests
{
    [MySqlAisladoFact]
    public async Task VehiculosHttps_BuscadorPermisosCrudClienteYContextos()
    {
        using var aislada = new MySqlAisladoFixture();
        int cliente = VehiculosAutocompletadoTests.CrearCliente(aislada);
        var servicio = VehiculosAutocompletadoTests.CrearServicio(aislada);
        int id = VehiculosAutocompletadoTests.Crear(servicio, "123ABC", cliente);
        VehiculosAutocompletadoTests.Crear(servicio, "1234ABC", cliente);
        VehiculosAutocompletadoTests.Crear(servicio, "AB123CD", cliente, extranjera: true);
        for (int indice = 0; indice < 10; indice++) VehiculosAutocompletadoTests.Crear(servicio, $"{800 + indice}ABC", cliente);
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
        string rutaCertificado = Path.Combine(Path.GetTempPath(), $"vehiculos_busqueda_{Guid.NewGuid():N}.pfx");
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
            Task<string> salida = servidor.StandardOutput.ReadToEndAsync(), errores = servidor.StandardError.ReadToEndAsync();
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
                using var peticionAnonima = new HttpRequestMessage(HttpMethod.Get, "/Vehiculos?handler=Sugerencias&termino=123");
                peticionAnonima.Headers.Add("X-Requested-With", "XMLHttpRequest");
                using var denegada = await anonimo.SendAsync(peticionAnonima);
                Assert.Equal(HttpStatusCode.Unauthorized, denegada.StatusCode);

                var cookies = new CookieContainer();
                using var admin = NuevoCliente(url, cookies);
                await IniciarSesion(admin, "Administrador", password);
                using var recepcionista = NuevoCliente(url, new CookieContainer());
                await IniciarSesion(recepcionista, "Recepcionista", password);
                using var permitida = await recepcionista.GetAsync("/Vehiculos?handler=Sugerencias&termino=123");
                Assert.Equal(HttpStatusCode.OK, permitida.StatusCode);
                string antes = JsonSerializer.Serialize(servicio.GetAll());
                string clientesAntes = JsonSerializer.Serialize(new ClienteRepository(aislada.Factory).GetAll());
                using var sugerencias = await admin.GetAsync("/Vehiculos?handler=Sugerencias&termino=Toyota");
                Assert.True(sugerencias.Headers.CacheControl?.NoStore);
                Assert.Equal("application/json", sugerencias.Content.Headers.ContentType?.MediaType);
                using var json = JsonDocument.Parse(await sugerencias.Content.ReadAsStringAsync());
                Assert.Equal(10, json.RootElement.GetArrayLength());
                foreach (var registro in json.RootElement.EnumerateArray())
                    Assert.Equal(new[] { "id", "textoPrincipal", "textoSecundario" }, registro.EnumerateObject().Select(propiedad => propiedad.Name));
                foreach (string termino in new[] { "", " ", "a", "' OR 1=1 --", "%%" })
                    Assert.Equal("[]", await admin.GetStringAsync("/Vehiculos?handler=Sugerencias&termino=" + Uri.EscapeDataString(termino)));
                string seleccion = $"/Vehiculos?handler=Buscar&Buscar=123ABC&VehiculoSeleccionadoId={id}";
                string listado = await admin.GetStringAsync(seleccion);
                Assert.Single(Regex.Matches(listado, "data-modal-abrir=\"modal-editar\""));
                Assert.Contains("data-placa=\"123ABC\"", listado);
                Assert.Equal(3, Regex.Matches(await admin.GetStringAsync("/Vehiculos?handler=Buscar&Buscar=123"), "data-modal-abrir=\"modal-editar\"").Count);

                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
                    await VerificarNavegador(url, cookies, cliente);
                Assert.Equal(antes, JsonSerializer.Serialize(servicio.GetAll()));
                Assert.Equal(clientesAntes, JsonSerializer.Serialize(new ClienteRepository(aislada.Factory).GetAll()));
                Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));

                var formulario = VehiculosAutocompletadoTests.Formulario(cliente);
                formulario.Id = id;
                formulario.Kilometraje = 2500;
                string listadoRecepcionista = await recepcionista.GetStringAsync(seleccion);
                using var actualizado = await Enviar(recepcionista, listadoRecepcionista, "Update", formulario, id, null);
                Assert.Equal(HttpStatusCode.Redirect, actualizado.StatusCode);
                Assert.Contains($"VehiculoSeleccionadoId={id}", actualizado.Headers.Location?.ToString());
                Assert.Equal(cliente, servicio.GetById(id)!.ClienteId);
                Assert.Equal(2500, servicio.GetById(id)!.Kilometraje);
                formulario.ClienteId = null;
                using var invalido = await Enviar(admin, listado, "Update", formulario, id, null);
                Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
                string conErrores = await invalido.Content.ReadAsStringAsync();
                Assert.Contains($"name=\"VehiculoSeleccionadoId\" value=\"{id}\"", conErrores);
                Assert.Contains("cliente", WebUtility.HtmlDecode(conErrores), StringComparison.OrdinalIgnoreCase);
                Assert.Equal(cliente, servicio.GetById(id)!.ClienteId);
                formulario.ClienteId = cliente;
                using var creado = await Enviar(admin, listado, "Create", VehiculosAutocompletadoTests.Formulario(cliente, "456XYZ"), id, null);
                Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
                Assert.Contains($"VehiculoSeleccionadoId={id}", creado.Headers.Location?.ToString());
                Guid orden = Guid.NewGuid();
                using var eliminado = await Enviar(admin, listado, "Delete", formulario, id, orden);
                Assert.Equal(HttpStatusCode.Redirect, eliminado.StatusCode);
                Assert.Contains(orden.ToString(), eliminado.Headers.Location?.ToString());
                Assert.Contains($"VehiculoSeleccionadoId={id}", eliminado.Headers.Location?.ToString());
                Assert.Null(servicio.GetById(id));
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
        inicio.ArgumentList.Add(typeof(VehiculoService).Assembly.Location);
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
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(await cliente.GetStringAsync("/Auth/Login")),
            ["Input.Username"] = usuario, ["Input.Password"] = password
        });
        using var respuesta = await cliente.PostAsync("/Auth/Login", contenido);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    private static async Task<HttpResponseMessage> Enviar(HttpClient cliente, string html, string handler,
        VehiculoFormViewModel formulario, int seleccionado, Guid? orden)
    {
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["id"] = formulario.Id.ToString(),
            ["VehiculoSeleccionadoId"] = seleccionado.ToString(), ["Buscar"] = "123ABC",
            ["OrdenBorradorId"] = orden?.ToString() ?? "", ["Formulario.Id"] = formulario.Id.ToString(),
            ["Formulario.Placa"] = formulario.Placa!, ["Formulario.Marca"] = formulario.Marca!,
            ["Formulario.Modelo"] = formulario.Modelo!, ["Formulario.Kilometraje"] = formulario.Kilometraje!.Value.ToString(),
            ["Formulario.ClienteId"] = formulario.ClienteId?.ToString() ?? "", ["Formulario.EsPlacaExtranjera"] = "false"
        });
        return await cliente.PostAsync("/Vehiculos?handler=" + handler, contenido);
    }

    private static async Task VerificarNavegador(string url, CookieContainer cookies, int cliente)
    {
        var inicio = new ProcessStartInfo("node")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add("tests/vehiculos_autocompletado_ui.cjs");
        inicio.Environment["TALLER_VEHICULOS_URL"] = url;
        inicio.Environment["TALLER_VEHICULOS_CLIENTE"] = cliente.ToString();
        inicio.Environment["TALLER_VEHICULOS_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
            .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await proceso.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
        Assert.True(proceso.ExitCode == 0, await salida + await errores);
    }
}
