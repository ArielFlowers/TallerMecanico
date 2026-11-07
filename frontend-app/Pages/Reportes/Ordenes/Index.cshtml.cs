using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Reportes.Ordenes;

[Authorize(Policy = "SoloAdministrador")]
public sealed class IndexModel(ReportesApi reportes) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public FiltroReporteOrdenes Filtro { get; set; } = new();

    public DocumentoReporteOrdenes? Documento { get; private set; }

    public string? MensajeError { get; private set; }

    public Task<IActionResult> OnGetAsync() => AtenderAsync(async () =>
    {
        await CargarDatosAsync(HttpContext.RequestAborted);
        return Page();
    });

    public Task<IActionResult> OnPostExportarPdfAsync() => AtenderAsync(() =>
        ExportarAsync(() => reportes.ExportarOrdenesPdfAsync(Filtro, HttpContext.RequestAborted),
            "application/pdf", $"ReporteOrdenes_{FechaArchivo()}.pdf"));

    public Task<IActionResult> OnPostExportarExcelAsync() => AtenderAsync(() =>
        ExportarAsync(() => reportes.ExportarOrdenesExcelAsync(Filtro, HttpContext.RequestAborted),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ReporteOrdenes_{FechaArchivo()}.xlsx"));

    private async Task CargarDatosAsync(CancellationToken cancelacion)
    {
        try
        {
            Documento = await reportes.ObtenerOrdenesAsync(Filtro, cancelacion);
            MensajeError = null;
        }
        catch (ValidationException ex)
        {
            MensajeError = ex.Message;
            Documento = null;
        }
    }

    private async Task<IActionResult> ExportarAsync(
        Func<Task<byte[]>> generador,
        string tipoContenido,
        string nombreArchivo)
    {
        if (!ModelState.IsValid)
        {
            await CargarDatosAsync(HttpContext.RequestAborted);
            return Page();
        }

        try
        {
            byte[] contenido = await generador();
            return File(contenido, tipoContenido, nombreArchivo);
        }
        catch (ValidationException ex)
        {
            MensajeError = ex.Message;
            await CargarDatosAsync(HttpContext.RequestAborted);
            return Page();
        }
    }

    private async Task<IActionResult> AtenderAsync(Func<Task<IActionResult>> accion)
    {
        try
        {
            return await accion();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "El servicio de reportes no está disponible. Intenta nuevamente.");
        }
        catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "El servicio de reportes no está disponible. Intenta nuevamente.");
        }
    }

    private static string FechaArchivo() =>
        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
}
