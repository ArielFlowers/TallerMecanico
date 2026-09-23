using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Servicios;

public class ControlModel : PageModel
{
    private readonly IServicioService _servicioService;

    public ControlModel(IServicioService servicioService)
    {
        _servicioService = servicioService;
    }

    public List<Servicio> Servicios { get; private set; } = [];

    [TempData]
    public string? MensajeError { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

    public void OnGet()
    {
        Servicios = _servicioService.ObtenerTodos();
    }
}
