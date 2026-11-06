using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.Pages;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel : PageModel
{
    private readonly DashboardService _dashboardService;

    public DashboardViewModel Dashboard { get; private set; } = new();

    public IndexModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public void OnGet()
    {
        Dashboard = _dashboardService.GetDashboardData();
    }
}
