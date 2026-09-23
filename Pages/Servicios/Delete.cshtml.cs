using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Servicios;

public class DeleteModel : PageModel
{
    private readonly IServicioService _servicioService;

    public DeleteModel(IServicioService servicioService)
    {
        _servicioService = servicioService;
    }

    public Servicio Servicio { get; private set; } = new();

    [TempData]
    public string? MensajeError { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

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
        try
        {
            _servicioService.Eliminar(id);
            MensajeExito = "Servicio eliminado correctamente.";
        }
        catch (InvalidOperationException ex)
        {
            MensajeError = ex.Message;
        }

        return RedirectToPage("./Control");
    }
}
