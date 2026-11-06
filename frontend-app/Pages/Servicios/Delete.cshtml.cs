using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Servicios;

public class DeleteModel(ServiciosApi serviciosApi) : ServiciosPageModel
{
    public ServicioDto Servicio { get; private set; } = new();
    [TempData] public string? MensajeError { get; set; }
    [TempData] public string? MensajeExito { get; set; }

    public Task<IActionResult> OnGetAsync(int id) => AtenderAsync(async () =>
    {
        var servicio = await serviciosApi.ObtenerPorIdAsync(id, HttpContext.RequestAborted);
        if (servicio is null) return NotFound();
        Servicio = servicio;
        return Page();
    });

    public Task<IActionResult> OnPostAsync(int id) => AtenderAsync(async () =>
    {
        var resultado = await serviciosApi.EliminarAsync(id, HttpContext.RequestAborted);
        if (resultado.Exito) MensajeExito = "Servicio eliminado correctamente.";
        else MensajeError = resultado.Mensaje;
        return RedirectToPage("./Index");
    });
}
