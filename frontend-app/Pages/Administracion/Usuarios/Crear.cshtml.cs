using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Contratos.Usuarios;

namespace TallerMecanico.Pages.Administracion.Usuarios;

[Authorize(Policy = "SoloAdministrador")]
public sealed class CrearModel(UsuariosApi usuarios) : PageModel
{
    [BindProperty]
    public UsuarioCreateInput Input { get; set; } = new();

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            var resultado = await usuarios.CrearInvitacionAsync(
                new CrearInvitacionSolicitud(Input.Email, Input.Rol, Input.Password), cancellationToken);
            if (!resultado.Exito)
            {
                string campo = resultado.Campo == nameof(Input.Email) ? "Input.Email" : string.Empty;
                ModelState.AddModelError(campo, resultado.Mensaje ?? "No se pudo crear la invitación.");
                return Page();
            }
            TempData["MensajeExito"] = $"Se ha enviado una invitación a {Input.Email} para verificar su correo y activar su cuenta.";
            return RedirectToPage("./Index");
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized) { return Challenge(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "La operación fue cancelada.");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "El servicio de usuarios no está disponible. Intenta nuevamente.");
        }
        return Page();
    }
}
