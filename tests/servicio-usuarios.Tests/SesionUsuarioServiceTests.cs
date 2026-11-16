using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using Xunit;

namespace TallerMecanico.IntegrationTests;

// RF-07: la sesión dura lo que indica ConfiguracionTokens.
[Trait("Area", "Backend")]
public class SesionUsuarioServiceTests
{
    [Fact]
    public void Iniciar_UsaDuracionDeConfiguracionTokens()
    {
        var sesiones = new SesionesFalsas();
        var servicio = new SesionUsuarioService(
            sesiones, new ConfiguracionTokensSimulada { MinutosSesion = 45 });

        DateTime antes = DateTime.UtcNow;
        servicio.Iniciar(7);
        DateTime despues = DateTime.UtcNow;

        Assert.Equal(7, sesiones.UsuarioId);
        Assert.InRange(sesiones.VenceEn!.Value, antes.AddMinutes(45), despues.AddMinutes(45));
    }

    [Fact]
    public void Renovar_DevuelveLoQueRespondeLaBase()
    {
        var sesiones = new SesionesFalsas { PermitirRenovacion = false };
        var servicio = new SesionUsuarioService(sesiones, new ConfiguracionTokensSimulada());

        Assert.False(servicio.Renovar(7));

        sesiones.PermitirRenovacion = true;
        DateTime antes = DateTime.UtcNow;
        Assert.True(servicio.Renovar(7));
        Assert.True(sesiones.VenceEn >= antes.AddMinutes(30));
    }

    [Fact]
    public void Cerrar_QuitaLaSesion()
    {
        var sesiones = new SesionesFalsas();
        var servicio = new SesionUsuarioService(sesiones, new ConfiguracionTokensSimulada());

        servicio.Cerrar(7);

        Assert.Equal(7, sesiones.Cerrado);
    }

    private sealed class SesionesFalsas : ISesionUsuarioPort
    {
        public bool PermitirRenovacion { get; set; } = true;
        public int? UsuarioId { get; private set; }
        public DateTime? VenceEn { get; private set; }
        public int? Cerrado { get; private set; }

        public void Iniciar(int usuarioId, DateTime venceEn)
        {
            UsuarioId = usuarioId;
            VenceEn = venceEn;
        }

        public bool Renovar(int usuarioId, DateTime venceEn)
        {
            if (!PermitirRenovacion) return false;
            UsuarioId = usuarioId;
            VenceEn = venceEn;
            return true;
        }

        public void Cerrar(int usuarioId) => Cerrado = usuarioId;
    }
}
