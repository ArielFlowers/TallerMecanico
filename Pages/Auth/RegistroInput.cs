using System.ComponentModel.DataAnnotations;
using TallerMecanico.Validators;

namespace TallerMecanico.Pages.Auth;

public sealed class RegistroInput : IValidatableObject
{
    [Required(ErrorMessage = "Ingrese un nombre de usuario.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese su correo electronico.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese una contrasena.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme su contrasena.")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(Password),
        ErrorMessage = "Las contrasenas no coinciden.")]
    public string ConfirmarPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!ValidadorSolicitudRegistro.UsuarioValido(Username))
        {
            yield return new ValidationResult(
                "Use entre 3 y 50 caracteres: letras, numeros, punto, guion o guion bajo.",
                new[] { nameof(Username) });
        }

        if (!ValidadorSolicitudRegistro.EmailValido(Email))
        {
            yield return new ValidationResult(
                "Ingrese un correo electronico valido.",
                new[] { nameof(Email) });
        }

        if (!ValidadorSolicitudRegistro.PasswordValido(Password))
        {
            yield return new ValidationResult(
                "La contrasena debe tener entre 12 y 128 caracteres y no comenzar ni terminar con espacios.",
                new[] { nameof(Password) });
        }
    }
}