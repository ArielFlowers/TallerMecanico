using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.Pages.Servicios;

public class IndexModel : PageModel
{
    private readonly IServicioService _servicioService;

    public IndexModel(IServicioService servicioService)
    {
        _servicioService = servicioService;
    }

    public List<Servicio> Servicios { get; private set; } = [];

    public string Busqueda { get; private set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int? ServicioSeleccionadoId { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

    public async Task OnGetAsync(string? buscar)
    {
        Busqueda = buscar?.Trim() ?? string.Empty;

        if (ServicioSeleccionadoId.HasValue)
        {
            var servicio = _servicioService.ObtenerPorId(ServicioSeleccionadoId.Value);
            Servicios = servicio is null ? [] : [servicio];
            return;
        }

        Servicios = string.IsNullOrWhiteSpace(Busqueda)
            ? _servicioService.ObtenerTodos()
            : (await _servicioService.BuscarAsync(Busqueda, HttpContext.RequestAborted)).ToList();
    }

    public async Task<JsonResult> OnGetSugerenciasAsync(string? termino)
    {
        Response.Headers.CacheControl = "no-store";
        var sugerencias = await _servicioService.SugerirAsync(termino, HttpContext.RequestAborted);
        return new JsonResult(sugerencias);
    }
}
