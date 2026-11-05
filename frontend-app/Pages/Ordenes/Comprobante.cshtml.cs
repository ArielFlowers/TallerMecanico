using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;
using TallerMecanico.Aplicacion.Fachadas;

namespace TallerMecanico.Pages.Ordenes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ComprobanteModel(ConsultaOrdenesFacade consultas, ILogger<ComprobanteModel> logger) : PageModel
{
    public IActionResult OnGet(int id)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest("Selecciona una orden válida.");

        try
        {
            byte[]? pdf = consultas.GenerarComprobante(id);
            if (pdf is null) return NotFound("La orden seleccionada no existe.");

            Response.GetTypedHeaders().ContentDisposition = new ContentDispositionHeaderValue("inline")
            {
                FileName = $"Comprobante-Orden-{id}.pdf"
            };
            return File(pdf, "application/pdf");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (DbException error)
        {
            logger.LogError(error, "No se pudo consultar la orden {OrdenId} para generar el comprobante.", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "No se pudo consultar la orden. Intenta nuevamente.");
        }
        catch (Exception error)
        {
            logger.LogError(error, "No se pudo generar el PDF de la orden {OrdenId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "No se pudo generar el comprobante. Intenta nuevamente.");
        }
    }
}
