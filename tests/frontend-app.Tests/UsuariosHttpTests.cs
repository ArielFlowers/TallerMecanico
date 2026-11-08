using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Api;
using TallerMecanico.Contratos.Usuarios;
using TallerMecanico.Seguridad;
using TallerMecanico.Sesion;
using TallerMecanico.Pages.Auth;
using UsuariosIndex = TallerMecanico.Pages.Administracion.Usuarios.IndexModel;
using CrearUsuario = TallerMecanico.Pages.Administracion.Usuarios.CrearModel;
using EditarUsuario = TallerMecanico.Pages.Administracion.Usuarios.EditarModel;
using IAutenticacionHttp = Microsoft.AspNetCore.Authentication.IAuthenticationService;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class UsuariosHttpTests
{
    [Fact]
    public async Task Cliente_UsaRutasDtosYFiltrosCodificadosSinEnviarAuditoria()
    {
        var api = PrepararApi();
        var usuarios = new UsuariosApi(api.Cliente());
        Assert.NotNull(await usuarios.IniciarSesionAsync("admin", "ClavePrueba123!"));
        Assert.True((await usuarios.ObtenerEstadoAsync(7))!.Activo);
        Assert.Single(await usuarios.ListarAsync("Ana & José", "Administrador", true, 2, 10));
        Assert.Equal(11, await usuarios.ContarAsync("Ana & José", "Administrador", true));
        Assert.Equal(7, (await usuarios.ObtenerPorIdAsync(7))!.Id);
        Assert.Null(await usuarios.ObtenerPorIdAsync(99));
        Assert.Equal(11, (await usuarios.ObtenerResumenAsync()).Total);
        Assert.True((await usuarios.CrearInvitacionAsync(new("nuevo@example.test", "Recepcionista", "ClavePrueba123!"))).Exito);
        Assert.True((await usuarios.ActualizarAsync(7, new("admin@example.test", "Administrador", true))).Exito);
        Assert.True((await usuarios.DesactivarAsync(7)).Exito);
        Assert.True((await usuarios.ReactivarAsync(7)).Exito);
        Assert.True((await usuarios.CambiarContrasenaAsync(new("Anterior123!", "NuevaClave123!", "NuevaClave123!"))).Exito);
        Assert.True((await usuarios.VerificarEmailAsync(new string('a', 64))).Exito);
        Assert.True((await usuarios.ActivarInvitacionAsync(new string('b', 64))).Exito);
        Assert.True((await usuarios.ReenviarVerificacionAsync("admin@example.test")).Exito);
        Assert.True((await usuarios.CerrarSesionAsync()).Exito);
        Assert.Contains(api.Llamadas, ruta => ruta.StartsWith("GET api/usuarios?") && ruta.Contains("%26") && ruta.Contains("pagina=2"));
        foreach (string cuerpo in api.Cuerpos)
        {
            using var json = JsonDocument.Parse(cuerpo);
            Assert.False(json.RootElement.TryGetProperty("creadoPor", out _));
            Assert.False(json.RootElement.TryGetProperty("modificadoPor", out _));
        }
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    public async Task Login_RespuestaValidaCreaCookieEnFrontendYRenuevaSesion(string rol)
    {
        var api = PrepararApi(); api.Json("POST api/auth/login", new UsuarioSesionDto(7, "admin", rol));
        var autenticacion = new AutenticacionUsuariosPrueba();
        var pagina = Preparar(new LoginModel(new UsuariosApi(api.Cliente())), autenticacion);
        pagina.Input = new LoginInput { Username = "admin", Password = "ClavePrueba123!" };
        pagina.HttpContext.Session.SetString("BorradorOrden", "anterior");
        Assert.Equal("/Index", Assert.IsType<RedirectToPageResult>(await pagina.OnPostAsync()).PageName);
        Assert.Equal("7", autenticacion.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(rol, autenticacion.Principal!.FindFirstValue(ClaimTypes.Role));
        Assert.False(autenticacion.Propiedades!.IsPersistent);
        Assert.Null(pagina.HttpContext.Session.GetString("BorradorOrden"));
        Assert.Equal("admin", pagina.HttpContext.Session.GetString("Username"));
        Assert.Equal(["POST api/auth/login"], api.Llamadas);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_RechazoOIdentidadInvalidaNoCreaCookie(bool rechazada)
    {
        var api = PrepararApi();
        if (rechazada) api.Error("POST api/auth/login", HttpStatusCode.Unauthorized);
        else api.Json("POST api/auth/login", new UsuarioSesionDto(7, "admin", "RolInvalido"));
        var autenticacion = new AutenticacionUsuariosPrueba();
        var pagina = Preparar(new LoginModel(new UsuariosApi(api.Cliente())), autenticacion);
        pagina.Input = new() { Username = "admin", Password = "ClavePrueba123!" };
        Assert.IsType<PageResult>(await pagina.OnPostAsync());
        Assert.Null(autenticacion.Principal);
        Assert.Equal("Usuario o contraseña incorrectos.", Error(pagina));
    }

    [Fact]
    public async Task Login_NoDisponibleConservaFormularioSinMostrarDetalleInterno()
    {
        var api = PrepararApi(); api.Error("POST api/auth/login", HttpStatusCode.InternalServerError, "Dato privado");
        var autenticacion = new AutenticacionUsuariosPrueba();
        var pagina = Preparar(new LoginModel(new UsuariosApi(api.Cliente())), autenticacion);
        pagina.Input.Username = "admin";
        Assert.IsType<PageResult>(await pagina.OnPostAsync());
        Assert.Contains("no está disponible", Error(pagina));
        Assert.DoesNotContain("Dato privado", Error(pagina));
        Assert.Equal("admin", pagina.Input.Username);
        Assert.Null(autenticacion.Principal);
    }

    [Fact]
    public async Task Logout_BorraCookieYSesionAunqueLaApiFalle()
    {
        var api = PrepararApi(); api.Error("POST api/auth/logout", HttpStatusCode.InternalServerError);
        var autenticacion = new AutenticacionUsuariosPrueba();
        var pagina = Preparar(new LogoutModel(new UsuariosApi(api.Cliente()), NullLogger<LogoutModel>.Instance), autenticacion);
        pagina.HttpContext.Session.SetString("BorradorOrden", "borrador");
        Assert.Equal("/Auth/Login", Assert.IsType<RedirectToPageResult>(await pagina.OnPostAsync()).PageName);
        Assert.True(autenticacion.CerroSesion);
        Assert.Empty(pagina.HttpContext.Session.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CambiarContrasena_ConservaRechazoOSolicitaNuevoLogin(bool exito)
    {
        var api = PrepararApi();
        if (!exito) api.Error("POST api/auth/cambiar-contrasena", HttpStatusCode.BadRequest, "La contraseña actual no es correcta.");
        var autenticacion = new AutenticacionUsuariosPrueba();
        var pagina = Preparar(new CambiarContrasenaModel(new UsuariosApi(api.Cliente())), autenticacion);
        pagina.Input = new() { Actual = "Actual123!", Nueva = "NuevaClave123!", Confirmacion = "NuevaClave123!" };
        pagina.HttpContext.Session.SetString("BorradorOrden", "borrador");
        var resultado = await pagina.OnPostAsync();
        if (exito)
        {
            Assert.Equal("/Auth/Login", Assert.IsType<RedirectToPageResult>(resultado).PageName);
            Assert.True(autenticacion.CerroSesion);
            Assert.Empty(pagina.HttpContext.Session.Keys);
            Assert.Contains("Inicie sesión nuevamente", pagina.TempData["MensajeExito"]!.ToString());
        }
        else
        {
            Assert.IsType<PageResult>(resultado);
            Assert.Equal("La contraseña actual no es correcta.", Error(pagina));
            Assert.False(autenticacion.CerroSesion);
            Assert.Equal("borrador", pagina.HttpContext.Session.GetString("BorradorOrden"));
        }
    }

    [Fact]
    public async Task Invitacion_DuplicadaConservaCamposYAsociaErrorAlCorreo()
    {
        var api = PrepararApi(); api.Json("POST api/usuarios", new { });
        using var respuesta = new RespuestaUsuariosPrueba(HttpStatusCode.Conflict,
            new ErrorApi("Ya existe un usuario o solicitud pendiente con ese valor.", "Email", "SolicitudDuplicada"));
        var pagina = Preparar(new CrearUsuario(new UsuariosApi(respuesta.Cliente())));
        pagina.Input = new() { Email = "nuevo@example.test", Rol = "Recepcionista", Password = "ClavePrueba123!", ConfirmarPassword = "ClavePrueba123!" };
        Assert.IsType<PageResult>(await pagina.OnPostAsync());
        Assert.Contains("Ya existe", Error(pagina, "Input.Email"));
        Assert.Equal("nuevo@example.test", pagina.Input.Email);
        Assert.False(pagina.TempData.ContainsKey("MensajeExito"));
    }

    [Theory]
    [InlineData("CorreoPendiente", "La invitación se guardó pero no se pudo enviar el correo.")]
    [InlineData("ConfiguracionCorreo", "Error de configuración del servidor de correo.")]
    public async Task Invitacion_ErrorCorreoPreservaMensajeSinIndicarExito(string codigo, string mensaje)
    {
        using var respuesta = new RespuestaUsuariosPrueba(HttpStatusCode.ServiceUnavailable, new ErrorApi(mensaje, Codigo: codigo));
        var pagina = Preparar(new CrearUsuario(new UsuariosApi(respuesta.Cliente())));
        Assert.IsType<PageResult>(await pagina.OnPostAsync());
        Assert.Equal(mensaje, Error(pagina));
        Assert.False(pagina.TempData.ContainsKey("MensajeExito"));
    }

    [Fact]
    public async Task Listado_ConservaFiltrosPaginacionResumenYFragmento()
    {
        var api = PrepararApi();
        var pagina = Preparar(new UsuariosIndex(new UsuariosApi(api.Cliente())));
        pagina.Busqueda = " Ana & José "; pagina.RolFiltro = "Administrador"; pagina.ActivoFiltro = true; pagina.Pagina = 50;
        Assert.IsType<PageResult>(await pagina.OnGetAsync());
        Assert.Equal(2, pagina.Pagina);
        Assert.Equal(11, pagina.TotalRegistros);
        Assert.Equal(11, pagina.TotalUsuarios);
        Assert.Equal(8, pagina.UsuariosActivos);
        Assert.Equal("admin", Assert.Single(pagina.Usuarios).Username);
        Assert.Contains(api.Llamadas, ruta => ruta.Contains("pagina=2") && ruta.Contains("%26"));
        Assert.IsType<PartialViewResult>(await pagina.OnGetResultadosAsync());
    }

    [Fact]
    public async Task Edicion_RechazoConservaValoresYMensajeSinDeclararExito()
    {
        var api = PrepararApi(); api.Error("PUT api/usuarios/7", HttpStatusCode.BadRequest, "No puede cambiar su propio rol.");
        var pagina = Preparar(new EditarUsuario(new UsuariosApi(api.Cliente())));
        pagina.Input = new() { Email = "nuevo@example.test", Rol = "Recepcionista", Activo = true };
        Assert.IsType<PageResult>(await pagina.OnPostAsync(7));
        Assert.Equal("No puede cambiar su propio rol.", Error(pagina));
        Assert.Equal("nuevo@example.test", pagina.Input.Email);
        Assert.Equal("admin", pagina.Usuario!.Username);
        Assert.False(pagina.TempData.ContainsKey("MensajeExito"));
        Assert.IsType<NotFoundResult>(await pagina.OnGetAsync(99));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Administracion_ConservaRechazosDeAutenticacionYRol(int codigo)
    {
        var api = PrepararApi(); api.Error("GET api/usuarios/conteo", (HttpStatusCode)codigo);
        var pagina = Preparar(new UsuariosIndex(new UsuariosApi(api.Cliente())));
        var resultado = await pagina.OnGetAsync();
        if (codigo == 401) Assert.IsType<ChallengeResult>(resultado);
        else Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task ActivacionYVerificacion_ElGetNoConsumeTokenYPostUsaApi()
    {
        var api = PrepararApi(); string token = new('a', 64);
        var activar = Preparar(new ActivarInvitacionModel(new UsuariosApi(api.Cliente())));
        activar.OnGet(token);
        Assert.True(activar.TokenValido); Assert.Empty(api.Llamadas);
        Assert.Equal("no-store", activar.Response.Headers.CacheControl);
        Assert.IsType<PageResult>(await activar.OnPostAsync()); Assert.True(activar.Activado);
        Assert.Contains("POST api/auth/activar-invitacion", api.Llamadas);
        var verificar = Preparar(new VerificarEmailModel(new UsuariosApi(api.Cliente())));
        verificar.OnGet(token);
        Assert.True(verificar.PuedeConfirmar);
        Assert.IsType<PageResult>(await verificar.OnPostAsync()); Assert.True(verificar.Confirmado);
        Assert.Equal(string.Empty, verificar.Token);
    }

    [Fact]
    public async Task Verificacion_ErrorTemporalNoSeConfundeConUnTokenInvalido()
    {
        var api = PrepararApi();
        api.Error("POST api/auth/verificar-email", HttpStatusCode.ServiceUnavailable);
        var pagina = Preparar(new VerificarEmailModel(new UsuariosApi(api.Cliente())));
        pagina.Token = new string('a', 64);
        Assert.IsType<PageResult>(await pagina.OnPostAsync());
        Assert.False(pagina.Confirmado);
        Assert.Equal("El servicio de usuarios no está disponible. Intenta nuevamente.", pagina.Mensaje);
        Assert.Equal(new string('a', 64), pagina.Token);
    }

    [Fact]
    public async Task RegistroPublicoPermaneceRetiradoYReenvioNoRevelaExistenciaDelCorreo()
    {
        var registro = Preparar(new RegistroModel());
        Assert.Equal("/Auth/Login", Assert.IsType<RedirectToPageResult>(registro.OnGet()).PageName);
        Assert.IsType<NotFoundResult>(await registro.OnPostAsync(CancellationToken.None));
        var api = PrepararApi(); api.Error("POST api/auth/reenviar-verificacion", HttpStatusCode.InternalServerError);
        var reenvio = Preparar(new ReenviarVerificacionModel(new UsuariosApi(api.Cliente()), NullLogger<ReenviarVerificacionModel>.Instance));
        reenvio.Email = "alguien@example.test";
        Assert.IsType<PageResult>(await reenvio.OnPostAsync(CancellationToken.None));
        Assert.True(reenvio.SolicitudRecibida); Assert.Equal(string.Empty, reenvio.Email);
        Assert.True(reenvio.ModelState.IsValid);
    }

    [Fact]
    public async Task Middleware_ApiCaidaNoEjecutaPaginaNiRevocaCookiePorFalloTemporal()
    {
        var api = PrepararApi(); api.Error("GET api/auth/estado/7", HttpStatusCode.InternalServerError);
        var autenticacion = new AutenticacionUsuariosPrueba();
        var pagina = Preparar(new LoginModel(new UsuariosApi(api.Cliente())), autenticacion);
        bool continuo = false;
        var middleware = new ValidarUsuarioActivoMiddleware(_ => { continuo = true; return Task.CompletedTask; },
            NullLogger<ValidarUsuarioActivoMiddleware>.Instance);
        await middleware.InvokeAsync(pagina.HttpContext, new UsuariosApi(api.Cliente()));
        Assert.False(continuo); Assert.False(autenticacion.CerroSesion);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, pagina.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_PostLogoutContinuaSinConsultarApi()
    {
        var api = PrepararApi(); var pagina = Preparar(new LoginModel(new UsuariosApi(api.Cliente())));
        pagina.Request.Method = "POST"; pagina.Request.Path = "/Auth/Logout";
        bool continuo = false;
        var middleware = new ValidarUsuarioActivoMiddleware(_ => { continuo = true; return Task.CompletedTask; },
            NullLogger<ValidarUsuarioActivoMiddleware>.Instance);
        await middleware.InvokeAsync(pagina.HttpContext, new UsuariosApi(api.Cliente()));
        Assert.True(continuo); Assert.Empty(api.Llamadas);
    }

    private static ApiFalsa PrepararApi()
    {
        var api = new ApiFalsa();
        api.Json("POST api/auth/login", new UsuarioSesionDto(7, "admin", "Administrador"));
        api.Json("GET api/auth/estado/7", new EstadoUsuarioDto(true, "Administrador", "admin"));
        api.Json("GET api/usuarios", new[] { new UsuarioListadoDto(7, "admin", "admin@example.test", "Administrador", true, new DateTime(2026, 1, 2)) });
        api.Json("GET api/usuarios/conteo", 11);
        api.Json("GET api/usuarios/resumen", new ResumenUsuarios(11, 8, 2));
        api.Json("GET api/usuarios/7", new UsuarioDto(7, "admin", "admin@example.test", "Administrador", true));
        foreach (string ruta in new[] { "POST api/usuarios", "PUT api/usuarios/7", "POST api/usuarios/7/desactivar",
            "POST api/usuarios/7/reactivar", "POST api/auth/cambiar-contrasena", "POST api/auth/verificar-email",
            "POST api/auth/activar-invitacion", "POST api/auth/reenviar-verificacion", "POST api/auth/logout" }) api.Json(ruta, new { });
        return api;
    }

    private static T Preparar<T>(T pagina, AutenticacionUsuariosPrueba? autenticacion = null) where T : PageModel
    {
        var servicios = new ServiceCollection()
            .AddSingleton<IAutenticacionHttp>(autenticacion ?? new())
            .AddSingleton<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider,
                Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider>()
            .BuildServiceProvider();
        var contexto = new DefaultHttpContext
        {
            RequestServices = servicios,
            Session = new SesionUsuariosPrueba(),
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "Administrador")
            }, CookieAuthenticationDefaults.AuthenticationScheme))
        };
        contexto.Response.Body = new MemoryStream();
        var pageContext = new PageContext { HttpContext = contexto };
        pageContext.ViewData = new ViewDataDictionary(
            new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), pageContext.ModelState);
        pagina.PageContext = pageContext;
        pagina.TempData = new TempDataDictionary(contexto, new TempDataUsuariosPrueba());
        return pagina;
    }

    private static string Error(PageModel pagina, string campo = "") =>
        Assert.Single(pagina.ModelState[campo]!.Errors).ErrorMessage;

    private sealed class TempDataUsuariosPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class RespuestaUsuariosPrueba(HttpStatusCode codigo, ErrorApi error) : HttpMessageHandler
    {
        public HttpClient Cliente() => new(this) { BaseAddress = new Uri("http://usuarios.prueba/") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(codigo) { Content = System.Net.Http.Json.JsonContent.Create(error) });
    }
}

internal sealed class SesionUsuariosPrueba : ISession
{
    private readonly Dictionary<string, byte[]> _datos = [];
    public bool IsAvailable => true;
    public string Id => "sesion-usuarios-prueba";
    public IEnumerable<string> Keys => _datos.Keys;
    public void Clear() => _datos.Clear();
    public void Remove(string key) => _datos.Remove(key);
    public void Set(string key, byte[] value) => _datos[key] = value;
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => _datos.TryGetValue(key, out value);
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class AutenticacionUsuariosPrueba : IAutenticacionHttp
{
    public bool CerroSesion { get; private set; }
    public ClaimsPrincipal? Principal { get; private set; }
    public AuthenticationProperties? Propiedades { get; private set; }
    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    { CerroSesion = true; return Task.CompletedTask; }
    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    { Principal = principal; Propiedades = properties; return Task.CompletedTask; }
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
}
