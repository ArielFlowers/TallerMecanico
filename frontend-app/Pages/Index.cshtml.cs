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

        MensajeCache = _dashboardCacheService.Invalidar() switch
        {
            ResultadoEliminacionCache.Eliminada =>
                "Redis confirmó la invalidación del resumen. Al seguir la redirección, el dashboard puede regenerar la caché desde MySQL.",
            ResultadoEliminacionCache.Inexistente =>
                "Redis confirmó que el resumen ya no estaba en caché. Al seguir la redirección, el dashboard puede regenerarlo desde MySQL.",
            ResultadoEliminacionCache.NoDisponible =>
                "Redis no está disponible. No se confirmó la invalidación; el resumen anterior puede persistir hasta su expiración.",
            ResultadoEliminacionCache.Deshabilitado =>
                "La caché está deshabilitada. No se ejecutó una invalidación en Redis.",
            _ =>
                "Falló la invalidación del resumen. No se confirmó su eliminación; puede persistir hasta su expiración."
        };

        return RedirectToPage();
    }
}
