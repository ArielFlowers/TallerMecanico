using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;

namespace TallerMecanico.Pages;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(OrdenesApi ordenes, ILogger<IndexModel> logger) : PageModel
{
    public DashboardViewModel Dashboard { get; private set; } = new();

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            Dashboard = await ordenes.ObtenerDashboardAsync(HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (HttpRequestException error)
        {
            logger.LogError(error, "No se pudo consultar el dashboard.");
            Error = "No se pudo consultar el resumen del taller. Intenta nuevamente.";
        }

        return Page();
    }
}
