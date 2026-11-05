namespace TallerMecanico.Aplicacion.Puertos;

// Puerto de entrada: cambio de contraseña del usuario que inició sesión.
public interface ICambioContrasenaPort
{
    bool Cambiar(string actual, string nueva, string confirmacion, out string error);
}
