using System.ComponentModel.DataAnnotations;


namespace TallerMecanico.Pages.Auth;

public sealed class RegistroInput : IValidatableObject
{
    [Required(ErrorMessage = "Ingrese un nombre de usuario.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese su correo electrónico.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese una contraseña.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme su contraseña.")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(Password),
        ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!ReglasCuenta.UsuarioValido(Username))
        {
            yield return new ValidationResult(
                "Use entre 3 y 50 caracteres: letras, números, punto, guion o guion bajo.",
                new[] { nameof(Username) });
        }

        if (!ReglasCuenta.EmailValido(Email))
        {
            yield return new ValidationResult(
                "Ingrese un correo electrónico válido.",
                new[] { nameof(Email) });
        }

        if (!ReglasCuenta.ContrasenaValida(Password))
        {
            yield return new ValidationResult(
                ReglasCuenta.MensajeContrasena,
                new[] { nameof(Password) });
        }
    }
}