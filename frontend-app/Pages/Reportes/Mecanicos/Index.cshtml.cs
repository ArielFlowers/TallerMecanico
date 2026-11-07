using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Reportes.Mecanicos;

[Authorize(Policy = "SoloAdministrador")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(ReportesApi reportes) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public FiltroReporteMecanicos Filtro { get; set; } = new();

    public DocumentoReporteMecanicos? Documento { get; private set; }

    public string? MensajeError { get; private set; }

    public Task<IActionResult> OnGetAsync() => AtenderAsync(async () =>
    {
        await CargarDatosAsync(HttpContext.RequestAborted);
        return Page();
    });

    public Task<IActionResult> OnGetResultadosAsync() => AtenderAsync(async () =>
    {
        await CargarDatosAsync(HttpContext.RequestAborted);
        return Partial("_Resultados", this);
    });

    public Task<IActionResult> OnPostExportarPdfAsync() => AtenderAsync(() =>
        ExportarAsync(() => reportes.ExportarMecanicosPdfAsync(Filtro, HttpContext.RequestAborted),
            "application/pdf", $"ReporteMecanicos_{FechaArchivo()}.pdf"));

    public Task<IActionResult> OnPostExportarExcelAsync() => AtenderAsync(() =>
        ExportarAsync(() => reportes.ExportarMecanicosExcelAsync(Filtro, HttpContext.RequestAborted),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ReporteMecanicos_{FechaArchivo()}.xlsx"));

    private async Task CargarDatosAsync(CancellationToken cancelacion)
    {
        IgnorarFiltroOculto();
        try
        {
            Documento = await reportes.ObtenerMecanicosAsync(Filtro, cancelacion);
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
        IgnorarFiltroOculto();
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

    private void IgnorarFiltroOculto()
    {
        // La pantalla filtra por nombre y fechas; un enlace antiguo con ID no debe aplicar un filtro invisible.
        Filtro.MecanicoId = null;
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
