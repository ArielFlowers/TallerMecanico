using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace TallerMecanico.Contratos.Usuarios;

// Reglas que usan por igual los formularios del Frontend y el Servicio 2,
// para que ambos acepten y rechacen exactamente lo mismo.
public static class ReglasCuenta
{
    public const string MensajeContrasena =
        "La contraseña debe tener entre 8 y 128 caracteres, con mayúscula, minúscula, número y carácter especial, sin espacios al principio ni al final.";

    public static bool UsuarioValido(string? username) =>
        !string.IsNullOrWhiteSpace(username) &&
        Regex.IsMatch(username, @"\A[a-zA-Z0-9._-]{3,50}\z", RegexOptions.CultureInvariant);

    public static bool EmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && email == email.Trim() &&
        new EmailAddressAttribute().IsValid(email);

    // Política de contraseña segura de la rúbrica: mínimo 8 caracteres, mayúscula,
    // minúscula, número y carácter especial.
    public static bool ContrasenaValida(string? contrasena) =>
        contrasena is not null && contrasena.Length is >= 8 and <= 128 && contrasena == contrasena.Trim() &&
        contrasena.Any(char.IsUpper) && contrasena.Any(char.IsLower) && contrasena.Any(char.IsDigit) &&
        contrasena.Any(caracter => !char.IsLetterOrDigit(caracter));
}
