namespace TallerMecanico.Aplicacion.Puertos;

// Puerto de entrada: lo que la presentación puede pedir sobre los usuarios.
public interface IGestionUsuariosPort
{
    string CrearInvitacion(
        string username,
        string email,
        string rol,
        string password,
        CancellationToken cancellationToken = default);

    bool ActivarUsuarioDesdeInvitacion(string token, out string? errorMessage);

    bool ActualizarUsuario(int id, string email, string rol, bool activo, out string? errorMessage);

    bool DesactivarUsuario(int id, out string? errorMessage);

    bool ReactivarUsuario(int id, out string? errorMessage);
}
