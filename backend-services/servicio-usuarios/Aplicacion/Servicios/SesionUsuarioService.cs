using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Aplicacion.Servicios;

// RF-07: la sesión dura lo que indica ConfiguracionTokens y se renueva con cada
// petición del usuario. Si pasa ese tiempo sin actividad, la sesión vence.
public sealed class SesionUsuarioService
{
    private readonly ISesionUsuarioPort _sesiones;
    private readonly IConfiguracionTokensPort _configuracionTokens;

    public SesionUsuarioService(
        ISesionUsuarioPort sesiones,
        IConfiguracionTokensPort configuracionTokens)
    {
        _sesiones = sesiones;
        _configuracionTokens = configuracionTokens;
    }

    public void Iniciar(int usuarioId)
    {
        DateTime venceEn = DateTime.UtcNow + _configuracionTokens.ObtenerDuracionSesion();
        _sesiones.Iniciar(usuarioId, venceEn);
    }

    public bool Renovar(int usuarioId)
    {
        DateTime venceEn = DateTime.UtcNow + _configuracionTokens.ObtenerDuracionSesion();
        return _sesiones.Renovar(usuarioId, venceEn);
    }

    public void Cerrar(int usuarioId)
    {
        _sesiones.Cerrar(usuarioId);
    }
}
