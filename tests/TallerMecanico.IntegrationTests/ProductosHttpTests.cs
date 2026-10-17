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

public sealed class ProductosHttpTests
{
    [MySqlAisladoFact]
    public async Task ProductosHttps_AutocompletadoCrudPermisosYRetornoSinCambiarInventario()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = ProductosAutocompletadoTests.CrearServicio(aislada);
        int id = ProductosAutocompletadoTests.Crear(servicio, "FIL-001", "Filtro De Aceite");
        ProductosAutocompletadoTests.Crear(servicio, "FIL-002", "Filtro De Aceite", stock: 0);
        ProductosAutocompletadoTests.Crear(servicio, "X-FIL-003", "Bujía De Iridio");
        for (int indice = 0; indice < 11; indice++)
            ProductosAutocompletadoTests.Crear(servicio, $"CAP-{indice:000}", "Sensor De Presión");
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
        string rutaCertificado = Path.Combine(Path.GetTempPath(), $"productos_{Guid.NewGuid():N}.pfx");
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
                using var peticionAnonima = new HttpRequestMessage(HttpMethod.Get, "/Productos?handler=Sugerencias&termino=FIL");
                peticionAnonima.Headers.Add("X-Requested-With", "XMLHttpRequest");
                using var denegada = await anonimo.SendAsync(peticionAnonima);
                Assert.Equal(HttpStatusCode.Unauthorized, denegada.StatusCode);

