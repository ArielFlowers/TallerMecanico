using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Dominio.Servicios;

namespace TallerMecanico.IntegrationTests;

public sealed class ServiciosPresentacionTests
{
    [Theory]
    [InlineData(3, 300, 100)]
    [InlineData(1, 250, 250)]
    [InlineData(0, 120, 120)]
    public void PrecioUnitario_SeDerivaDelSubtotalHistoricoSinModificarlo(int cantidad, int subtotal, int precio)
    {
        var servicio = new ServicioOrdenDetalle { Costo = subtotal, Cantidad = cantidad };

        Assert.Equal(precio, servicio.PrecioUnitario);
        Assert.Equal(subtotal, servicio.Costo);
    }

    [PdfContenidoFact]
    public void Pdf_CantidadesYSubtotalesHistoricosNoSeMultiplicanOtraVez()
    {
        var orden = SoporteComprobantePdf.Orden() with
        {
            Total = 581.50m,
            Servicios = [
                new() { Nombre = "Cambio de filtros", Descripcion = "Servicio aislado de prueba", Costo = 300m, Cantidad = 3, UnidadCantidad = "unidad", TiempoEstimadoHoras = 3 },
                new() { Nombre = "Cambio de aceite", Descripcion = "Servicio único de prueba", Costo = 250m, Cantidad = 1, TiempoEstimadoHoras = 1 }
            ]
        };
        byte[] pdf = SoporteComprobantePdf.CrearGenerador().Generar(orden,
            new ImporteEnPalabras().Convertir(orden.Total), "generador_prueba", TimeProvider.System.GetUtcNow());
        string texto = SoporteComprobantePdf.ExtraerTexto(pdf);

        foreach (string esperado in new[] { "Cambio de filtros", "Cantidad: 3", "Precio unitario: Bs. 100,00", "Subtotal: Bs. 300,00",
            "Cambio de aceite", "Cantidad: 1", "Precio unitario: Bs. 250,00", "Subtotal: Bs. 250,00", "TOTAL: Bs. 581,50" })
            Assert.Contains(esperado, texto);
        Assert.DoesNotContain("900,00", texto);
        SoporteComprobantePdf.GuardarMuestra(pdf, "servicios-cantidades.pdf");
    }
}
