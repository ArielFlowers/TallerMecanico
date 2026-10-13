using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.Common;
using TallerMecanico.Application;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Models;

namespace TallerMecanico.Pages.Ordenes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(ConsultaOrdenesFacade consultas, ILogger<IndexModel> logger) : PageModel
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

    public IActionResult OnGetDetalle(int id)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            return BadRequest("Selecciona una orden válida.");
        }

        try
        {
            var detalle = consultas.ObtenerDetalle(id);
            return detalle is null
                ? NotFound("La orden seleccionada no existe.")
                : Partial("_DetalleOrden", detalle);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (DbException error)
        {
            logger.LogError(error, "No se pudo consultar el detalle de la orden {OrdenId}.", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "No se pudo consultar la orden. Intenta nuevamente.");
        }
    }
}
