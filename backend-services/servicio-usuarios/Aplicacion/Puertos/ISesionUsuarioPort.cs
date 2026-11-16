namespace TallerMecanico.Aplicacion.Puertos;

// Guarda en Usuarios.SesionVenceEn hasta cuándo dura la sesión abierta.
public interface ISesionUsuarioPort
{
    void Iniciar(int usuarioId, DateTime venceEn);

    // Solo renueva una sesión que todavía no venció.
    bool Renovar(int usuarioId, DateTime venceEn);

    void Cerrar(int usuarioId);
}
