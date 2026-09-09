using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Historial;

public class IndexModel : PageModel
{
    private readonly IHistorialCostoServicioService _historialService;

    public IndexModel(
        IHistorialCostoServicioService historialService)
    {
        _historialService = historialService;
    }

    public IReadOnlyList<HistorialCostoServicio> Historial
        { get; private set; } = [];

    public void OnGet()
    {
        Historial = _historialService.ObtenerHistorial();
    }
}