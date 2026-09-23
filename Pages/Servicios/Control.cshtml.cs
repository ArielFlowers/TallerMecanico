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

    public void OnGet()
    {
        Servicios = _servicioService.ObtenerTodos();
    }
}