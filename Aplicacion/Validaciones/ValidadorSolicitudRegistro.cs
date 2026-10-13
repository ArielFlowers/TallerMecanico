using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace TallerMecanico.Validators;

public static class ValidadorSolicitudRegistro
{
    public static bool UsuarioValido(string? username)
    {
        return !string.IsNullOrWhiteSpace(username)
            && Regex.IsMatch(
                username,
                @"\A[a-zA-Z0-9._-]{3,50}\z",
                RegexOptions.CultureInvariant);
    }

    public static bool EmailValido(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)
            || email.Length > 254
            || email != email.Trim())
        {
            return false;
        }

        var atributo = new EmailAddressAttribute();

        return atributo.IsValid(email);
    }

    public static bool PasswordValido(string? password)
    {
        return password is not null
            && password.Length >= 12
            && password.Length <= 128
            && password == password.Trim();
    }
}