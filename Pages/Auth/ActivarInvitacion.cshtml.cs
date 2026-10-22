using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("EmailVerificationRateLimit")]
public sealed class ActivarInvitacionModel : PageModel
{
    private readonly UsuarioAdministracionService _usuarioAdministracion;

    public ActivarInvitacionModel(UsuarioAdministracionService usuarioAdministracion)
    {
        _usuarioAdministracion = usuarioAdministracion;
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

    public IActionResult OnPost()
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

        bool resultado = _usuarioAdministracion.ActivarUsuarioDesdeInvitacion(
            Input.Token,
            Input.Password,
            out string? errorMessage);

        if (!resultado)
        {
            Mensaje = errorMessage ?? "Error al activar la cuenta.";
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

    [Required(ErrorMessage = "Ingrese una contraseña.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña debe tener entre 12 y 128 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}