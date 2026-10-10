using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;

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

    [TempData]
    public string? MensajeError { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

    public void OnGet(string? buscar)
    {
        Busqueda = buscar?.Trim() ?? string.Empty;

        List<Servicio> servicios = _servicioService.ObtenerTodos();

        if (string.IsNullOrWhiteSpace(Busqueda))
        {
            Servicios = servicios;
            return;
        }

        Servicios = servicios
            .Where(servicio =>
                (servicio.Nombre?.Contains(
                    Busqueda,
                    StringComparison.OrdinalIgnoreCase) ?? false)
                ||
                (servicio.Descripcion?.Contains(
                    Busqueda,
                    StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }
}