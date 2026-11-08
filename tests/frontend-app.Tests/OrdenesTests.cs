using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Sesion;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class OrdenesTests
{
    [Fact]
    public void Token_SeMantieneParaReenviosYPerteneceAlUsuario()
    {
        var usuario = new UsuarioPrueba();
        var contexto = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = new SesionPrueba() } };
        var tokens = new TokenOrdenSesion(contexto, usuario, TimeProvider.System);
        Guid token = tokens.Generar();
        Assert.True(tokens.EsValido(token));
        Assert.True(tokens.EsValido(token));
        Assert.False(tokens.EsValido(Guid.Empty));
        Assert.False(tokens.EsValido(Guid.NewGuid()));
        usuario.Username = "otro";
        Assert.False(tokens.EsValido(token));
    }

    [Fact]
    public void Token_VenceYSeLimpiaAlCerrarSesion()
    {
        var usuario = new UsuarioPrueba();
        var sesion = new SesionPrueba();
        var contexto = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = sesion } };
        var reloj = new RelojPrueba();
        var tokens = new TokenOrdenSesion(contexto, usuario, reloj);
        Guid token = tokens.Generar();
        reloj.Ahora += TimeSpan.FromMinutes(31);
        Assert.False(tokens.EsValido(token));
        token = tokens.Generar();
        sesion.Clear();
        Assert.False(tokens.EsValido(token));
        usuario.EstaAutenticado = false;
        Assert.Throws<UnauthorizedAccessException>(() => tokens.Generar());
    }

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
