using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Pages.Reportes.Mecanicos;

[Authorize(Policy = "SoloAdministrador")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel : PageModel
{
    private readonly IReporteMecanicosPort _reporte;
    private readonly ICurrentUser _usuarioActual;

    public IndexModel(
        IReporteMecanicosPort reporte,
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
        IgnorarFiltroOculto();
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
        IgnorarFiltroOculto();
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

    private void IgnorarFiltroOculto()
    {
        // La pantalla filtra por nombre y fechas; un enlace antiguo con ID no debe aplicar un filtro invisible.
        Filtro.MecanicoId = null;
    }

    private string UsuarioActualNombre =>
        string.IsNullOrWhiteSpace(_usuarioActual.Username)
            ? "sistema"
            : _usuarioActual.Username;

    private static string FechaArchivo() =>
        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
}
