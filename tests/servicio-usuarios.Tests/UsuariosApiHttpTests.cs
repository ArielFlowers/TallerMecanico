using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthenticationSchemeOptions = Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos;
using TallerMecanico.Controllers;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

// Frontera HTTP real con persistencia y SMTP en memoria; nunca usa Aiven ni .env.
[Trait("Area", "API")]
public sealed class UsuariosApiHttpTests(UsuariosApiHost host) : IClassFixture<UsuariosApiHost>
{
    [Fact]
    public async Task ApiSinClaveInterna_RechazaAcceso()
    {
        using var cliente = host.Cliente(conClave: false);
        using var respuesta = await cliente.GetAsync("api/auth/estado/7");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Recepcionista")]
    [InlineData("Administrador")]
    public async Task Estado_NoExigeRol_YEntregaElRolActual(string? rol)
    {
        using var cliente = host.Cliente(rol);
        var estado = await cliente.GetFromJsonAsync<EstadoUsuarioDto>("api/auth/estado/7");
        Assert.NotNull(estado);
        Assert.True(estado.Activo);
        Assert.Equal("Recepcionista", estado.Rol);
        Assert.Equal("recepcionista", estado.Username);
    }

    [Fact]
    public async Task Estado_UsuarioDesactivadoOInexistente_NoConservaSesion()
    {
        using var cliente = host.Cliente("Recepcionista");
        foreach (int id in new[] { 8, 9999 })
        {
            var estado = await cliente.GetFromJsonAsync<EstadoUsuarioDto>($"api/auth/estado/{id}");
            Assert.NotNull(estado);
            Assert.False(estado.Activo);
        }
    }

    [Fact]
    public async Task Recepcionista_NoPuedeAdministrarUsuarios()
    {
        using var cliente = host.Cliente("Recepcionista");
        using var respuesta = await cliente.GetAsync("api/usuarios");
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Login_DevuelveSoloSesion_YAuditaExitoYFallo()
    {
        using var cliente = host.Cliente();
        using var exito = await cliente.PostAsJsonAsync("api/auth/login", new CredencialesLogin("recepcionista", "Clave123!"));
        Assert.Equal(HttpStatusCode.OK, exito.StatusCode);
        string cuerpo = await exito.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(cuerpo);
        Assert.Equal(new[] { "id", "username", "rol" }, documento.RootElement.EnumerateObject().Select(propiedad => propiedad.Name));
        Assert.DoesNotContain("HASH", cuerpo);
        Assert.DoesNotContain("Clave123!", cuerpo);

        using var fallo = await cliente.PostAsJsonAsync("api/auth/login", new CredencialesLogin("recepcionista", "incorrecta"));
        Assert.Equal(HttpStatusCode.Unauthorized, fallo.StatusCode);
        Assert.Contains(host.Auditoria.Eventos, evento => evento.Accion == "LOGIN_EXITOSO" && evento.UsuarioId == 7);
        Assert.Contains(host.Auditoria.Eventos, evento => evento.Accion == "LOGIN_FALLIDO" && evento.UsuarioId is null);
    }

    [Fact]
    public async Task ListadoYEdicion_NoExponenCredenciales()
    {
        using var cliente = host.Cliente("Administrador");
        foreach (string ruta in new[] { "api/usuarios", "api/usuarios/7" })
        {
            using var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            string cuerpo = await respuesta.Content.ReadAsStringAsync();
            Assert.DoesNotContain("password", cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HASH", cuerpo);
        }
    }

    [Fact]
    public async Task InvitacionDuplicada_RespondeConflictoConCampo()
    {
        using var cliente = host.Cliente("Administrador");
        using var respuesta = await cliente.PostAsJsonAsync("api/usuarios", new CrearInvitacionSolicitud(
            "recepcionista@example.com", "Recepcionista", "Clave123!"));
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<ErrorApi>();
        Assert.NotNull(error);
        Assert.Equal("Email", error.Campo);
        Assert.Equal("SolicitudDuplicada", error.Codigo);
    }

    [Fact]
    public async Task Administrador_NoPuedeDesactivarSuCuenta()
    {
        using var cliente = host.Cliente("Administrador");
        using var respuesta = await cliente.PostAsync("api/usuarios/1/desactivar", null);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("propia cuenta", await respuesta.Content.ReadAsStringAsync());
    }
}

public sealed class UsuariosApiHost : IAsyncLifetime
{
    private WebApplication _app = null!;
    private string _url = string.Empty;
    internal AuditoriaApiEnMemoria Auditoria { get; } = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(AuthController).Assembly.GetName().Name
        });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ClaveInterna"] = "clave-exclusiva-de-pruebas-api",
            ["App:PublicBaseUrl"] = "https://localhost",
            ["ConnectionStrings:MySqlConnection"] = "Server=127.0.0.1;Database=autotaller_test_sin_conexion;User ID=prueba;Password=prueba;"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers(opciones => opciones.Filters.Add<ErroresApiFilter>())
            .AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthentication(ClaveInternaHandler.Esquema)
            .AddScheme<AuthenticationSchemeOptions, ClaveInternaHandler>(ClaveInternaHandler.Esquema, null);
        builder.Services.AddAuthorization(opciones => opciones.FallbackPolicy =
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<Argon2idOptions>(builder.Configuration.GetSection(Argon2idOptions.SectionName));
        builder.Services.AddSingleton<IFallbackHash, AuthenticationFallbackHash>();
        builder.Services.AddScoped<IActivacionInvitacionPort, ActivacionInvitacionMySqlAdapter>();
        builder.Services.AddScoped<IRevisionSolicitudesPort, RevisionSolicitudesRepository>();
        builder.Services.AddScoped<DatabaseConnectionFactory, MySqlConnectionFactory>();
        builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
        builder.Services.AddScoped<IGeneradorNombreUsuarioPort, GeneradorNombreUsuarioService>();
        builder.Services.AddScoped<IGestionUsuariosPort, UsuarioAdministracionService>();
        builder.Services.AddScoped<ICambioContrasenaPort, CambioContrasenaService>();
        builder.Services.AddScoped<RegistroUsuarioService>();
        builder.Services.AddScoped<RegistroFacade>();
        builder.Services.AddScoped<VerificacionEmailService>();
        builder.Services.AddScoped<ReenvioVerificacionService>();
        builder.Services.AddScoped<RevisionSolicitudesService>();
        var usuarios = new UsuariosApiEnMemoria();
        builder.Services.AddSingleton<IUsuarioPort>(usuarios);
        builder.Services.AddSingleton<IUsuarioAdministracionPort>(usuarios);
        builder.Services.AddSingleton<IUsuarioConsultaPort>(usuarios);
        builder.Services.AddSingleton<ISolicitudRegistroPort>(new RegistroCorreoFixture.SolicitudesEnMemoria());
        builder.Services.AddSingleton<IEmailService>(new RegistroCorreoFixture.CorreoSimulado());
        builder.Services.AddSingleton<IPasswordHasher, HasherApiPrueba>();
        builder.Services.AddSingleton<IAuditoriaPort>(Auditoria);
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        _url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public HttpClient Cliente(string? rol = null, bool conClave = true)
    {
        var cliente = new HttpClient { BaseAddress = new Uri(_url) };
        if (conClave) cliente.DefaultRequestHeaders.Add("X-Clave-Interna", "clave-exclusiva-de-pruebas-api");
        if (rol is not null)
        {
            cliente.DefaultRequestHeaders.Add("X-Usuario-Id", rol == "Administrador" ? "1" : "7");
            cliente.DefaultRequestHeaders.Add("X-Usuario-Nombre", rol == "Administrador" ? "admin" : "recepcionista");
            cliente.DefaultRequestHeaders.Add("X-Usuario-Rol", rol);
        }
        return cliente;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private sealed class HasherApiPrueba : IPasswordHasher
    {
        public string GenerarHash(string password) => "HASH_" + password;
        public bool VerificarPassword(string password, string passwordHash) => GenerarHash(password) == passwordHash;
    }

    internal sealed class AuditoriaApiEnMemoria : IAuditoriaPort
    {
        public ConcurrentQueue<RegistroAuditoria> Eventos { get; } = new();
        public void Registrar(RegistroAuditoria registro) => Eventos.Enqueue(registro);
        public IReadOnlyList<RegistroAuditoria> ObtenerRecientes(int cantidad) => Eventos.TakeLast(cantidad).ToArray();
    }

    private sealed class UsuariosApiEnMemoria : IUsuarioAdministracionPort
    {
        private readonly Usuario[] _usuarios =
        [
            new() { Id = 1, Username = "admin", Email = "admin@example.com", Rol = "Administrador", Activo = true, EmailVerificado = true, PasswordHash = "HASH_Clave123!" },
            new() { Id = 7, Username = "recepcionista", Email = "recepcionista@example.com", Rol = "Recepcionista", Activo = true, EmailVerificado = true, PasswordHash = "HASH_Clave123!" },
            new() { Id = 8, Username = "desactivado", Rol = "Recepcionista", Activo = false, EmailVerificado = true, PasswordHash = "HASH_Clave123!" }
        ];
        public Usuario? ObtenerPorId(int id) => _usuarios.FirstOrDefault(usuario => usuario.Id == id);
        public Usuario? ObtenerPorUsername(string username) => _usuarios.FirstOrDefault(usuario => usuario.Username == username);
        public Usuario? ObtenerPorIdConCredenciales(int id) => ObtenerPorId(id);
        public bool ExisteUsername(string username) => _usuarios.Any(usuario => usuario.Username == username);
        public bool ExisteEmail(string email) => _usuarios.Any(usuario => usuario.Email == email);
        public void Agregar(Usuario usuario) => throw new NotSupportedException();
        public void Actualizar(Usuario usuario) => throw new NotSupportedException();
        public IReadOnlyList<UsuarioListadoDto> Listar(int pagina, int tamanoPagina) => _usuarios.Select(usuario =>
            new UsuarioListadoDto(usuario.Id, usuario.Username, usuario.Email ?? string.Empty, usuario.Rol, usuario.Activo, usuario.FechaCreacion)).ToArray();
        public IReadOnlyList<UsuarioListadoDto> ListarFiltrados(string? busqueda, string? rol, bool? activo, int pagina, int tamanoPagina) => Listar(pagina, tamanoPagina);
        public int ContarTotal() => _usuarios.Length;
        public int ContarFiltrados(string? busqueda, string? rol, bool? activo) => _usuarios.Length;
        public int ContarUsuariosActivos() => _usuarios.Count(usuario => usuario.Activo);
        public int ContarAdministradoresActivosListado() => ContarAdministradoresActivos();
        public int ContarAdministradoresActivos() => _usuarios.Count(usuario => usuario.Activo && usuario.Rol == "Administrador");
        public bool ActualizarUsuarioAtomico(int id, string email, string rol, bool activo, string modificadoPor, out string errorMessage) => throw new NotSupportedException();
        public bool DesactivarUsuarioAtomico(int id, string modificadoPor, out string errorMessage) => throw new NotSupportedException();
    }
}
