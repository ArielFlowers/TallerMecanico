using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.Pages.Auth;

[Authorize]
public sealed class CambiarContrasenaModel(
    CambioContrasenaService servicio) : PageModel
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

        if (!servicio.Cambiar(
                Input.Actual,
                Input.Nueva,
                Input.Confirmacion,
                out string error))
        {
            ModelState.AddModelError(string.Empty, error);
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
        public string Nueva { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme la nueva contraseña.")]
        public string Confirmacion { get; set; } = string.Empty;
    }
}