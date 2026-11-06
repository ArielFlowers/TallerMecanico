using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Contratos.Ordenes;

namespace TallerMecanico.Pages.Ordenes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(OrdenesApi ordenes, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public EstadoOrden? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Fecha { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? OrdenSeleccionadaId { get; set; }

    public IReadOnlyList<OrdenResumenDto> Ordenes { get; private set; } = [];

    public string? MensajeExito { get; private set; }

    public int? OrdenCreadaId { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (ModelState.IsValid)
        {
            try
            {
                await CargarOrdenesAsync();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (HttpRequestException error)
            {
                AgregarErrorServicio(error);
            }
        }

        MensajeExito = TempData?.ContainsKey("OrdenMensaje") == true
            ? TempData["OrdenMensaje"] as string
            : null;
        if (TempData?.TryGetValue("OrdenCreadaId", out object? creada) == true && creada is int creadaId)
            OrdenCreadaId = creadaId;
        return Page();
    }

    public async Task<IActionResult> OnGetSugerenciasAsync(string? termino)
    {
        if (!ModelState.IsValid) return BadRequest("Revisa los filtros de estado y fecha.");

        var sugerencias = await ordenes.SugerirAsync(termino, Estado, Fecha, HttpContext.RequestAborted);
        return new JsonResult(sugerencias);
    }

    public async Task<IActionResult> OnGetDetalleAsync(int id)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            return BadRequest("Selecciona una orden válida.");
        }

        try
        {
            var detalle = await ordenes.ObtenerPorIdAsync(id);
            return detalle is null
                ? NotFound("La orden seleccionada no existe.")
                : Partial("_DetalleOrden", detalle);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (HttpRequestException error)
        {
            logger.LogError(error, "No se pudo consultar el detalle de la orden {OrdenId}.", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "No se pudo consultar la orden. Intenta nuevamente.");
        }
    }

    public async Task<IActionResult> OnPostAnularAsync(int id)
    {
        if (id <= 0)
        {
            ModelState.AddModelError(string.Empty, "Selecciona una orden válida.");
            Ordenes = [];
            return Page();
        }

        try
        {
            ResultadoOrdenDto resultado = await ordenes.AnularAsync(id);
            if (!resultado.Exito)
            {
                foreach (var error in resultado.Errores)
                    ModelState.AddModelError(string.Empty, error.Value);
                await CargarOrdenesAsync();
                return Page();
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (HttpRequestException error)
        {
            AgregarErrorServicio(error);
            await CargarOrdenesSeguroAsync();
            return Page();
        }

        TempData["OrdenMensaje"] = $"Orden #{id} anulada correctamente. Se restituyó el stock.";
        return RedirectToPage(new { Estado, Fecha = Fecha?.ToString("yyyy-MM-dd"), TerminoBusqueda, OrdenSeleccionadaId });
    }

    private async Task CargarOrdenesAsync()
    {
        TerminoBusqueda = TerminoBusqueda?.Trim();
        Ordenes = await ordenes.ObtenerAsync(Estado, Fecha, TerminoBusqueda, OrdenSeleccionadaId);
    }

    private async Task CargarOrdenesSeguroAsync()
    {
        try
        {
            await CargarOrdenesAsync();
        }
        catch (HttpRequestException error)
        {
            AgregarErrorServicio(error);
        }
    }

    private void AgregarErrorServicio(HttpRequestException error)
    {
        logger.LogError(error, "No se pudo consultar el listado de órdenes.");
        ModelState.AddModelError(string.Empty, "No se pudo consultar las órdenes. Intenta nuevamente.");
    }
}
