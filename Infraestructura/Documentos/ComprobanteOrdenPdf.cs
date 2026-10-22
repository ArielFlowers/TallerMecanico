using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Configuracion;

namespace TallerMecanico.Infraestructura.Documentos;

public sealed class ComprobanteOrdenPdf(ConfiguracionTaller taller) : IComprobanteOrdenPdf
{
    private const string ColorPrincipal = "#7c3aed";
    private const string ColorFondo = "#f5f0ff";
    private const string ColorTexto = "#251e35";
    private const string ColorSecundario = "#6b6378";
    private const string ColorBorde = "#e5dfee";
    private static readonly CultureInfo CulturaBoliviana = CultureInfo.GetCultureInfo("es-BO");
    private static readonly TimeSpan DesfaseBolivia = TimeSpan.FromHours(-4);
    private readonly ConfiguracionTaller _taller = taller;

    public byte[] Generar(OrdenDetalle orden, string totalLiteral, string generadoPor, DateTimeOffset fechaGeneracion)
    {
        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(36);
            pagina.DefaultTextStyle(estilo => estilo.FontFamily("Lato").FontSize(10).FontColor(ColorTexto));
            pagina.Header().Element(contenedor => ComponerEncabezado(contenedor, orden));
            pagina.Content().PaddingVertical(20).Column(columna =>
            {
                columna.Spacing(18);
                columna.Item().Element(contenedor => ComponerInformacion(contenedor, orden));
                columna.Item().Element(contenedor => ComponerProductos(contenedor, orden.Productos));
                columna.Item().ShowEntire().Background(ColorFondo).Padding(16).Column(total =>
                {
                    total.Spacing(8);
                    total.Item().AlignRight().Text($"TOTAL: {_taller.Moneda} {FormatearImporte(orden.Total)}")
                        .FontSize(18).Bold().FontColor(ColorPrincipal);
                    total.Item().Text(totalLiteral).SemiBold();
                });
                if (orden.Estado == EstadoOrden.Anulada)
                {
                    columna.Item().Text($"Anulada por {orden.AnuladoPor} el {FormatearFechaUtc(orden.FechaAnulacion!.Value)} (Bolivia).")
                        .FontColor(Colors.Red.Darken2);
                }
            });
            pagina.Footer().Element(contenedor => ComponerPie(contenedor, orden, generadoPor, fechaGeneracion));
        })).WithMetadata(new DocumentMetadata
        {
            Title = $"Comprobante de venta - Orden {orden.Id}",
            Author = generadoPor,
            Creator = _taller.NombreTaller,
            CreationDate = fechaGeneracion.UtcDateTime,
            ModifiedDate = fechaGeneracion.UtcDateTime
        }).GeneratePdf();
    }

    private void ComponerEncabezado(IContainer contenedor, OrdenDetalle orden)
    {
        contenedor.BorderBottom(2).BorderColor(ColorPrincipal).PaddingBottom(16).Row(fila =>
        {
            fila.ConstantItem(44).Height(44).Background(ColorPrincipal).AlignCenter().AlignMiddle()
                .Text("AP").FontSize(20).Bold().FontColor(Colors.White);
            fila.RelativeItem().PaddingLeft(12).Column(marca =>
            {
                marca.Item().Text(_taller.NombreTaller).FontSize(20).Bold();
                marca.Item().PaddingTop(4).Text("COMPROBANTE DE VENTA").FontSize(10).SemiBold().FontColor(ColorPrincipal);
            });
            fila.ConstantItem(125).AlignRight().Column(referencia =>
            {
                referencia.Item().Text($"ORDEN #{orden.Id}").FontSize(14).Bold();
                referencia.Item().PaddingTop(6).Text(orden.Estado == EstadoOrden.Anulada ? "ANULADA" : "ACTIVA")
                    .Bold().FontColor(orden.Estado == EstadoOrden.Anulada ? Colors.Red.Darken2 : ColorPrincipal);
            });
        });
    }

    private void ComponerInformacion(IContainer contenedor, OrdenDetalle orden)
    {
        contenedor.Column(columna =>
        {
            columna.Spacing(12);
            columna.Item().Text($"Fecha del trabajo: {orden.Fecha.ToString(_taller.FormatoFecha)}").SemiBold();
            columna.Item().Row(fila =>
            {
                fila.RelativeItem().Element(caja => ComponerCaja(caja, "CLIENTE",
                    orden.NombreCliente ?? "Sin cliente asignado",
                    $"CI: {FormatearCi(orden.CiCliente, orden.ComplementoCiCliente)}",
                    $"Celular: {orden.CelularCliente ?? "No disponible"}"));
                fila.ConstantItem(12);
                fila.RelativeItem().Element(caja => ComponerCaja(caja, "VEHÍCULO",
                    $"Placa: {orden.Placa}", $"Marca: {orden.Marca}", $"Modelo: {orden.Modelo}"));
            });
            columna.Item().Element(caja => ComponerCaja(caja, "MECÁNICO RESPONSABLE", orden.NombreMecanico));
            IReadOnlyList<ServicioOrdenDetalle> servicios = orden.Servicios.Count > 0
                ? orden.Servicios
                : orden.Servicio is null ? [] : [orden.Servicio];
            if (servicios.Count > 0)
            {
                string[] lineasServicio = servicios.SelectMany(servicio => new[]
                {
                    servicio.Nombre,
                    servicio.Descripcion,
                    $"Costo: Bs. {FormatearImporte(servicio.Costo)}",
                    $"Tiempo estimado: {servicio.TiempoEstimadoHoras.ToString("0.##", CulturaBoliviana)} h"
                }).ToArray();
                columna.Item().Element(caja => ComponerCaja(caja,
                    servicios.Count == 1 ? "SERVICIO REALIZADO" : "SERVICIOS REALIZADOS",
                    lineasServicio));
            }
        });
    }

    private static void ComponerCaja(IContainer contenedor, string titulo, params string[] lineas)
    {
        contenedor.Border(1).BorderColor(ColorBorde).Padding(12).Column(columna =>
        {
            columna.Spacing(5);
            columna.Item().Text(titulo).FontSize(9).Bold().FontColor(ColorPrincipal);
            foreach (string linea in lineas) columna.Item().Text(linea);
        });
    }

    private static void ComponerProductos(IContainer contenedor, IReadOnlyList<ProductoOrdenDetalle> productos)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(40);
                columnas.RelativeColumn();
                columnas.ConstantColumn(90);
                columnas.ConstantColumn(132);
            });
            tabla.Header(cabecera =>
            {
                foreach (string titulo in new[] { "Cant.", "Descripción del producto", "Precio unitario (Bs.)", "Importe (Bs.)" })
                {
                    cabecera.Cell().Background(ColorPrincipal).Padding(8).Text(titulo)
                        .FontSize(9).SemiBold().FontColor(Colors.White);
                }
            });
            foreach (var producto in productos)
            {
                tabla.Cell().Element(CeldaProducto).Text(producto.Cantidad.ToString(CultureInfo.InvariantCulture));
                tabla.Cell().Element(CeldaProducto).Text(producto.Nombre);
                tabla.Cell().Element(CeldaProducto).AlignRight().Text(FormatearImporte(producto.PrecioUnitario));
                tabla.Cell().Element(CeldaProducto).AlignRight().Text(FormatearImporte(producto.Subtotal));
            }
        });
    }

    private static IContainer CeldaProducto(IContainer contenedor) =>
        contenedor.BorderBottom(1).BorderColor(ColorBorde).PaddingVertical(8).PaddingHorizontal(6)
            .DefaultTextStyle(estilo => estilo.FontSize(9));

    private void ComponerPie(IContainer contenedor, OrdenDetalle orden, string generadoPor, DateTimeOffset fechaGeneracion)
    {
        contenedor.BorderTop(1).BorderColor(ColorBorde).PaddingTop(10).DefaultTextStyle(estilo => estilo.FontSize(8).FontColor(ColorSecundario))
            .Column(columna =>
            {
                columna.Spacing(4);
                columna.Item().Text($"Creado por: {orden.CreadoPor} | Creación: {FormatearFechaUtc(orden.FechaCreacion)}");
                columna.Item().Text($"Copia generada por: {generadoPor} | Generación: {fechaGeneracion.ToOffset(DesfaseBolivia):dd/MM/yyyy HH:mm:ss}");
                columna.Item().Row(fila =>
                {
                    fila.RelativeItem().Text($"Referencia: orden #{orden.Id} | Horas de Bolivia (UTC-4)");
                    fila.AutoItem().Text(texto =>
                    {
                        texto.Span("Página ");
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                });
            });
    }

    private static string FormatearImporte(decimal importe) => importe.ToString("N2", CulturaBoliviana);

    private static string FormatearCi(string? ci, string? complemento) =>
        ci is null ? "No disponible" : string.IsNullOrEmpty(complemento) ? ci : $"{ci}-{complemento}";

    private static string FormatearFechaUtc(DateTime fecha) =>
        new DateTimeOffset(DateTime.SpecifyKind(fecha, DateTimeKind.Utc)).ToOffset(DesfaseBolivia)
            .ToString("dd/MM/yyyy HH:mm", CulturaBoliviana);
}
