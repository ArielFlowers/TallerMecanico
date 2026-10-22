using System.ComponentModel.DataAnnotations;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.Pages.Administracion.Usuarios;

public sealed class UsuarioEditInput : IValidatableObject
{
    [Required(ErrorMessage = "Ingrese un correo electrónico.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El correo no puede superar 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione un rol.")]
    public string Rol { get; set; } = string.Empty;

    [Display(Name = "Activo")]
    public bool Activo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
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