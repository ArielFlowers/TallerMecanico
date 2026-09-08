using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Servicios;

public class DeleteModel : PageModel
{
    private readonly ServicioService _servicioService;

    public DeleteModel(ServicioService servicioService)
    {
        _servicioService = servicioService;
    }

    public Servicio Servicio { get; private set; } = new();

    public IActionResult OnGet(int id)
    {
        Servicio? servicio = _servicioService.ObtenerPorId(id);

        if (servicio is null)
        {
            return NotFound();
        }

        Servicio = servicio;

        return Page();
    }

    public IActionResult OnPost(int id)
    {
        _servicioService.Eliminar(id);

        return RedirectToPage("./Index");
    }
}
