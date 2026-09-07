using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Servicios;

public class CreateModel : PageModel
{
    private readonly ServicioService _servicioService;

    public CreateModel(ServicioService servicioService)
    {
        _servicioService = servicioService;
    }

    [BindProperty]
    public ServicioFormViewModel Formulario { get; set; } = new();

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Servicio servicio = new()
        {
            Nombre = Formulario.Nombre.Trim(),
            Descripcion = Formulario.Descripcion.Trim(),
            Costo = Formulario.Costo,
            TiempoEstimadoHoras = Formulario.TiempoEstimadoHoras
        };

        _servicioService.Crear(servicio);

        return RedirectToPage("./Index");
    }
}
