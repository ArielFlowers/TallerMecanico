using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Servicios.Reportes;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;
[Trait("Area", "Backend")]
public class ReporteOrdenesServiceTests
{
    [Fact]
    public void ObtenerDatos_DevuelveResultadoDeConsulta()
    {
        var consulta = new ConsultaReportesFalsa();
        var pdf = new PdfOrdenesFalso();
        var excel = new ExcelOrdenesFalso();
        var servicio = new ReporteOrdenesService(consulta, pdf, excel);

        var filtro = new FiltroReporteOrdenes();
        var resultado = servicio.ObtenerDatos(filtro, "admin");

        Assert.Same(consulta.DevueltoOrdenes, resultado);
        Assert.Equal("admin", consulta.UltimoGeneradoPor);
        Assert.Equal(2, resultado!.Filas.Count);
        Assert.Equal(470.50m, resultado.Resumen.TotalGeneral);
    }

    [Fact]
    public void GenerarPdf_LlamaPuertoPdf()
    {
        var consulta = new ConsultaReportesFalsa();
        var pdf = new PdfOrdenesFalso();
        var excel = new ExcelOrdenesFalso();
        var servicio = new ReporteOrdenesService(consulta, pdf, excel);

        byte[] bytes = servicio.GenerarPdf(new FiltroReporteOrdenes(), "admin");

        Assert.Equal("PDF:2", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Same(consulta.DevueltoOrdenes, pdf.UltimoDocumento);
    }

    [Fact]
    public void GenerarExcel_LlamaPuertoExcel()
    {
        var consulta = new ConsultaReportesFalsa();
        var pdf = new PdfOrdenesFalso();
        var excel = new ExcelOrdenesFalso();
        var servicio = new ReporteOrdenesService(consulta, pdf, excel);

        byte[] bytes = servicio.GenerarExcel(new FiltroReporteOrdenes(), "admin");

        Assert.Equal("XLSX:2", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Same(consulta.DevueltoOrdenes, excel.UltimoDocumento);
    }

    [Theory]
    [InlineData("2026-02-01", "2026-01-01")]
    public void ValidarFiltro_RechazaFechaDesdePosteriorAFechaHasta(string desde, string hasta)
    {
        var consulta = new ConsultaReportesFalsa();
        var servicio = new ReporteOrdenesService(consulta, new PdfOrdenesFalso(), new ExcelOrdenesFalso());

        var ex = Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(
            () => servicio.ObtenerDatos(
                new FiltroReporteOrdenes
                {
                    FechaDesde = DateOnly.Parse(desde),
                    FechaHasta = DateOnly.Parse(hasta)
                },
                "admin"));

        Assert.Contains("fecha desde", ex.Message);
    }

    [Theory]
    [InlineData("Pendiente")]
    [InlineData("Finalizada")]
    public void ValidarFiltro_RechazaEstadoInvalido(string estado)
    {
        var consulta = new ConsultaReportesFalsa();
        var servicio = new ReporteOrdenesService(consulta, new PdfOrdenesFalso(), new ExcelOrdenesFalso());

        var ex = Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(
            () => servicio.ObtenerDatos(
                new FiltroReporteOrdenes { Estado = estado },
                "admin"));

        Assert.Contains("estado", ex.Message);
    }

    [Theory]
    [InlineData("Activa")]
    [InlineData("Anulada")]
    [InlineData(null)]
    public void ValidarFiltro_AceptaEstadosValidos(string? estado)
    {
        var consulta = new ConsultaReportesFalsa();
        var servicio = new ReporteOrdenesService(consulta, new PdfOrdenesFalso(), new ExcelOrdenesFalso());

        var resultado = servicio.ObtenerDatos(
            new FiltroReporteOrdenes { Estado = estado },
            "admin");

        Assert.NotNull(resultado);
    }
}
