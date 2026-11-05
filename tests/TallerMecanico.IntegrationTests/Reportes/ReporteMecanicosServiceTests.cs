using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Servicios.Reportes;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;
[Trait("Area", "Backend")]
public class ReporteMecanicosServiceTests
{
    [Fact]
    public void ObtenerDatos_DevuelveResultadoDeConsulta()
    {
        var consulta = new ConsultaReportesFalsa();
        var pdf = new PdfMecanicosFalso();
        var excel = new ExcelMecanicosFalso();
        var servicio = new ReporteMecanicosService(consulta, pdf, excel);

        var filtro = new FiltroReporteMecanicos();
        var resultado = servicio.ObtenerDatos(filtro, "admin");

        Assert.Same(consulta.DevueltoMecanicos, resultado);
        Assert.Equal("admin", consulta.UltimoGeneradoPor);
        Assert.Equal(2, resultado!.Filas.Count);
        Assert.Equal(920.50m, resultado.Resumen.TotalFacturado);
    }

    [Fact]
    public void GenerarPdf_LlamaPuertoPdf()
    {
        var consulta = new ConsultaReportesFalsa();
        var pdf = new PdfMecanicosFalso();
        var excel = new ExcelMecanicosFalso();
        var servicio = new ReporteMecanicosService(consulta, pdf, excel);

        byte[] bytes = servicio.GenerarPdf(new FiltroReporteMecanicos(), "admin");

        Assert.Equal("PDF:2", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Same(consulta.DevueltoMecanicos, pdf.UltimoDocumento);
    }

    [Fact]
    public void GenerarExcel_LlamaPuertoExcel()
    {
        var consulta = new ConsultaReportesFalsa();
        var pdf = new PdfMecanicosFalso();
        var excel = new ExcelMecanicosFalso();
        var servicio = new ReporteMecanicosService(consulta, pdf, excel);

        byte[] bytes = servicio.GenerarExcel(new FiltroReporteMecanicos(), "admin");

        Assert.Equal("XLSX:2", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Same(consulta.DevueltoMecanicos, excel.UltimoDocumento);
    }

    [Theory]
    [InlineData("2026-02-01", "2026-01-01")]
    public void ValidarFiltro_RechazaFechaDesdePosteriorAFechaHasta(string desde, string hasta)
    {
        var consulta = new ConsultaReportesFalsa();
        var servicio = new ReporteMecanicosService(consulta, new PdfMecanicosFalso(), new ExcelMecanicosFalso());

        var ex = Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(
            () => servicio.ObtenerDatos(
                new FiltroReporteMecanicos
                {
                    FechaDesde = DateOnly.Parse(desde),
                    FechaHasta = DateOnly.Parse(hasta)
                },
                "admin"));

        Assert.Contains("fecha desde", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidarFiltro_RechazaMecanicoIdInvalido(int mecanicoId)
    {
        var consulta = new ConsultaReportesFalsa();
        var servicio = new ReporteMecanicosService(consulta, new PdfMecanicosFalso(), new ExcelMecanicosFalso());

        var ex = Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(
            () => servicio.ObtenerDatos(
                new FiltroReporteMecanicos { MecanicoId = mecanicoId },
                "admin"));

        Assert.Contains("mecánico", ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(null)]
    public void ValidarFiltro_AceptaMecanicoIdValido(int? mecanicoId)
    {
        var consulta = new ConsultaReportesFalsa();
        var servicio = new ReporteMecanicosService(consulta, new PdfMecanicosFalso(), new ExcelMecanicosFalso());

        var resultado = servicio.ObtenerDatos(
            new FiltroReporteMecanicos { MecanicoId = mecanicoId },
            "admin");

        Assert.NotNull(resultado);
    }
}
