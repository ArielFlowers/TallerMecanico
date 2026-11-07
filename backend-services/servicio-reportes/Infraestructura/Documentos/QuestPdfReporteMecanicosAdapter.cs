using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Configuracion;

namespace TallerMecanico.Infraestructura.Documentos;

public sealed class QuestPdfReporteMecanicosAdapter(ConfiguracionTaller taller)
    : IGeneradorReporteMecanicosPdfPort
{
    private const string ColorPrincipal = "#7c3aed";
    private const string ColorFondo = "#f5f0ff";
    private const string ColorTexto = "#251e35";
    private const string ColorSecundario = "#6b6378";
    private const string ColorBorde = "#e5dfee";
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-BO");
    private static readonly TimeSpan DesfaseBolivia = TimeSpan.FromHours(-4);
    private readonly ConfiguracionTaller _taller = taller;

    private static DateTimeOffset AFechaBolivia(DateTime fecha) =>
        new DateTimeOffset(DateTime.SpecifyKind(fecha, DateTimeKind.Utc)).ToOffset(DesfaseBolivia);

    public byte[] Generar(DocumentoReporteMecanicos documento)
    {
        return Document.Create(documentoPdf =>
        {
            documentoPdf.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(estilo =>
                    estilo.FontFamily("Lato").FontSize(9).FontColor(ColorTexto));
                page.Header().Element(contenedor => ComponerEncabezado(contenedor, documento));
                page.Content().PaddingVertical(16).Column(columna =>
                {
                    columna.Spacing(12);
                    columna.Item().Element(c => ComponerTabla(c, documento));
                    if (documento.Filas.Count > 0)
                        columna.Item().Element(c => GraficoMecanicos.Componer(c, documento.Filas, _taller.Moneda));
                });
                page.Footer().Element(contenedor => ComponerPie(contenedor, documento));
            });
        }).WithMetadata(new DocumentMetadata
        {
            Title = "Reporte estadístico por mecánico",
            Author = documento.GeneradoPor,
            Creator = _taller.NombreTaller,
            CreationDate = documento.FechaGeneracion,
            ModifiedDate = documento.FechaGeneracion
        }).GeneratePdf();
    }

    private void ComponerEncabezado(IContainer contenedor, DocumentoReporteMecanicos documento)
    {
        contenedor.Column(bloque =>
        {
            bloque.Item().BorderBottom(2).BorderColor(ColorPrincipal).PaddingBottom(16).Row(fila =>
        {
            fila.ConstantItem(44).Height(44).Image(LogoTaller.Imagen);
            fila.RelativeItem().PaddingLeft(12).Column(marca =>
            {
                marca.Item().Text(_taller.NombreTaller).FontSize(20).Bold();
                marca.Item().PaddingTop(4).Text("REPORTE ESTADÍSTICO POR MECÁNICO")
                    .FontSize(10).SemiBold().FontColor(ColorPrincipal);
            });
            fila.ConstantItem(125).AlignRight().Column(referencia =>
            {
                referencia.Item().Text("AUTO TALLER PRO").FontSize(14).Bold();
                referencia.Item().PaddingTop(6).Text(AFechaBolivia(documento.FechaGeneracion)
                    .ToString("dd/MM/yyyy HH:mm", Cultura))
                    .FontSize(8).FontColor(ColorSecundario);
            });
        });

        bloque.Item().PaddingTop(10).Column(info =>
        {
            info.Item().Text("Filtros aplicados").FontSize(8).Bold().FontColor(ColorSecundario);
            var filtros = new List<string>();
            if (!string.IsNullOrWhiteSpace(documento.Filtro.NombreMecanico))
            {
                filtros.Add($"Nombre: {documento.Filtro.NombreMecanico}");
            }
            if (documento.Filtro.FechaDesde.HasValue)
            {
                filtros.Add($"Desde: {documento.Filtro.FechaDesde.Value:dd/MM/yyyy}");
            }
            if (documento.Filtro.FechaHasta.HasValue)
            {
                filtros.Add($"Hasta: {documento.Filtro.FechaHasta.Value:dd/MM/yyyy}");
            }
            if (documento.Filtro.MecanicoId.HasValue)
            {
                filtros.Add($"Mecánico: {documento.Filtro.MecanicoId.Value}");
            }
            info.Item().Text(filtros.Count > 0
                    ? string.Join("  |  ", filtros)
                    : "Sin filtros aplicados.")
                .FontSize(9);
        });
        });

    }

    private void ComponerResumen(IContainer contenedor, DocumentoReporteMecanicos documento)
    {
        var resumen = documento.Resumen;
        contenedor.Background(ColorFondo).Border(1).BorderColor(ColorBorde).Padding(12).Column(columna =>
        {
            columna.Spacing(6);
            columna.Item().Text("Resumen del reporte").FontSize(9).Bold().FontColor(ColorPrincipal);
            columna.Item().Row(fila =>
            {
                fila.RelativeItem().Element(c => ComponerCajaResumen(c, "Mecánicos", resumen.TotalMecanicos.ToString("N0", Cultura)));
                fila.ConstantItem(12);
                fila.RelativeItem().Element(c => ComponerCajaResumen(c, "Órdenes totales", resumen.TotalOrdenes.ToString("N0", Cultura)));
                fila.ConstantItem(12);
                fila.RelativeItem().Element(c => ComponerCajaResumen(c, "Total facturado", $"{_taller.Moneda} {resumen.TotalFacturado.ToString("N2", Cultura)}"));
            });
        });
    }

    private static void ComponerCajaResumen(
        IContainer contenedor, string titulo, string valor)
    {
        contenedor.Border(1).BorderColor(ColorBorde).Padding(8).Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Text(titulo).FontSize(7.5f).Bold().FontColor(ColorSecundario);
            columna.Item().Text(valor).FontSize(10).Bold().FontColor(ColorTexto);
        });
    }

    private void ComponerTabla(IContainer contenedor, DocumentoReporteMecanicos documento)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(42);
                columnas.RelativeColumn(3);
                columnas.ConstantColumn(70);
                columnas.ConstantColumn(62);
                columnas.ConstantColumn(70);
                columnas.ConstantColumn(70);
            });

            tabla.Header(cabecera =>
            {
                foreach (string titulo in new[]
                {
                    "#", "Mecánico", "C.I.", "Órdenes", "Total facturado", "Promedio"
                })
                {
                    cabecera.Cell().Background(ColorPrincipal).Padding(8).Text(titulo)
                        .FontSize(8).SemiBold().FontColor(Colors.White);
                }
            });

            if (documento.Filas.Count == 0)
            {
                tabla.Cell().ColumnSpan(6).Padding(16).AlignCenter()
                    .Text("No se encontraron registros con los filtros seleccionados.")
                    .FontColor(ColorSecundario).Italic();
                return;
            }

            foreach (var fila in documento.Filas)
            {
                tabla.Cell().BorderBottom(1).BorderColor(ColorBorde).Padding(8)
                    .Text(fila.MecanicoId.ToString()).FontSize(8);
                tabla.Cell().BorderBottom(1).BorderColor(ColorBorde).Padding(8)
                    .Text(fila.NombreMecanico).FontSize(8);
                tabla.Cell().BorderBottom(1).BorderColor(ColorBorde).Padding(8)
                    .Text(fila.CiMecanico).FontSize(8);
                tabla.Cell().BorderBottom(1).BorderColor(ColorBorde).Padding(8)
                    .Text(fila.TotalOrdenes.ToString("N0", Cultura)).FontSize(8);
                tabla.Cell().BorderBottom(1).BorderColor(ColorBorde).Padding(8)
                    .AlignRight().Text($"{_taller.Moneda} {fila.TotalFacturado.ToString("N2", Cultura)}")
                    .FontSize(8);
                tabla.Cell().BorderBottom(1).BorderColor(ColorBorde).Padding(8)
                    .AlignRight().Text($"{_taller.Moneda} {fila.PromedioPorOrden.ToString("N2", Cultura)}")
                    .FontSize(8);
            }
        });
    }

    private void ComponerPie(IContainer contenedor, DocumentoReporteMecanicos documento)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Element(c => ComponerResumen(c, documento));

            columna.Item().PaddingTop(10).BorderTop(1).BorderColor(ColorBorde)
                .DefaultTextStyle(estilo => estilo.FontSize(7.5f).FontColor(ColorSecundario))
                .Column(detalle =>
                {
                    detalle.Spacing(4);
                    detalle.Item().Text($"Generado por: {documento.GeneradoPor}");
                    detalle.Item().Text($"Generación: {AFechaBolivia(documento.FechaGeneracion):dd/MM/yyyy HH:mm} (Hora de Bolivia)");
                    detalle.Item().Row(fila =>
                    {
                        fila.RelativeItem().Text($"Reporte: estadístico por mecánico | {_taller.NombreTaller}");
                        fila.AutoItem().Text(texto =>
                        {
                            texto.Span("Página ");
                            texto.CurrentPageNumber();
                            texto.Span(" de ");
                            texto.TotalPages();
                        });
                    });
                });
        });
    }
}
