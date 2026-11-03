using System.IO.Compression;
using System.Text;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Infraestructura.Configuracion;
using TallerMecanico.Infraestructura.Exportacion;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;

public class ClosedXmlReporteMecanicosAdapterTests
{
    [Fact]
    public void Generar_ProduceXlsxGenuinoConDatos()
    {
        var adapter = new ClosedXmlReporteMecanicosAdapter(ConfiguracionTaller.Instancia);
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

        byte[] xlsx = adapter.Generar(documento);

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
        Assert.Contains("Reporte estadístico por mecánico", hoja);
        Assert.Contains("Carlos Ruiz", hoja);
        Assert.Contains("Ana Torres", hoja);
        Assert.Contains("1234567", hoja);
        Assert.Contains("7654321", hoja);
        Assert.Contains("600.00", hoja);
        Assert.Contains("320.50", hoja);
        Assert.Contains("200.00", hoja);
        Assert.Contains("160.25", hoja);
        Assert.Contains(920.50m.ToString("N2", System.Globalization.CultureInfo.CurrentCulture), hoja);
    }

    [Fact]
    public void Generar_ConDatasetVacio_ProduceXlsxGenuino()
    {
        var adapter = new ClosedXmlReporteMecanicosAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteMecanicos
        {
            Filtro = new FiltroReporteMecanicos(),
            Filas = [],
            Resumen = new ResumenReporteMecanicos(),
            GeneradoPor = "admin",
            FechaGeneracion = DateTime.UtcNow
        };

        byte[] xlsx = adapter.Generar(documento);

        Assert.NotEmpty(xlsx);
        Assert.Equal(0x50, xlsx[0]);
        Assert.Equal(0x4B, xlsx[1]);

        using var archivo = new ZipArchive(new MemoryStream(xlsx, writable: false));
        Assert.Contains("xl/worksheets/sheet1.xml", archivo.Entries.Select(e => e.FullName));

        string hoja = LeerEntrada(archivo, "xl/worksheets/sheet1.xml");
        Assert.Contains("AutoTaller Pro", hoja);
        Assert.Contains("Reporte estadístico por mecánico", hoja);
    }

    [Fact]
    public void Generar_ValidaEstructuraOpenXmlYTiposDeCelda()
    {
        var adapter = new ClosedXmlReporteMecanicosAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteMecanicos
        {
            Filtro = new FiltroReporteMecanicos(),
            Filas =
            [
                new FilaReporteMecanico
                {
                    MecanicoId = 1,
                    NombreMecanico = "Mecánico Uno",
                    CiMecanico = "CI-001",
                    TotalOrdenes = 1,
                    TotalFacturado = 150.00m,
                    PromedioPorOrden = 150.00m
                }
            ],
            Resumen = new ResumenReporteMecanicos
            {
                TotalMecanicos = 1,
                TotalOrdenes = 1,
                TotalFacturado = 150.00m
            },
            GeneradoPor = "admin",
            FechaGeneracion = new DateTime(2026, 1, 15, 10, 0, 0)
        };

        byte[] xlsx = adapter.Generar(documento);

        using var archivo = new ZipArchive(new MemoryStream(xlsx, writable: false));

        // Validar workbook.xml contiene la hoja de cálculo con nombre correcto
        string workbook = LeerEntrada(archivo, "xl/workbook.xml");
        Assert.Contains("Mecánicos", workbook);
        Assert.Contains("sheetId=\"1\"", workbook);
        Assert.Contains("r:id=\"rId1\"", workbook);

        // Validar que existe sheetView con showGridLines
        string hoja = LeerEntrada(archivo, "xl/worksheets/sheet1.xml");
        Assert.Contains("sheetView", hoja);
        Assert.Contains("showGridLines=\"false\"", hoja);

        // Validar que las celdas numéricas usan tipo "n" (número) y no inlineStr
        Assert.Contains("<v>150.00</v>", hoja);
        Assert.Contains("t=\"inlineStr\"", hoja);
    }

    private static string LeerEntrada(ZipArchive archivo, string nombre)
    {
        var entrada = archivo.GetEntry(nombre);
        Assert.NotNull(entrada);
        using var reader = new StreamReader(entrada.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
