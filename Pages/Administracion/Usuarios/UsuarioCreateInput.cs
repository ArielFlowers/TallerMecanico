using System.ComponentModel.DataAnnotations;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.Pages.Administracion.Usuarios;

public sealed class UsuarioCreateInput : IValidatableObject
{
    [StringLength(50, MinimumLength = 3, ErrorMessage = "El usuario debe tener entre 3 y 50 caracteres.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese un correo electrónico.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El correo no puede superar 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione un rol.")]
    public string Rol { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese una contraseña.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; set; } = string.Empty;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ValidadorSolicitudRegistro.PasswordValido(Password))
        {
            yield return new ValidationResult(
                "La contraseña debe tener entre 8 y 128 caracteres, con mayúscula, minúscula, número y carácter especial, sin espacios al principio ni al final.",
                new[] { nameof(Password) });
        }

        if (!string.IsNullOrWhiteSpace(Username) &&
            !ValidadorSolicitudRegistro.UsuarioValido(Username))
        {
            yield return new ValidationResult(
                "Use entre 3 y 50 caracteres: letras, números, punto, guion o guion bajo.",
                new[] { nameof(Username) });
        }

        if (!ValidadorSolicitudRegistro.EmailValido(Email))
        {
            yield return new ValidationResult(
                "Ingrese un correo electrónico válido.",
                new[] { nameof(Email) });
        }

        if (Rol != "Administrador" && Rol != "Recepcionista")
        {
            yield return new ValidationResult(
                "El rol debe ser Administrador o Recepcionista.",
                new[] { nameof(Rol) });
        }
    }
}