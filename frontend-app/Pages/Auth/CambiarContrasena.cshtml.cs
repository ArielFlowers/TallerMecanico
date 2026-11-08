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

    // La misma política que aplica el Servicio 2, para avisar antes de enviar.
    public sealed class Entrada : IValidatableObject
    {
        [Required(ErrorMessage = "Ingrese su contraseña actual.")]
        public string Actual { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese la nueva contraseña.")]
        public string Nueva { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme la nueva contraseña.")]
        [Compare(nameof(Nueva), ErrorMessage = "Las contraseñas nuevas no coinciden.")]
        public string Confirmacion { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(Nueva) && !ReglasCuenta.ContrasenaValida(Nueva))
                yield return new ValidationResult(ReglasCuenta.MensajeContrasena, [nameof(Nueva)]);
        }
    }
}