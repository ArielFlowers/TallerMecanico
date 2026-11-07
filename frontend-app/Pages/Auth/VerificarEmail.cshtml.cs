using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("EmailVerificationRateLimit")]
public sealed class VerificarEmailModel : PageModel
{
    private readonly UsuariosApi _usuarios;

    public VerificarEmailModel(
        UsuariosApi usuarios)
    {
        _usuarios = usuarios;
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
            Mensaje = "El enlace no es válido o ha expirado.";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ConfigurarSeguridad();

        if (!TokenValido(Token))
        {
            Mensaje = "El enlace no es válido o ha expirado.";
            return Page();
        }

        try
        {
            var resultado = await _usuarios.VerificarEmailAsync(Token, HttpContext.RequestAborted);
            if (resultado.Codigo == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                Mensaje = "El servicio de usuarios no está disponible. Intenta nuevamente.";
                return Page();
            }
            Confirmado = resultado.Exito;
        }
        catch (Exception error) when (error is HttpRequestException ||
            error is TaskCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            Mensaje = "El servicio de usuarios no está disponible. Intenta nuevamente.";
            return Page();
        }

        Mensaje = Confirmado
            ? "Correo verificado correctamente. " +
              "Tu solicitud está pendiente de aprobación."
            : "El enlace no es válido, ha expirado " +
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
