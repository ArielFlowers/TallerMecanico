using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("EmailVerificationRateLimit")]
public sealed class VerificarEmailModel : PageModel
{
    private readonly VerificacionEmailService _verificacion;

    public VerificarEmailModel(
        VerificacionEmailService verificacion)
    {
        _verificacion = verificacion;
    }

    [BindProperty]
    public string Token { get; set; } = string.Empty;

    public bool PuedeConfirmar { get; private set; }

    public string? Mensaje { get; private set; }

    public bool Confirmado { get; private set; }

    public void OnGet(string? token)
    {
        ConfigurarSeguridad();

        if (TokenValido(token))
        {
            Token = token!;
            PuedeConfirmar = true;
        }
        else
        {
            Mensaje = "El enlace no es valido o ha expirado.";
        }
    }

    public IActionResult OnPost()
    {
        ConfigurarSeguridad();

        if (!TokenValido(Token))
        {
            Mensaje = "El enlace no es valido o ha expirado.";
            return Page();
        }

        Confirmado = _verificacion.ConfirmarEmail(Token);

        Mensaje = Confirmado
            ? "Correo verificado correctamente. " +
              "Tu solicitud esta pendiente de aprobacion."
            : "El enlace no es valido, ha expirado " +
              "o ya fue utilizado.";

        Token = string.Empty;
        return Page();
    }

    private static bool TokenValido(string? token)
    {
        return !string.IsNullOrWhiteSpace(token) &&
               token.Length == 64 &&
               token.All(Uri.IsHexDigit);
    }

    private void ConfigurarSeguridad()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}