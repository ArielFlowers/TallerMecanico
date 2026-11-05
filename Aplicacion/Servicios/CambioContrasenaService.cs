using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class CambioContrasenaService(
    IUsuarioPort usuarios,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IAuditoriaPort auditoria)
{
    public bool Cambiar(
        string actual,
        string nueva,
        string confirmacion,
        out string error)
    {
        error = string.Empty;

        if (!currentUser.EstaAutenticado ||
            currentUser.UsuarioId is not int id)
        {
            error = "Debe iniciar sesión.";
            return false;
        }

        if (string.IsNullOrEmpty(actual) ||
            string.IsNullOrEmpty(nueva) ||
            string.IsNullOrEmpty(confirmacion))
        {
            error = "Todos los campos son obligatorios.";
            return false;
        }

        if (nueva != confirmacion)
        {
            error = "Las contraseñas nuevas no coinciden.";
            return false;
        }

        if (nueva.Length < 8 || nueva.Length > 128 ||
            !nueva.Any(char.IsUpper) ||
            !nueva.Any(char.IsLower) ||
            !nueva.Any(char.IsDigit) ||
            !nueva.Any(c => !char.IsLetterOrDigit(c)))
        {
            error = "La nueva contraseña debe tener entre 8 y 128 caracteres, "
                  + "mayúscula, minúscula, número y carácter especial.";
            return false;
        }

        Usuario? usuario = usuarios.ObtenerPorId(id);

        if (usuario is null || !usuario.Activo)
        {
            error = "La cuenta no está disponible.";
            return false;
        }

        if (!passwordHasher.VerificarPassword(actual, usuario.PasswordHash))
        {
            error = "La contraseña actual es incorrecta.";
            return false;
        }

        if (passwordHasher.VerificarPassword(nueva, usuario.PasswordHash))
        {
            error = "La contraseña nueva debe ser diferente de la actual.";
            return false;
        }

        usuario.PasswordHash = passwordHasher.GenerarHash(nueva);
        usuario.ModificadoPor = usuario.Username;
        usuario.FechaModificacion = DateTime.UtcNow;

        usuarios.Actualizar(usuario);

        auditoria.Registrar(new RegistroAuditoria
        {
            UsuarioId = usuario.Id,
            Username = usuario.Username,
            Accion = "CONTRASENA_CAMBIADA",
            Entidad = "Usuario",
            EntidadId = usuario.Id.ToString(),
            Fecha = DateTime.UtcNow,
            Detalle = "Cambio de contraseña realizado por el titular."
        });

        return true;
    }
}