using System.ComponentModel.DataAnnotations;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Aplicacion.Servicios.Reportes;

public sealed class ReporteMecanicosService : IReporteMecanicosPort
{
    private readonly IConsultaReportesPort _consulta;
    private readonly IGeneradorReporteMecanicosPdfPort _pdf;
    private readonly IGeneradorReporteMecanicosExcelPort _excel;

    public ReporteMecanicosService(
        IConsultaReportesPort consulta,
        IGeneradorReporteMecanicosPdfPort pdf,
        IGeneradorReporteMecanicosExcelPort excel)
    {
        _consulta = consulta;
        _pdf = pdf;
        _excel = excel;
    }

    public DocumentoReporteMecanicos ObtenerDatos(
        FiltroReporteMecanicos filtro,
        string generadoPor)
    {
        ValidarFiltro(filtro);
        return _consulta.ObtenerReporteMecanicos(filtro, generadoPor);
    }

    public byte[] GenerarPdf(
        FiltroReporteMecanicos filtro,
        string generadoPor)
    {
        var documento = ObtenerDatos(filtro, generadoPor);
        return _pdf.Generar(documento);
    }

    public byte[] GenerarExcel(
        FiltroReporteMecanicos filtro,
        string generadoPor)
    {
        var documento = ObtenerDatos(filtro, generadoPor);
        return _excel.Generar(documento);
    }

    private static void ValidarFiltro(FiltroReporteMecanicos filtro)
    {
        if (filtro is null)
        {
            throw new ArgumentNullException(nameof(filtro));
        }

        filtro.NombreMecanico = string.IsNullOrWhiteSpace(filtro.NombreMecanico)
            ? null
            : filtro.NombreMecanico.Trim();

        if (filtro.NombreMecanico?.Length > 300)
        {
            throw new ValidationException(
                "El nombre del mecánico no puede superar los 300 caracteres.");
        }

        if (filtro.FechaDesde.HasValue && filtro.FechaHasta.HasValue &&
            filtro.FechaDesde.Value > filtro.FechaHasta.Value)
        {
            throw new ValidationException(
                "La fecha desde no puede ser posterior a la fecha hasta.");
        }

        if (filtro.MecanicoId.HasValue && filtro.MecanicoId.Value <= 0)
        {
            throw new ValidationException(
                "El identificador del mecánico debe ser mayor que cero.");
        }
    }
}
