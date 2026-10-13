using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.Common;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Pages.Ordenes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(ConsultaOrdenesFacade consultas, OrdenServicioFacade ordenes, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public EstadoOrden? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Fecha { get; set; }

    public IReadOnlyList<OrdenResumen> Ordenes { get; private set; } = [];

    public string? MensajeExito { get; private set; }

    public IActionResult OnGet()
    {
        if (ModelState.IsValid)
        {
            Ordenes = consultas.Obtener(Estado, Fecha);
        }

        MensajeExito = TempData?.ContainsKey("OrdenMensaje") == true
            ? TempData["OrdenMensaje"] as string
            : null;
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

    public IActionResult OnPostAnular(int id)
    {
        if (id <= 0)
        {
            ModelState.AddModelError(string.Empty, "Selecciona una orden válida.");
            Ordenes = ModelState.IsValid ? consultas.Obtener(Estado, Fecha) : [];
            return Page();
        }

        ResultadoOrden resultado = ordenes.AnularOrden(id);
        if (!resultado.Exito)
        {
            foreach (var error in resultado.Errores)
                ModelState.AddModelError(string.Empty, error.Value);
            Ordenes = consultas.Obtener(Estado, Fecha);
            return Page();
        }

        TempData["OrdenMensaje"] = $"Orden #{id} anulada correctamente. Se restituyó el stock.";
        return RedirectToPage();
    }
}
