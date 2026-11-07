using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Contratos.Usuarios;

namespace TallerMecanico.Pages.Administracion.Usuarios;

[Authorize(Policy = "SoloAdministrador")]
public sealed class EditarModel(UsuariosApi usuarios) : PageModel
{
    public UsuarioDto? Usuario { get; private set; }

    [BindProperty]
    public UsuarioEditInput Input { get; set; } = new();

    public Task<IActionResult> OnGetAsync(int id) => AtenderAsync(async () =>
    {
        Usuario = await usuarios.ObtenerPorIdAsync(id, HttpContext.RequestAborted);
        if (Usuario is null) return NotFound();
        Input = new UsuarioEditInput
        {
            Email = Usuario.Email ?? string.Empty,
            Rol = Usuario.Rol,
            Activo = Usuario.Activo
        };
        return Page();
    });

    public Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken = default) => AtenderAsync(async () =>
    {
        Usuario = await usuarios.ObtenerPorIdAsync(id, cancellationToken);
        if (Usuario is null) return NotFound();
        if (!ModelState.IsValid) return Page();
        var resultado = await usuarios.ActualizarAsync(id,
            new ActualizarUsuarioSolicitud(Input.Email, Input.Rol, Input.Activo), cancellationToken);
        if (!resultado.Exito)
        {
            if (resultado.Codigo == HttpStatusCode.NotFound) return NotFound();
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "Error al actualizar el usuario.");
            return Page();
        }
        TempData["MensajeExito"] = "Usuario actualizado correctamente.";
        return RedirectToPage("./Index");
    });

    private async Task<IActionResult> AtenderAsync(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized) { return Challenge(); }
        catch (Exception error) when (error is HttpRequestException ||
            error is TaskCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            if (Usuario is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "El servicio de usuarios no está disponible. Intenta nuevamente.");
            ModelState.AddModelError(string.Empty, "El servicio de usuarios no está disponible. Intenta nuevamente.");
            return Page();
        }
    }
}
