using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Infraestructura.Documentos;

namespace TallerMecanico.Infraestructura.Exportacion;

public sealed class ClosedXmlReporteOrdenesAdapter(ConfiguracionTaller taller)
    : IGeneradorReporteOrdenesExcelPort
{
    private const string NombreHoja = "Órdenes de Trabajo";

    public byte[] Generar(DocumentoReporteOrdenes documento)
    {
        var cabeceras = new[]
        {
            "#", "Fecha", "Placa", "Cliente", "Mecánico", "Servicio", "Total", "Estado"
        };

        var filas = new List<IReadOnlyList<object?>>();
        foreach (var orden in documento.Filas)
        {
            filas.Add(new List<object?>
            {
                orden.OrdenId,
                orden.Fecha,
                orden.Placa,
                orden.NombreCliente ?? "Sin cliente",
                orden.NombreMecanico,
                orden.NombreServicio ?? "Sin servicio",
                orden.Total,
                orden.Estado
            });
        }

        var resumen = new List<XlsxFilaResumen>
        {
            new() { Etiqueta = "Órdenes totales", Valor = documento.Resumen.TotalOrdenes.ToString("N0") },
            new() { Etiqueta = "Total facturado", Valor = $"{taller.Moneda} {documento.Resumen.TotalGeneral.ToString("N2")}" },
            new() { Etiqueta = "Activas", Valor = documento.Resumen.OrdenesActivas.ToString("N0") },
            new() { Etiqueta = "Anuladas", Valor = documento.Resumen.OrdenesAnuladas.ToString("N0") }
        };

        return XlsxBuilder.Build(
            NombreHoja,
            "Reporte detallado de órdenes de trabajo",
            cabeceras,
            filas,
            resumen,
            columnasMoneda: new[] { 6 },
            columnasFecha: new[] { 1 },
            grafico: GraficoEstadosOrdenes.GenerarImagen(documento.Resumen));
    }
}
