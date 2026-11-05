using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Seguridad;
using IAutenticacionHttp = Microsoft.AspNetCore.Authentication.IAuthenticationService;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ValidarUsuarioActivoMiddlewareTests
{
    [Fact]
    public async Task UsuarioVigente_ConservaSesionYContinua()
    {
        var resultado = await InvocarAsync(
            Principal(new Claim(ClaimTypes.NameIdentifier, "7"),
                new Claim(ClaimTypes.Role, "Administrador")),
            new Usuario { Id = 7, Rol = "Administrador", Activo = true });

        Assert.True(resultado.Continuo);
        Assert.Null(resultado.Autenticacion.EsquemaCerrado);
        Assert.True(resultado.Contexto.User.Identity?.IsAuthenticated);
        Assert.Equal("borrador-prueba", resultado.Contexto.Session.GetString("BorradorOrden"));
    }

    [Theory]
    [InlineData("Recepcionista", true)]
    [InlineData("Administrador", false)]
    [InlineData(null, true)]
    public async Task CuentaModificadaOEliminada_BorraSesionYDetienePeticion(
        string? rolActual, bool activo)
    {
        var resultado = await InvocarAsync(
            Principal(new Claim(ClaimTypes.NameIdentifier, "7"),
                new Claim(ClaimTypes.Role, "Administrador")),
            rolActual is null ? null : new Usuario { Id = 7, Rol = rolActual, Activo = activo });

        ComprobarRevocacion(resultado);
    }

    [Theory]
    [InlineData("sin-id")]
    [InlineData("id-no-numerico")]
    [InlineData("id-cero")]
    [InlineData("id-negativo")]
    [InlineData("id-duplicado")]
    [InlineData("sin-rol")]
    [InlineData("rol-duplicado")]
    public async Task IdentidadInvalida_CierraSesionSinConservarBorradores(string caso)
    {
        var claims = new List<Claim>();
        if (caso != "sin-id")
        {
            string id = caso switch
            {
                "id-no-numerico" => "invalido",
                "id-cero" => "0",
                "id-negativo" => "-7",
                _ => "7"
            };
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id));
        }
        if (caso == "id-duplicado")
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "8"));
        if (caso != "sin-rol")
            claims.Add(new Claim(ClaimTypes.Role, "Administrador"));
        if (caso == "rol-duplicado")
            claims.Add(new Claim(ClaimTypes.Role, "Recepcionista"));

        var resultado = await InvocarAsync(
            Principal(claims.ToArray()),
            new Usuario { Id = 7, Rol = "Administrador", Activo = true });

        ComprobarRevocacion(resultado);
    }

    [Fact]
    public async Task VisitanteAnonimo_ContinuaSinCerrarSesion()
    {
        var resultado = await InvocarAsync(new ClaimsPrincipal(new ClaimsIdentity()), null);

        Assert.True(resultado.Continuo);
        Assert.Null(resultado.Autenticacion.EsquemaCerrado);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

    private static void ComprobarRevocacion(Resultado resultado)
    {
        Assert.False(resultado.Continuo);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme,
            resultado.Autenticacion.EsquemaCerrado);
        Assert.Empty(resultado.Contexto.Session.Keys);
        Assert.False(resultado.Contexto.User.Identity?.IsAuthenticated);
        Assert.Equal(StatusCodes.Status302Found, resultado.Contexto.Response.StatusCode);
        Assert.Equal("/Auth/Login?expired=1", resultado.Contexto.Response.Headers.Location);
    }

    private static async Task<Resultado> InvocarAsync(ClaimsPrincipal principal, Usuario? usuario)
    {
        var autenticacion = new AutenticacionPrueba();
        using var servicios = new ServiceCollection()
            .AddSingleton<IAutenticacionHttp>(autenticacion)
            .BuildServiceProvider();
        var contexto = new DefaultHttpContext
        {
            User = principal,
            Session = new SesionPrueba(),
            RequestServices = servicios
        };
        contexto.Session.SetString("BorradorOrden", "borrador-prueba");
        contexto.Session.SetString("Username", "usuario-prueba");

        bool continuo = false;
        var middleware = new ValidarUsuarioActivoMiddleware(_ =>
        {
            continuo = true;
            return Task.CompletedTask;
        }, NullLogger<ValidarUsuarioActivoMiddleware>.Instance);

        await middleware.InvokeAsync(contexto, new UsuariosPrueba(usuario));
        return new Resultado(contexto, autenticacion, continuo);
    }

    private sealed record Resultado(
        DefaultHttpContext Contexto, AutenticacionPrueba Autenticacion, bool Continuo);

    private sealed class UsuariosPrueba(Usuario? usuario) : IUsuarioPort
    {
        public Usuario? ObtenerPorId(int id) => usuario?.Id == id ? usuario : null;
        public Usuario? ObtenerPorUsername(string username) => throw new NotSupportedException();
        public bool ExisteUsername(string username) => throw new NotSupportedException();
        public bool ExisteEmail(string email) => throw new NotSupportedException();
        public void Agregar(Usuario usuario) => throw new NotSupportedException();
        public void Actualizar(Usuario usuario) => throw new NotSupportedException();
    }

    private sealed class AutenticacionPrueba : IAutenticacionHttp
    {
        public string? EsquemaCerrado { get; private set; }
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            EsquemaCerrado = scheme;
            return Task.CompletedTask;
        }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            throw new NotSupportedException();
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            throw new NotSupportedException();
    }
}
