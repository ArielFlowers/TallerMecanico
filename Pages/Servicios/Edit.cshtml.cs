using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Servicios;

public class EditModel : PageModel
{
    private readonly ServicioService _servicioService;

    public EditModel(ServicioService servicioService)
    {
        _servicioService = servicioService;
    }

    [BindProperty]
    public ServicioFormViewModel Formulario { get; set; } = new();

    public int ServicioId { get; private set; }

    public IActionResult OnGet(int id)
    {
        Servicio? servicio = _servicioService.ObtenerPorId(id);

        if (servicio is null)
        {
            return NotFound();
        }

        ServicioId = servicio.Id;

        Formulario = new ServicioFormViewModel
        {
            Nombre = servicio.Nombre,
            Descripcion = servicio.Descripcion,
            Costo = servicio.Costo,
            TiempoEstimadoHoras = servicio.TiempoEstimadoHoras
        };

        return Page();
    }

    public IActionResult OnPost(int id)
    {
        ServicioId = id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Servicio servicio = new()
        {
            Id = id,
            Nombre = Formulario.Nombre.Trim(),
            Descripcion = Formulario.Descripcion.Trim(),
            Costo = Formulario.Costo,
            TiempoEstimadoHoras = Formulario.TiempoEstimadoHoras
        };

        _servicioService.Actualizar(servicio);

        return RedirectToPage("./Index");
    }
}
