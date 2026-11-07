using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace TallerMecanico.Pages.Auth;

// Validación de formularios; el Servicio 2 vuelve a validar cada solicitud.
internal static class ValidacionCuentaInput
{
    public static bool UsuarioValido(string? username) =>
        !string.IsNullOrWhiteSpace(username) && Regex.IsMatch(username,
            @"\A[a-zA-Z0-9._-]{3,50}\z", RegexOptions.CultureInvariant);

    public static bool EmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && email == email.Trim() &&
        new EmailAddressAttribute().IsValid(email);

    public static bool PasswordValido(string? password) =>
        password is not null && password.Length is >= 8 and <= 128 && password == password.Trim() &&
        password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit) &&
        password.Any(caracter => !char.IsLetterOrDigit(caracter));
}
