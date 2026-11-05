using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Infraestructura.Documentos;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;
[Trait("Area", "Backend")]
public class QuestPdfReporteOrdenesAdapterTests
{
    static QuestPdfReporteOrdenesAdapterTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Evaluation;
    }

    [Fact]
    public void Generar_ProducePdfValidoConDatos()
    {
        var adapter = new QuestPdfReporteOrdenesAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteOrdenes
        {
            Filtro = new FiltroReporteOrdenes(),
            Filas =
            [
                new FilaReporteOrden
                {
                    OrdenId = 7,
                    Fecha = new DateTime(2026, 1, 15, 10, 0, 0),
                    Placa = "ABC-123",
                    NombreCliente = "Cliente Prueba",
                    NombreMecanico = "Mecánico Prueba",
                    NombreServicio = "Revisión",
                    Total = 100.00m,
                    Estado = "Activa",
                    CreadoPor = "admin"
                }
            ],
            Resumen = new ResumenReporteOrdenes
            {
                TotalOrdenes = 1,
                TotalGeneral = 100.00m,
                OrdenesActivas = 1,
                OrdenesAnuladas = 0
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
        var adapter = new QuestPdfReporteOrdenesAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteOrdenes
        {
            Filtro = new FiltroReporteOrdenes(),
            Filas = [],
            Resumen = new ResumenReporteOrdenes(),
            GeneradoPor = "admin",
            FechaGeneracion = DateTime.UtcNow
        };

        byte[] pdf = adapter.Generar(documento);

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.UTF8.GetString(pdf, 0, 5));
    }
}
