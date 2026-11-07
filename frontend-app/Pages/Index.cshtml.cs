using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;

namespace TallerMecanico.Pages;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel : PageModel
{
    private readonly OrdenesApi _ordenes;

    public DashboardViewModel Dashboard { get; private set; } = new();

    public IndexModel(OrdenesApi ordenes)
    {
        _ordenes = ordenes;
    }

    public async Task OnGetAsync()
    {
        Dashboard = await _ordenes.ObtenerDashboardAsync(HttpContext.RequestAborted);
    }
}
