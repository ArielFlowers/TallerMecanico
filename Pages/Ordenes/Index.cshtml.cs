using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Application;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Models;

namespace TallerMecanico.Pages.Ordenes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(ConsultaOrdenesFacade consultas) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public EstadoOrden? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Fecha { get; set; }

    public IReadOnlyList<OrdenResumen> Ordenes { get; private set; } = [];

    public IActionResult OnGet()
    {
        if (ModelState.IsValid)
        {
            Ordenes = consultas.Obtener(Estado, Fecha);
        }

        return Page();
    }
}
