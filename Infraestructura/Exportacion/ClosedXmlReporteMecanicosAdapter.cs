using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Infraestructura.Documentos;

namespace TallerMecanico.Infraestructura.Exportacion;

public sealed class ClosedXmlReporteMecanicosAdapter(ConfiguracionTaller taller)
    : IGeneradorReporteMecanicosExcelPort
{
    private const string NombreHoja = "Mecánicos";

    public byte[] Generar(DocumentoReporteMecanicos documento)
    {
        var cabeceras = new[]
        {
            "#", "Mecánico", "C.I.", "Órdenes", "Total facturado", "Promedio por orden"
        };

        var filas = new List<IReadOnlyList<object?>>();
        foreach (var mecanico in documento.Filas)
        {
            filas.Add(new List<object?>
            {
                mecanico.MecanicoId,
                mecanico.NombreMecanico,
                mecanico.CiMecanico,
                mecanico.TotalOrdenes,
                mecanico.TotalFacturado,
                mecanico.PromedioPorOrden
            });
        }

        var resumen = new List<XlsxFilaResumen>
        {
            new() { Etiqueta = "Mecánicos", Valor = documento.Resumen.TotalMecanicos.ToString("N0") },
            new() { Etiqueta = "Órdenes totales", Valor = documento.Resumen.TotalOrdenes.ToString("N0") },
            new() { Etiqueta = "Total facturado", Valor = $"{taller.Moneda} {documento.Resumen.TotalFacturado.ToString("N2")}" },
            new() { Etiqueta = "Nombre del mecánico", Valor = documento.Filtro.NombreMecanico ?? "Todos" },
            new() { Etiqueta = "Fecha desde", Valor = documento.Filtro.FechaDesde?.ToString("dd/MM/yyyy") ?? "Sin límite" },
            new() { Etiqueta = "Fecha hasta", Valor = documento.Filtro.FechaHasta?.ToString("dd/MM/yyyy") ?? "Sin límite" }
        };

        return XlsxBuilder.Build(
            NombreHoja,
            "Reporte estadístico por mecánico",
            cabeceras,
            filas,
            resumen,
            columnasMoneda: new[] { 4, 5 },
            grafico: documento.Filas.Count > 0
                ? GraficoMecanicos.GenerarImagen(documento.Filas, taller.Moneda)
                : null,
            tituloGrafico: GraficoMecanicos.Titulo,
            altoGrafico: (int)(GraficoMecanicos.AltoImagen(documento.Filas.Count) * 1.25f));
    }
}
