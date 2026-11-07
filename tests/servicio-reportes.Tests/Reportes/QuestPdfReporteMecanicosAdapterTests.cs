using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Infraestructura.Documentos;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;
[Trait("Area", "Backend")]
public class QuestPdfReporteMecanicosAdapterTests
{
    static QuestPdfReporteMecanicosAdapterTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Evaluation;
    }

    [Fact]
    public void Generar_ProducePdfValidoConDatos()
    {
        var adapter = new QuestPdfReporteMecanicosAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteMecanicos
        {
            Filtro = new FiltroReporteMecanicos(),
            Filas =
            [
                new FilaReporteMecanico
                {
                    MecanicoId = 1,
                    NombreMecanico = "Carlos Ruiz",
                    CiMecanico = "1234567",
                    TotalOrdenes = 3,
                    TotalFacturado = 600.00m,
                    PromedioPorOrden = 200.00m
                },
                new FilaReporteMecanico
                {
                    MecanicoId = 2,
                    NombreMecanico = "Ana Torres",
                    CiMecanico = "7654321",
                    TotalOrdenes = 2,
                    TotalFacturado = 320.50m,
                    PromedioPorOrden = 160.25m
                }
            ],
            Resumen = new ResumenReporteMecanicos
            {
                TotalMecanicos = 2,
                TotalOrdenes = 5,
                TotalFacturado = 920.50m
            },
            GeneradoPor = "admin",
            FechaGeneracion = new DateTime(2026, 1, 15, 10, 0, 0)
        };

        byte[] pdf = adapter.Generar(documento);

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.UTF8.GetString(pdf, 0, 5));
    }

    [Fact]
    public void Generar_ConDatasetVacio_ProducePdfValido()
    {
        var adapter = new QuestPdfReporteMecanicosAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteMecanicos
        {
            Filtro = new FiltroReporteMecanicos(),
            Filas = [],
            Resumen = new ResumenReporteMecanicos(),
            GeneradoPor = "admin",
            FechaGeneracion = DateTime.UtcNow
        };

        byte[] pdf = adapter.Generar(documento);

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.UTF8.GetString(pdf, 0, 5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(120)]
    public void Generar_MuchosMecanicos_ConservaTablaYGraficoSinDesbordar(int cantidad)
    {
        byte[] pdf = new QuestPdfReporteMecanicosAdapter(ConfiguracionTaller.Instancia)
            .Generar(CrearDocumento(cantidad));

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        SoportePdf.GuardarMuestra(pdf, $"reporte-mecanicos-{cantidad}.pdf");
    }

    [PdfContenidoFact]
    public void Generar_MuchosMecanicos_IncluyeTodasLasFilasEnTablaYGrafico()
    {
        const int cantidad = 120;
        byte[] pdf = new QuestPdfReporteMecanicosAdapter(ConfiguracionTaller.Instancia)
            .Generar(CrearDocumento(cantidad));
        string texto = SoportePdf.ExtraerTexto(pdf);

        Assert.Contains("Total facturado por mecánico", texto);
        for (int indice = 1; indice <= cantidad; indice++)
        {
            string nombre = $"MecanicoPrueba{indice:000}";
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(texto, nombre).Count);
        }
    }

    private static DocumentoReporteMecanicos CrearDocumento(int cantidad) => new()
    {
        Filas = Enumerable.Range(1, cantidad).Select(indice => new FilaReporteMecanico
        {
            MecanicoId = indice,
            NombreMecanico = $"MecanicoPrueba{indice:000}",
            CiMecanico = $"{7000000 + indice}",
            TotalOrdenes = indice % 3,
            TotalFacturado = indice % 3 * 100m,
            PromedioPorOrden = indice % 3 == 0 ? 0 : 100m
        }).ToArray(),
        Resumen = new ResumenReporteMecanicos
        {
            TotalMecanicos = cantidad,
            TotalOrdenes = Enumerable.Range(1, cantidad).Sum(indice => indice % 3),
            TotalFacturado = Enumerable.Range(1, cantidad).Sum(indice => indice % 3 * 100m)
        },
        GeneradoPor = "admin_prueba",
        FechaGeneracion = DateTime.UtcNow
    };
}