                var cookies = new CookieContainer();
                using var admin = NuevoCliente(url, cookies);
                await IniciarSesion(admin, "Administrador", password);
                using var recepcionista = NuevoCliente(url, new CookieContainer());
                await IniciarSesion(recepcionista, "Recepcionista", password);
                recepcionista.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                using var permitida = await recepcionista.GetAsync("/Productos?handler=Sugerencias&termino=FIL");
                Assert.Equal(HttpStatusCode.OK, permitida.StatusCode);
                string antes = JsonSerializer.Serialize(servicio.Obtener());
                using var sugerencias = await admin.GetAsync("/Productos?handler=Sugerencias&termino=CAP");
                Assert.True(sugerencias.Headers.CacheControl?.NoStore);
                Assert.Equal("application/json", sugerencias.Content.Headers.ContentType?.MediaType);
                using var json = JsonDocument.Parse(await sugerencias.Content.ReadAsStringAsync());
                Assert.Equal(10, json.RootElement.GetArrayLength());
                foreach (var registro in json.RootElement.EnumerateArray())
                    Assert.Equal(new[] { "id", "textoPrincipal", "textoSecundario" }, registro.EnumerateObject().Select(propiedad => propiedad.Name));
                foreach (string termino in new[] { "", " ", "a", "' OR 1=1 --", "%%" })
                    Assert.Equal("[]", await admin.GetStringAsync("/Productos?handler=Sugerencias&termino=" + Uri.EscapeDataString(termino)));
                string seleccion = $"/Productos?TerminoBusqueda=Filtro&ProductoSeleccionadoId={id}";
                string listado = await admin.GetStringAsync(seleccion);
                Assert.Single(Regex.Matches(listado, "data-editar-producto\\s"));
                Assert.Contains("data-codigo=\"FIL-001\"", listado);
                Assert.Equal(3, Regex.Matches(await admin.GetStringAsync("/Productos?handler=Buscar&TerminoBusqueda=FIL"), "data-editar-producto\\s").Count);

                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_MODULE")))
                    await VerificarNavegador(url, cookies);
                Assert.Equal(antes, JsonSerializer.Serialize(servicio.Obtener()));
                Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));

                string listadoRecepcion = await recepcionista.GetStringAsync(seleccion);
                var formulario = ProductosAutocompletadoTests.Formulario();
                foreach (string handler in new[] { "Crear", "Actualizar", "Eliminar" })
                {
                    using var prohibida = await Enviar(recepcionista, listadoRecepcion, handler, formulario, id, id, null);
                    Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode);
                }
                Assert.Equal(antes, JsonSerializer.Serialize(servicio.Obtener()));
                Guid orden = Guid.NewGuid();
                formulario.Nombre = "Filtro Premium";
                using var actualizado = await Enviar(admin, listado, "Actualizar", formulario, id, id, orden);
                Assert.Equal(HttpStatusCode.Redirect, actualizado.StatusCode);
                Assert.Contains($"ProductoSeleccionadoId={id}", actualizado.Headers.Location?.ToString());
                Assert.Contains(orden.ToString(), actualizado.Headers.Location?.ToString());
                Assert.Equal(10, servicio.ObtenerPorId(id)!.Stock);
                Assert.Equal("Filtro Premium", servicio.ObtenerPorId(id)!.Nombre);
                formulario.Precio = -1;
                using var invalido = await Enviar(admin, listado, "Actualizar", formulario, id, id, orden);
                Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
                string conErrores = await invalido.Content.ReadAsStringAsync();
                Assert.Contains("El precio debe ser mayor a cero.", WebUtility.HtmlDecode(conErrores));
                Assert.Contains($"name=\"ProductoSeleccionadoId\" value=\"{id}\"", conErrores);
                Assert.Contains(orden.ToString(), conErrores);
                Assert.Equal(25m, servicio.ObtenerPorId(id)!.Precio);

                using var creado = await Enviar(admin, listado, "Crear", ProductosAutocompletadoTests.Formulario("NUE-001"), 0, id, null);
                Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
                Assert.Contains($"ProductoSeleccionadoId={id}", creado.Headers.Location?.ToString());
                using var eliminado = await Enviar(admin, listado, "Eliminar", formulario, id, id, orden);
                Assert.Equal(HttpStatusCode.Redirect, eliminado.StatusCode);
                Assert.Contains(orden.ToString(), eliminado.Headers.Location?.ToString());
                Assert.Contains($"ProductoSeleccionadoId={id}", eliminado.Headers.Location?.ToString());
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
        inicio.ArgumentList.Add(typeof(ProductoService).Assembly.Location);
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
        ProductoFormViewModel formulario, int id, int seleccionado, Guid? orden)
    {
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["ProductoId"] = id.ToString(),
            ["ProductoSeleccionadoId"] = seleccionado.ToString(), ["TerminoBusqueda"] = "Filtro",
            ["OrdenBorradorId"] = orden?.ToString() ?? "", ["ProductoInput.Codigo"] = formulario.Codigo!,
            ["ProductoInput.Nombre"] = formulario.Nombre!, ["ProductoInput.Precio"] = formulario.Precio!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ProductoInput.Stock"] = formulario.Stock!.Value.ToString(), ["ProductoInput.StockMinimo"] = formulario.StockMinimo!.Value.ToString()
        });
        return await cliente.PostAsync("/Productos?handler=" + handler, contenido);
    }

    private static async Task VerificarNavegador(string url, CookieContainer cookies)
    {
        var inicio = new ProcessStartInfo("node")
        {
            WorkingDirectory = MySqlAisladoFixture.RaizProyecto, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add("tests/productos_autocompletado_ui.cjs");
        inicio.Environment["TALLER_PRODUCTOS_URL"] = url;
        inicio.Environment["TALLER_PRODUCTOS_COOKIES"] = JsonSerializer.Serialize(cookies.GetCookies(new Uri(url)).Cast<Cookie>()
            .Select(cookie => new { name = cookie.Name, value = cookie.Value, url, httpOnly = cookie.HttpOnly, secure = cookie.Secure }));
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await proceso.WaitForExitAsync(limite.Token); }
        catch (OperationCanceledException) { proceso.Kill(entireProcessTree: true); throw; }
        Assert.True(proceso.ExitCode == 0, await salida + await errores);
    }
}
