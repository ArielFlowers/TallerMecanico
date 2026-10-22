using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Pages;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel : PageModel
{
    private readonly DashboardCacheService _dashboardCacheService;

    public DashboardViewModel Dashboard { get; private set; } = new();

    public ResultadoDashboard? Cache { get; private set; }

    [TempData]
    public string? MensajeCache { get; set; }

    public IndexModel(DashboardCacheService dashboardCacheService)
    {
        _dashboardCacheService = dashboardCacheService;
    }

    public void OnGet()
    {
        Cache = _dashboardCacheService.Obtener();
        Dashboard = Cache.Dashboard;
    }

    // H2: invalidación manual equivalente a redis-cli DEL.
    public IActionResult OnPostInvalidarCache()
    {
        if (!User.IsInRole("Administrador"))
        {
            return Forbid();
        }

        MensajeCache = _dashboardCacheService.Invalidar()
            ? "Caché invalidada. La siguiente consulta será MISS y se repoblará desde MySQL."
            : "La caché ya estaba vacía o Redis no está disponible.";

        return RedirectToPage();
    }
}
