using System.Collections.Generic;
using System.Linq;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.IntegrationTests.Reportes;

public sealed class ConsultaReportesFalsa : IConsultaReportesPort
{
    public DocumentoReporteOrdenes? DevueltoOrdenes { get; private set; }
    public DocumentoReporteMecanicos? DevueltoMecanicos { get; private set; }
    public FiltroReporteOrdenes? UltimoFiltroOrdenes { get; private set; }
    public FiltroReporteMecanicos? UltimoFiltroMecanicos { get; private set; }
    public string? UltimoGeneradoPor { get; private set; }

    public DocumentoReporteOrdenes ObtenerReporteOrdenes(
        FiltroReporteOrdenes filtro,
        string generadoPor)
    {
        UltimoFiltroOrdenes = filtro;
        UltimoGeneradoPor = generadoPor;
        return DevueltoOrdenes ??= CrearPorDefectoOrdenes();
    }

    public DocumentoReporteMecanicos ObtenerReporteMecanicos(
        FiltroReporteMecanicos filtro,
        string generadoPor)
    {
        UltimoFiltroMecanicos = filtro;
        UltimoGeneradoPor = generadoPor;
        return DevueltoMecanicos ??= CrearPorDefectoMecanicos();
    }

    public IReadOnlyList<MecanicoListaDto> ObtenerMecanicos() => [];

    private static DocumentoReporteOrdenes CrearPorDefectoOrdenes() =>
        new()
        {
            Filas = [
                new FilaReporteOrden
                {
                    OrdenId = 1,
                    Fecha = new DateTime(2026, 1, 10, 9, 30, 0),
                    Placa = "AAA-111",
                    NombreCliente = "Juan Pérez",
                    NombreMecanico = "Carlos Ruiz",
                    NombreServicio = "Cambio de aceite",
                    Total = 150.00m,
                    Estado = "Activa",
                    CreadoPor = "sistema"
                },
                new FilaReporteOrden
                {
                    OrdenId = 2,
                    Fecha = new DateTime(2026, 1, 11, 14, 0, 0),
                    Placa = "BBB-222",
                    NombreCliente = "María López",
                    NombreMecanico = "Ana Torres",
                    NombreServicio = "Frenos",
                    Total = 320.50m,
                    Estado = "Anulada",
                    CreadoPor = "sistema"
                }
            ],
            Resumen = new ResumenReporteOrdenes
            {
                TotalOrdenes = 2,
                TotalGeneral = 470.50m,
                OrdenesActivas = 1,
                OrdenesAnuladas = 1
            },
            GeneradoPor = "sistema"
        };

    private static DocumentoReporteMecanicos CrearPorDefectoMecanicos() =>
        new()
        {
            Filas = [
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
            GeneradoPor = "sistema"
        };
}

public sealed class PdfOrdenesFalso : IGeneradorReporteOrdenesPdfPort
{
    public byte[] UltimoGenerado { get; private set; } = [];
    public DocumentoReporteOrdenes? UltimoDocumento { get; private set; }
    public byte[] Generar(DocumentoReporteOrdenes documento)
    {
        UltimoDocumento = documento;
        UltimoGenerado = System.Text.Encoding.UTF8.GetBytes($"PDF:{documento.Filas.Count}");
        return UltimoGenerado;
    }
}

public sealed class ExcelOrdenesFalso : IGeneradorReporteOrdenesExcelPort
{
    public byte[] UltimoGenerado { get; private set; } = [];
    public DocumentoReporteOrdenes? UltimoDocumento { get; private set; }
    public byte[] Generar(DocumentoReporteOrdenes documento)
    {
        UltimoDocumento = documento;
        UltimoGenerado = System.Text.Encoding.UTF8.GetBytes($"XLSX:{documento.Filas.Count}");
        return UltimoGenerado;
    }
}

public sealed class PdfMecanicosFalso : IGeneradorReporteMecanicosPdfPort
{
    public byte[] UltimoGenerado { get; private set; } = [];
    public DocumentoReporteMecanicos? UltimoDocumento { get; private set; }
    public byte[] Generar(DocumentoReporteMecanicos documento)
    {
        UltimoDocumento = documento;
        UltimoGenerado = System.Text.Encoding.UTF8.GetBytes($"PDF:{documento.Filas.Count}");
        return UltimoGenerado;
    }
}

public sealed class ExcelMecanicosFalso : IGeneradorReporteMecanicosExcelPort
{
    public byte[] UltimoGenerado { get; private set; } = [];
    public DocumentoReporteMecanicos? UltimoDocumento { get; private set; }
    public byte[] Generar(DocumentoReporteMecanicos documento)
    {
        UltimoDocumento = documento;
        UltimoGenerado = System.Text.Encoding.UTF8.GetBytes($"XLSX:{documento.Filas.Count}");
        return UltimoGenerado;
    }
}
