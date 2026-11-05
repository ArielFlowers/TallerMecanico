using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios.Reportes;

namespace TallerMecanico.Pages.Reportes.Mecanicos;

[Authorize(Policy = "SoloAdministrador")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel : PageModel
{
    private readonly ReporteMecanicosService _reporte;
    private readonly ICurrentUser _usuarioActual;

    public IndexModel(
        ReporteMecanicosService reporte,
        ICurrentUser usuarioActual)
    {
        _reporte = reporte;
        _usuarioActual = usuarioActual;
    }

    [BindProperty(SupportsGet = true)]
    public FiltroReporteMecanicos Filtro { get; set; } = new();

    public DocumentoReporteMecanicos? Documento { get; private set; }

    public string? MensajeError { get; private set; }

    public void OnGet()
    {
        CargarDatos();
    }

    public IActionResult OnGetResultados()
    {
        CargarDatos();
        return Partial("_Resultados", this);
    }

    public IActionResult OnPostExportarPdf()
    {
        return Exportar(() => _reporte.GenerarPdf(Filtro, UsuarioActualNombre),
            "application/pdf", $"ReporteMecanicos_{FechaArchivo()}.pdf");
    }

    public IActionResult OnPostExportarExcel()
    {
        return Exportar(() => _reporte.GenerarExcel(Filtro, UsuarioActualNombre),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ReporteMecanicos_{FechaArchivo()}.xlsx");
    }

    private void CargarDatos()
    {
        UsarSoloNombre();
        try
        {
            Documento = _reporte.ObtenerDatos(Filtro, UsuarioActualNombre);
            MensajeError = null;
        }
        catch (ValidationException ex)
        {
            MensajeError = ex.Message;
            Documento = null;
        }
    }

    private IActionResult Exportar(
        Func<byte[]> generador,
        string tipoContenido,
        string nombreArchivo)
    {
        UsarSoloNombre();
        if (!ModelState.IsValid)
        {
            CargarDatos();
            return Page();
        }

        try
        {
            byte[] contenido = generador();
            return File(contenido, tipoContenido, nombreArchivo);
        }
        catch (ValidationException ex)
        {
            MensajeError = ex.Message;
            CargarDatos();
            return Page();
        }
    }

    private void UsarSoloNombre()
    {
        // Los enlaces antiguos con fechas o ID no deben aplicar filtros invisibles.
        Filtro = new FiltroReporteMecanicos { NombreMecanico = Filtro.NombreMecanico };
    }

    private string UsuarioActualNombre =>
        string.IsNullOrWhiteSpace(_usuarioActual.Username)
            ? "sistema"
            : _usuarioActual.Username;

    private static string FechaArchivo() =>
        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
}
