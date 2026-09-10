using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Servicios;

public class RegistrosModel : PageModel
{
    private readonly ServicioService _servicioService;

    public RegistrosModel(ServicioService servicioService)
    {
        _servicioService = servicioService;
    }

    public List<Servicio> Servicios { get; private set; } = [];

    public void OnGet()
    {
        Servicios = _servicioService.ObtenerTodos();
    }
}
