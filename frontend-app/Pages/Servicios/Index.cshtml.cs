using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Servicios;

public class IndexModel(ServiciosApi serviciosApi) : ServiciosPageModel
{
    public IReadOnlyList<ServicioDto> Servicios { get; private set; } = [];
    public string Busqueda { get; private set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int? ServicioSeleccionadoId { get; set; }

    [TempData] public string? MensajeError { get; set; }
    [TempData] public string? MensajeExito { get; set; }

    public Task<IActionResult> OnGetAsync(string? buscar) => AtenderAsync(async () =>
    {
        Busqueda = buscar?.Trim() ?? string.Empty;
        if (ServicioSeleccionadoId.HasValue)
        {
            var servicio = await serviciosApi.ObtenerPorIdAsync(ServicioSeleccionadoId.Value, HttpContext.RequestAborted);
            Servicios = servicio is null ? [] : [servicio];
        }
        else
            Servicios = await serviciosApi.ObtenerAsync(string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda, HttpContext.RequestAborted);
        return Page();
    });

    public Task<IActionResult> OnGetSugerenciasAsync(string? termino) => AtenderAsync(async () =>
    {
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(await serviciosApi.SugerirAsync(termino, HttpContext.RequestAborted));
    });
}
