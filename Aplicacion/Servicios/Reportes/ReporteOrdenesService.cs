using System.ComponentModel.DataAnnotations;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Aplicacion.Servicios.Reportes;

public sealed class ReporteOrdenesService
{
    private static readonly string[] EstadosValidos = { "Activa", "Anulada" };

    private readonly IConsultaReportesPort _consulta;
    private readonly IGeneradorReporteOrdenesPdfPort _pdf;
    private readonly IGeneradorReporteOrdenesExcelPort _excel;

    public ReporteOrdenesService(
        IConsultaReportesPort consulta,
        IGeneradorReporteOrdenesPdfPort pdf,
        IGeneradorReporteOrdenesExcelPort excel)
    {
        _consulta = consulta;
        _pdf = pdf;
        _excel = excel;
    }

    public DocumentoReporteOrdenes ObtenerDatos(
        FiltroReporteOrdenes filtro,
        string generadoPor)
    {
        ValidarFiltro(filtro);
        return _consulta.ObtenerReporteOrdenes(filtro, generadoPor);
    }

    public byte[] GenerarPdf(
        FiltroReporteOrdenes filtro,
        string generadoPor)
    {
        var documento = ObtenerDatos(filtro, generadoPor);
        return _pdf.Generar(documento);
    }

    public byte[] GenerarExcel(
        FiltroReporteOrdenes filtro,
        string generadoPor)
    {
        var documento = ObtenerDatos(filtro, generadoPor);
        return _excel.Generar(documento);
    }

    private static void ValidarFiltro(FiltroReporteOrdenes filtro)
    {
        if (filtro is null)
        {
            throw new ArgumentNullException(nameof(filtro));
        }

        if (filtro.FechaDesde.HasValue && filtro.FechaHasta.HasValue &&
            filtro.FechaDesde.Value > filtro.FechaHasta.Value)
        {
            throw new ValidationException(
                "La fecha desde no puede ser posterior a la fecha hasta.");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Estado) &&
            !EstadosValidos.Contains(filtro.Estado!))
        {
            throw new ValidationException(
                "El estado de la orden debe ser Activa o Anulada.");
        }
    }
}
