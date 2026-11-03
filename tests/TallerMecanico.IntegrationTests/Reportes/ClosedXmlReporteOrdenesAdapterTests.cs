using System.IO.Compression;
using System.Text;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Infraestructura.Configuracion;
using TallerMecanico.Infraestructura.Exportacion;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;

public class ClosedXmlReporteOrdenesAdapterTests
{
    [Fact]
    public void Generar_ProduceXlsxGenuinoConDatos()
    {
        var adapter = new ClosedXmlReporteOrdenesAdapter(ConfiguracionTaller.Instancia);
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

        byte[] xlsx = adapter.Generar(documento);

        // Un archivo XLSX genuino es un paquete ZIP (Open XML) con firma PK.
        Assert.NotEmpty(xlsx);
        Assert.Equal(0x50, xlsx[0]);
        Assert.Equal(0x4B, xlsx[1]);

        using var archivo = new ZipArchive(new MemoryStream(xlsx, writable: false));
        var nombres = archivo.Entries.Select(e => e.FullName).ToHashSet();
        Assert.Contains("[Content_Types].xml", nombres);
        Assert.Contains("_rels/.rels", nombres);
        Assert.Contains("xl/workbook.xml", nombres);
        Assert.Contains("xl/styles.xml", nombres);
        Assert.Contains("xl/worksheets/sheet1.xml", nombres);

        string hoja = LeerEntrada(archivo, "xl/worksheets/sheet1.xml");
        Assert.Contains("AutoTaller Pro", hoja);
        Assert.Contains("Reporte detallado", hoja);
        Assert.Contains("ABC-123", hoja);
        Assert.Contains("100", hoja);
    }

    [Fact]
    public void Generar_ConDatasetVacio_ProduceXlsxGenuino()
    {
        var adapter = new ClosedXmlReporteOrdenesAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteOrdenes
        {
            Filtro = new FiltroReporteOrdenes(),
            Filas = [],
            Resumen = new ResumenReporteOrdenes(),
            GeneradoPor = "admin",
            FechaGeneracion = DateTime.UtcNow
        };

        byte[] xlsx = adapter.Generar(documento);

        Assert.NotEmpty(xlsx);
        Assert.Equal(0x50, xlsx[0]);
        Assert.Equal(0x4B, xlsx[1]);

        using var archivo = new ZipArchive(new MemoryStream(xlsx, writable: false));
        Assert.Contains("xl/worksheets/sheet1.xml", archivo.Entries.Select(e => e.FullName));
    }

    private static string LeerEntrada(ZipArchive archivo, string nombre)
    {
        var entrada = archivo.GetEntry(nombre);
        Assert.NotNull(entrada);
        using var reader = new StreamReader(entrada.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
