using System.Text;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.IntegrationTests;

[Collection("MySQL aislado")]
public sealed class ComprobanteMySqlTests
{
    [MySqlAisladoFact]
    public void GeneracionRepetida_UsaLaOrdenGuardadaYNoAlteraStockEstadoAuditoriaNiTotal()
    {
        using var aislada = new MySqlAisladoFixture();
        int id = ConsultaOrdenesMySqlTests.CrearOrden(aislada, new DateTime(2026, 10, 12));
        aislada.Ejecutar("UPDATE Productos SET Precio = 999.99;");
        object? stock = aislada.Escalar("SELECT SUM(Stock) FROM Productos;");
        object? orden = aislada.Escalar("SELECT CONCAT_WS('|', Id, Total, Token, Estado, CreadoPor, FechaCreacion) FROM Ordenes;");
        object? detalles = aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|', Id, Cantidad, PrecioUnitario, Subtotal) ORDER BY Id) FROM DetalleOrdenes;");
        var usuario = new UsuarioPrueba { Username = "generador_copia" };
        var consultas = new ConsultaOrdenesService(new CreadorConsultaOrdenes(aislada.Factory).CrearRepositorio(), usuario);
        var fachada = SoporteComprobantePdf.CrearFachada(consultas, usuario);

        byte[] primera = fachada.GenerarComprobante(id)!;
        byte[] segunda = fachada.GenerarComprobante(id)!;
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(primera, 0, 8));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(segunda, 0, 8));
        Assert.Equal(stock, aislada.Escalar("SELECT SUM(Stock) FROM Productos;"));
        Assert.Equal(orden, aislada.Escalar("SELECT CONCAT_WS('|', Id, Total, Token, Estado, CreadoPor, FechaCreacion) FROM Ordenes;"));
        Assert.Equal(detalles, aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|', Id, Cantidad, PrecioUnitario, Subtotal) ORDER BY Id) FROM DetalleOrdenes;"));
        Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        Assert.Null(fachada.GenerarComprobante(int.MaxValue));
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_PDF_PYTHON")))
        {
            string texto = SoporteComprobantePdf.ExtraerTexto(primera);
            Assert.Contains("TOTAL: Bs. 31,00", texto);
            Assert.Contains("10,00", texto);
            Assert.Contains("21,00", texto);
            Assert.Contains("generador_copia", texto);
            Assert.DoesNotContain("999,99", texto);
        }
    }
}
