using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("EmailVerificationRateLimit")]
public sealed class ActivarInvitacionModel : PageModel
{
    private readonly UsuariosApi _usuarios;

    public ActivarInvitacionModel(UsuariosApi usuarios)
    {
        _usuarios = usuarios;
    }

    [BindProperty]
    public ActivarInvitacionInput Input { get; set; } = new();

    public string? Token { get; set; }

    public bool TokenValido { get; private set; }

    public string? Mensaje { get; private set; }

    public bool Activado { get; private set; }

    public void OnGet(string? token)
    {
        ConfigurarSeguridad();

        if (EsTokenValido(token))
        {
            Token = token!;
            TokenValido = true;
            Input.Token = token!;
        }
        else
        {
            Mensaje = "El enlace de invitación no es válido o ha expirado.";
            TokenValido = false;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ConfigurarSeguridad();

        if (!EsTokenValido(Input.Token))
        {
            Mensaje = "El enlace de invitación no es válido o ha expirado.";
            TokenValido = false;
            return Page();
        }

        if (!ModelState.IsValid)
        {
            TokenValido = true;
            return Page();
        }

        try
        {
            var resultado = await _usuarios.ActivarInvitacionAsync(Input.Token, HttpContext.RequestAborted);
            if (!resultado.Exito)
            {
                Mensaje = resultado.Mensaje ?? "Error al activar la cuenta.";
                TokenValido = false;
                return Page();
            }
        }
        catch (Exception error) when (error is HttpRequestException ||
            error is TaskCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            Mensaje = "El servicio de usuarios no está disponible. Intenta nuevamente.";
            TokenValido = false;
            return Page();
        }

        Activado = true;
        Mensaje = "Su cuenta ha sido activada correctamente. Ya puede iniciar sesión.";
        return Page();
    }

    private static bool EsTokenValido(string? token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && token.Length == 64
            && token.All(Uri.IsHexDigit);
    }

    private void ConfigurarSeguridad()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}

public sealed class ActivarInvitacionInput
{
    public string Token { get; set; } = string.Empty;
}
