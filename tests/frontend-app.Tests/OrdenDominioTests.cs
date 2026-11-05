using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class OrdenDominioTests
{
    [Fact]
    public void DetalleOrden_CalculaSubtotal()
    {
        var detalle = new DetalleOrden { Cantidad = 3, PrecioUnitario = 12.50m };

        Assert.Equal(37.50m, detalle.CalcularSubtotal());
    }

    [Theory]
    [InlineData(1, 10, true)]
    [InlineData(0, 10, false)]
    [InlineData(-1, 10, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, -5, false)]
    public void DetalleOrden_ValidaCantidadYPrecio(int cantidad, int precio, bool esperado)
    {
        var detalle = new DetalleOrden { Cantidad = cantidad, PrecioUnitario = precio };

        Assert.Equal(esperado, detalle.EsValido());
    }

    [Fact]
    public void Orden_CalculaTotalConRepuestosYServicios()
    {
        var orden = new Orden
        {
            Servicios = [new DetalleServicioOrden { Costo = 100m }, new DetalleServicioOrden { Costo = 50m }]
        };
        DetalleOrden[] repuestos = [new DetalleOrden { Subtotal = 20m }, new DetalleOrden { Subtotal = 5m }];

        Assert.Equal(175m, orden.CalcularTotal(repuestos));
    }

    [Fact]
    public void Orden_NuevaPuedeAnularse()
    {
        var orden = new Orden();

        Assert.False(orden.EstaAnulada);
        Assert.True(orden.PuedeAnularse());
    }

    [Fact]
    public void Orden_AlAnularse_CambiaEstadoYRegistraAuditoria()
    {
        var orden = new Orden();
        var fecha = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        orden.Anular("admin", fecha);

        Assert.True(orden.EstaAnulada);
        Assert.False(orden.PuedeAnularse());
        Assert.Equal("admin", orden.AnuladoPor);
        Assert.Equal(fecha, orden.FechaAnulacion);
    }

    [Fact]
    public void Orden_NoSePuedeAnularDosVeces()
    {
        var orden = new Orden();
        var fecha = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        orden.Anular("admin", fecha);

        Assert.Throws<InvalidOperationException>(() => orden.Anular("otro", fecha.AddHours(1)));
        Assert.Equal("admin", orden.AnuladoPor);
        Assert.Equal(fecha, orden.FechaAnulacion);
    }
}
