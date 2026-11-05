using TallerMecanico.Aplicacion.DTOs.Reportes;
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
}
