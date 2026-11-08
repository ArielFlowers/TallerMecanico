using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TallerMecanico.Pages.Servicios;

public abstract class ServiciosPageModel : PageModel
{
    protected async Task<IActionResult> AtenderAsync(Func<Task<IActionResult>> accion)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        if (!User.IsInRole(Roles.Administrador)) return Forbid();

        try { return await accion(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized) { return Challenge(); }
        catch (HttpRequestException)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El módulo de servicios no está disponible. Intenta nuevamente."); }
        catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El módulo de servicios no está disponible. Intenta nuevamente."); }
    }
}
