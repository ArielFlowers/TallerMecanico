using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Api;
using TallerMecanico.Pages.Servicios;

namespace TallerMecanico.Pages.Historial;

public class IndexModel(ServiciosApi serviciosApi) : ServiciosPageModel
{
    public IReadOnlyList<HistorialCostoServicioDto> Historial { get; private set; } = [];
    public bool TieneRegistros => Historial.Count > 0;

    public Task<IActionResult> OnGetAsync() => AtenderAsync(async () =>
    {
        Historial = await serviciosApi.ObtenerHistorialAsync(HttpContext.RequestAborted);
        return Page();
    });
}
