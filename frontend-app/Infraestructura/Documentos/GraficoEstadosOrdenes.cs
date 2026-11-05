using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TallerMecanico.Infraestructura.Documentos;

// Una composición para que las exportaciones PDF y Excel muestren los mismos datos.
internal static class GraficoEstadosOrdenes
{
    private const string ColorActivas = "#15803d";
    private const string ColorAnuladas = "#c24152";
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-BO");

    public static void Componer(IContainer contenedor, ResumenReporteOrdenes resumen)
    {
        int total = resumen.OrdenesActivas + resumen.OrdenesAnuladas;
        int maximo = Math.Max(1, Math.Max(resumen.OrdenesActivas, resumen.OrdenesAnuladas));

        contenedor.Border(1).BorderColor("#e5dfee").Padding(12).Column(columna =>
        {
            columna.Spacing(9);
            columna.Item().Text("Órdenes por estado").FontSize(12).Bold().FontColor("#6d28d9");
            columna.Item().Text("Cantidad de órdenes del resultado filtrado")
                .FontSize(9).FontColor("#6b6378");

            ComponerBarra(columna.Item(), "Activas", resumen.OrdenesActivas, maximo, total, ColorActivas);
            ComponerBarra(columna.Item(), "Anuladas", resumen.OrdenesAnuladas, maximo, total, ColorAnuladas);

            columna.Item().Text("Verde: activas · Coral: anuladas").FontSize(8).FontColor("#6b6378");
            if (total == 0)
                columna.Item().Text("Sin órdenes para los filtros seleccionados.")
                    .FontSize(9).Italic().FontColor("#6b6378");
        });
    }

    private static void ComponerBarra(IContainer contenedor, string estado,
        int cantidad, int maximo, int total, string color)
    {
        string porcentaje = (total == 0 ? 0m : 100m * cantidad / total).ToString("0.#", Cultura);
        contenedor.Row(fila =>
        {
            fila.ConstantItem(64).AlignMiddle().Text(estado).FontSize(10).SemiBold().FontColor(color);
            fila.RelativeItem().AlignMiddle().Row(barra =>
            {
                if (cantidad > 0)
                    barra.RelativeItem(cantidad).Height(14).Background(color);
                if (cantidad < maximo)
                    barra.RelativeItem(maximo - cantidad).Height(14).Background("#ede9f2");
            });
            fila.ConstantItem(80).AlignRight().AlignMiddle()
                .Text($"{cantidad:N0} ({porcentaje}%)").FontSize(10).FontColor("#251e35");
        });
    }

    public static byte[] GenerarImagen(ResumenReporteOrdenes resumen) =>
        Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(520, 200);
            pagina.Margin(8);
            pagina.DefaultTextStyle(estilo => estilo.FontFamily("Lato"));
            pagina.Content().Element(contenedor => Componer(contenedor, resumen));
        })).GenerateImages(new ImageGenerationSettings { RasterDpi = 144 }).Single();
}
