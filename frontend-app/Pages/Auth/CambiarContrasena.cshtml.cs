using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Contratos.Usuarios;

namespace TallerMecanico.Pages.Auth;

[Authorize]
public sealed class CambiarContrasenaModel(
    UsuariosApi usuarios) : PageModel
{
    [BindProperty]
    public Entrada Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var resultado = await usuarios.CambiarContrasenaAsync(
                new CambiarContrasenaSolicitud(Input.Actual, Input.Nueva, Input.Confirmacion),
                HttpContext.RequestAborted);
            if (!resultado.Exito)
            {
                ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo cambiar la contraseña.");
                return Page();
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception error) when (error is HttpRequestException ||
            error is TaskCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "El servicio de usuarios no está disponible. Intenta nuevamente.");
            return Page();
        }

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        HttpContext.Session.Clear();

        TempData["MensajeExito"] =
            "Contraseña actualizada correctamente. Su sesión ha finalizado. Inicie sesión nuevamente.";

        return RedirectToPage("/Auth/Login");
    }

    public sealed class Entrada
    {
        [Required(ErrorMessage = "Ingrese su contraseña actual.")]
        public string Actual { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese la nueva contraseña.")]
        [StringLength(128, MinimumLength = 8,
        ErrorMessage = "La nueva contraseña debe tener entre 8 y 128 caracteres.")]
        public string Nueva { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme la nueva contraseña.")]
        public string Confirmacion { get; set; } = string.Empty;
    }
}