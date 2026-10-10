using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Application;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Infraestructura.Session;
using TallerMecanico.Services;
using TallerMecanico.Validators;

namespace TallerMecanico.IntegrationTests;

public sealed class OrdenesTests
{
    private static CrearOrdenSolicitud Solicitud() => new(1, 1, DateTime.Today, Guid.NewGuid(), [new(1, 1)]);

    [Fact]
    public void Validacion_AgrupaProductosYOrdenaIds()
    {
        var validador = new ValidacionOrdenes();
        Assert.Empty(validador.Validar(Solicitud()));
        Assert.Equal(new[] { new LineaOrdenSolicitud(2, 5), new LineaOrdenSolicitud(9, 3) },
            validador.AgruparLineas([new(9, 1), new(2, 2), new(9, 2), new(2, 3)]));
    }

    [Fact]
    public void Validacion_RechazaCabeceraTokenYDetallesVacios()
    {
        var errores = new ValidacionOrdenes().Validar(new(0, 0, default, Guid.Empty, []));
        Assert.Contains("VehiculoId", errores);
        Assert.Contains("MecanicoId", errores);
        Assert.Contains("Fecha", errores);
        Assert.Contains("Token", errores);
        Assert.Contains("Lineas", errores);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Validacion_RechazaProductoOCantidadInvalida(int producto, int cantidad)
    {
        Assert.NotEmpty(new ValidacionOrdenes().Validar(Solicitud() with { Lineas = [new(producto, cantidad)] }));
    }

    [Fact]
    public void Validacion_ImpideDesbordamientoAlAgrupar()
    {
        Assert.Throws<OverflowException>(() => new ValidacionOrdenes().AgruparLineas([new(1, int.MaxValue), new(1, 1)]));
    }

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

    [Theory]
    [InlineData(false, "Administrador")]
    [InlineData(true, "Invitado")]
    public void Seguridad_NoIniciaTransaccionSinPermiso(bool autenticado, string rol)
    {
        var usuario = new UsuarioPrueba { EstaAutenticado = autenticado, Rol = rol };
        var servicio = Servicio(new UnidadProhibida(), usuario);
        Assert.False(servicio.CrearOrden(Solicitud()).Exito);
        Assert.False(servicio.AnularOrden(1).Exito);
    }

    [Fact]
    public void Recepcionista_NoPuedeAnularYNingunUsuarioAceptaTokenAjeno()
    {
        var servicio = Servicio(new UnidadProhibida(), new UsuarioPrueba());
        Assert.False(servicio.AnularOrden(1).Exito);
        var facade = new OrdenServicioFacade(servicio, new TokensInvalidos());
        Assert.Contains("Token", facade.CrearOrden(Solicitud()).Errores);
    }

    [Fact]
    public void CantidadAgrupadaExcesiva_NoAbreTransaccion()
    {
        var servicio = Servicio(new UnidadProhibida(), new UsuarioPrueba());
        var solicitud = Solicitud() with { Lineas = [new(1, int.MaxValue), new(1, 1)] };
        Assert.Contains("Lineas", servicio.CrearOrden(solicitud).Errores);
    }

    private static OrdenServicioService Servicio(IUnidadTrabajoOrdenPort unidad, UsuarioPrueba usuario) =>
        new(unidad, usuario, new ValidacionOrdenes(), TimeProvider.System, NullLogger<OrdenServicioService>.Instance);

    private sealed class UnidadProhibida : IUnidadTrabajoOrdenPort
    {
        public ITransaccionOrden Iniciar() => throw new InvalidOperationException("No se debe abrir una transacción.");
    }

    private sealed class TokensInvalidos : ITokenOrdenPort
    {
        public Guid Generar() => Guid.NewGuid();
        public bool EsValido(Guid token) => false;
    }

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
