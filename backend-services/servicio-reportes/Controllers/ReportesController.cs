using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Contratos.Reportes;
using TallerMecanico.Contratos;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize(Roles = Roles.Administrador)]
public sealed class ReportesController(
    IReporteOrdenesPort ordenes,
    IReporteMecanicosPort mecanicos,
    ICurrentUser usuarioActual) : ControllerBase
{
    private const string TipoPdf = "application/pdf";
    private const string TipoExcel =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("ordenes")]
    public DocumentoReporteOrdenes ObtenerOrdenes([FromQuery] FiltroReporteOrdenes filtro) =>
        ordenes.ObtenerDatos(filtro, GeneradoPor);

    [HttpGet("ordenes/pdf")]
    public FileContentResult ExportarOrdenesPdf([FromQuery] FiltroReporteOrdenes filtro) =>
        File(ordenes.GenerarPdf(filtro, GeneradoPor), TipoPdf,
            $"ReporteOrdenes_{FechaArchivo()}.pdf");

    [HttpGet("ordenes/excel")]
    public FileContentResult ExportarOrdenesExcel([FromQuery] FiltroReporteOrdenes filtro) =>
        File(ordenes.GenerarExcel(filtro, GeneradoPor), TipoExcel,
            $"ReporteOrdenes_{FechaArchivo()}.xlsx");

    [HttpGet("mecanicos")]
    public DocumentoReporteMecanicos ObtenerMecanicos([FromQuery] FiltroReporteMecanicos filtro) =>
        mecanicos.ObtenerDatos(filtro, GeneradoPor);

    [HttpGet("mecanicos/pdf")]
    public FileContentResult ExportarMecanicosPdf([FromQuery] FiltroReporteMecanicos filtro) =>
        File(mecanicos.GenerarPdf(filtro, GeneradoPor), TipoPdf,
            $"ReporteMecanicos_{FechaArchivo()}.pdf");

    [HttpGet("mecanicos/excel")]
    public FileContentResult ExportarMecanicosExcel([FromQuery] FiltroReporteMecanicos filtro) =>
        File(mecanicos.GenerarExcel(filtro, GeneradoPor), TipoExcel,
            $"ReporteMecanicos_{FechaArchivo()}.xlsx");

    private string GeneradoPor =>
        string.IsNullOrWhiteSpace(usuarioActual.Username)
            ? "sistema"
            : usuarioActual.Username;

    private static string FechaArchivo() =>
        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
}
