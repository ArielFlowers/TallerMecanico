
using System.ComponentModel.DataAnnotations;

namespace TallerMecanico.Pages.Auth;

public sealed class LoginInput
{
    [Required(ErrorMessage = "Ingrese su usuario.")]
    [StringLength(
        100,
        ErrorMessage = "El usuario no puede superar 100 caracteres.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese su contraseña.")]
    [StringLength(
        128,
        ErrorMessage = "La contraseña no puede superar 128 caracteres.")]
    public string Password { get; set; } = string.Empty;
}
