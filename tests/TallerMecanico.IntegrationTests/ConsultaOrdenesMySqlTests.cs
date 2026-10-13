using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.IntegrationTests;

[Collection("MySQL aislado")]
public sealed class ConsultaOrdenesMySqlTests
{
    private static int _ci = 6000000;

    private static ConsultaOrdenesFacade Fachada(MySqlAisladoFixture aislada) =>
        SoporteComprobantePdf.CrearFachada(new ConsultaOrdenesService(
            new CreadorConsultaOrdenes(aislada.Factory).CrearRepositorio(), new UsuarioPrueba()));

    [MySqlAisladoFact]
    public void Listado_UnaFilaPorOrdenAunqueTengaVariosProductos()
    {
        using var aislada = new MySqlAisladoFixture();
        int id = CrearOrden(aislada, new DateTime(2026, 10, 12));
        CrearOrden(aislada, new DateTime(2026, 10, 11), EstadoOrden.Anulada);

        var ordenes = Fachada(aislada).Obtener();

        Assert.Equal(2, ordenes.Count);
        Assert.Equal(ordenes.Count, ordenes.Select(orden => orden.Id).Distinct().Count());
        var orden = Assert.Single(ordenes, orden => orden.Id == id);
        Assert.Equal("Ana María Arce Pérez", orden.NombreCliente);
        Assert.Equal("José Muñoz Núñez", orden.NombreMecanico);
        Assert.Equal("recepcion_prueba", orden.CreadoPor);
        Assert.Equal(15, orden.Placa.Length);
    }

    [MySqlAisladoFact]
    public void FiltroEstado_DevuelveActivasOAnuladasSinOcultarElListadoCompleto()
    {
        using var aislada = new MySqlAisladoFixture();
        int activa = CrearOrden(aislada, new DateTime(2026, 10, 12));
        int anulada = CrearOrden(aislada, new DateTime(2026, 10, 12), EstadoOrden.Anulada);
        var fachada = Fachada(aislada);

        Assert.Equal(activa, Assert.Single(fachada.Obtener(EstadoOrden.Activa)).Id);
        Assert.Equal(anulada, Assert.Single(fachada.Obtener(EstadoOrden.Anulada)).Id);
        Assert.Equal(2, fachada.Obtener().Count);
    }

    [MySqlAisladoFact]
    public void FiltroFecha_IncluyeTodoElDiaDelTrabajoYNoUsaFechaCreacion()
    {
        using var aislada = new MySqlAisladoFixture();
        int inicio = CrearOrden(aislada, new DateTime(2026, 10, 12));
        int fin = CrearOrden(aislada, new DateTime(2026, 10, 12, 23, 59, 59));
        CrearOrden(aislada, new DateTime(2026, 10, 11, 23, 59, 59));
        CrearOrden(aislada, new DateTime(2026, 10, 13));

        var ordenes = Fachada(aislada).Obtener(fecha: new DateOnly(2026, 10, 12));

        Assert.Equal(new[] { fin, inicio }, ordenes.Select(orden => orden.Id));
        Assert.All(ordenes, orden => Assert.Equal(new DateOnly(2026, 10, 12), DateOnly.FromDateTime(orden.Fecha)));
    }

    [MySqlAisladoFact]
    public void FiltrosCombinados_AplicanEstadoYFecha()
    {
        using var aislada = new MySqlAisladoFixture();
        CrearOrden(aislada, new DateTime(2026, 10, 12));
        int esperada = CrearOrden(aislada, new DateTime(2026, 10, 12), EstadoOrden.Anulada);
        CrearOrden(aislada, new DateTime(2026, 10, 13), EstadoOrden.Anulada);

        var ordenes = Fachada(aislada).Obtener(EstadoOrden.Anulada, new DateOnly(2026, 10, 12));

        Assert.Equal(esperada, Assert.Single(ordenes).Id);
    }

    [MySqlAisladoFact]
    public void Ordenamiento_FechaDescendenteYIdDescendenteConResultadoEstable()
    {
        using var aislada = new MySqlAisladoFixture();
        int primera = CrearOrden(aislada, new DateTime(2026, 10, 12));
        int segunda = CrearOrden(aislada, new DateTime(2026, 10, 12));
        int anterior = CrearOrden(aislada, new DateTime(2026, 10, 11));
        int reciente = CrearOrden(aislada, new DateTime(2026, 10, 13));
        var fachada = Fachada(aislada);

        Assert.Equal(new[] { reciente, segunda, primera, anterior }, fachada.Obtener().Select(orden => orden.Id));
        Assert.Equal(fachada.Obtener(), fachada.Obtener());
    }

    [MySqlAisladoFact]
    public void ListadoVacio_YFiltrosSinCoincidenciasDevuelvenColeccionVacia()
    {
        using var aislada = new MySqlAisladoFixture();
        var fachada = Fachada(aislada);
        Assert.Empty(fachada.Obtener());
        CrearOrden(aislada, new DateTime(2026, 10, 12));

        Assert.Empty(fachada.Obtener(fecha: new DateOnly(2026, 10, 10)));
        Assert.Empty(fachada.Obtener(EstadoOrden.Anulada));
    }

    [MySqlAisladoFact]
    public void Total_UsaElValorGuardadoYLaConsultaNoModificaOrdenesDetallesNiStock()
    {
        using var aislada = new MySqlAisladoFixture();
        CrearOrden(aislada, new DateTime(2026, 10, 12));
        aislada.Ejecutar("UPDATE Productos SET Precio = 999.99;");
        object? stock = aislada.Escalar("SELECT SUM(Stock) FROM Productos;");
        object? detalles = aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;");
        object? total = aislada.Escalar("SELECT Total FROM Ordenes;");

        var fachada = Fachada(aislada);
        Assert.Equal(31m, Assert.Single(fachada.Obtener()).Total);
        Assert.Equal(31m, Assert.Single(fachada.Obtener()).Total);
        Assert.Equal(stock, aislada.Escalar("SELECT SUM(Stock) FROM Productos;"));
        Assert.Equal(detalles, aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;"));
        Assert.Equal(total, aislada.Escalar("SELECT Total FROM Ordenes;"));
        Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
    }

    [MySqlAisladoFact]
    public void ClienteDesasignado_ConservaLaOrdenEnElListado()
    {
        using var aislada = new MySqlAisladoFixture();
        int id = CrearOrden(aislada, new DateTime(2026, 10, 12));
        aislada.Ejecutar("UPDATE Vehiculos SET ClienteId = NULL;");

        var orden = Assert.Single(Fachada(aislada).Obtener());

        Assert.Equal(id, orden.Id);
        Assert.Null(orden.NombreCliente);
    }

    [MySqlAisladoFact]
    public void FechaMaxima_NoDesbordaAlCalcularElDiaSiguiente()
    {
        using var aislada = new MySqlAisladoFixture();
        CrearOrden(aislada, new DateTime(9999, 12, 31, 23, 59, 59));

        Assert.Single(Fachada(aislada).Obtener(fecha: DateOnly.MaxValue));
    }

    [MySqlAisladoFact]
    public void Detalle_RecuperaRelacionesProductosEImportesHistoricosSinRecalcular()
    {
        using var aislada = new MySqlAisladoFixture();
        int id = CrearOrden(aislada, new DateTime(2026, 10, 12, 15, 30, 0));
        aislada.Ejecutar("UPDATE Productos SET Precio = 999.99;");
        aislada.Ejecutar("UPDATE Clientes SET ComplementoCi = '1A'; UPDATE Mecanicos SET ComplementoCi = '2B';");

        var detalle = Fachada(aislada).ObtenerDetalle(id);

        Assert.NotNull(detalle);
        Assert.Equal(id, detalle.Id);
        Assert.Equal(new DateTime(2026, 10, 12, 15, 30, 0), detalle.Fecha);
        Assert.Equal("Toyota", detalle.Marca);
        Assert.Equal("Corolla", detalle.Modelo);
        Assert.Equal(15, detalle.Placa.Length);
        Assert.Equal("Ana María Arce Pérez", detalle.NombreCliente);
        Assert.False(string.IsNullOrWhiteSpace(detalle.CiCliente));
        Assert.Equal("1A", detalle.ComplementoCiCliente);
        Assert.Equal("71234567", detalle.CelularCliente);
        Assert.Equal("José Muñoz Núñez", detalle.NombreMecanico);
        Assert.Equal(detalle.CiCliente, detalle.CiMecanico);
        Assert.Equal("2B", detalle.ComplementoCiMecanico);
        Assert.Equal(31m, detalle.Total);
        Assert.Equal(new[] { 10m, 21m }, detalle.Productos.Select(producto => producto.PrecioUnitario));
        Assert.Equal(new[] { 10m, 21m }, detalle.Productos.Select(producto => producto.Subtotal));
        Assert.All(detalle.Productos, producto => Assert.Equal(1, producto.Cantidad));
        Assert.All(detalle.Productos, producto => Assert.Equal("Producto de prueba", producto.Nombre));
        Assert.Equal(2, detalle.Productos.Select(producto => producto.Codigo).Distinct().Count());
    }

    [MySqlAisladoFact]
    public void Detalle_OrdenActivaConservaAuditoriaUtcSinAnulacion()
    {
        using var aislada = new MySqlAisladoFixture();
        int id = CrearOrden(aislada, new DateTime(2026, 10, 12));

        var detalle = Fachada(aislada).ObtenerDetalle(id)!;

        Assert.Equal(EstadoOrden.Activa, detalle.Estado);
        Assert.Equal("recepcion_prueba", detalle.CreadoPor);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), detalle.FechaCreacion);
        Assert.Equal(DateTimeKind.Utc, detalle.FechaCreacion.Kind);
        Assert.Null(detalle.AnuladoPor);
        Assert.Null(detalle.FechaAnulacion);
    }

    [MySqlAisladoFact]
    public void Detalle_OrdenAnuladaConservaProductosTotalYAmbasAuditorias()
    {
        using var aislada = new MySqlAisladoFixture();
        int id = CrearOrden(aislada, new DateTime(2026, 10, 12), EstadoOrden.Anulada);

        var detalle = Fachada(aislada).ObtenerDetalle(id)!;

        Assert.Equal(EstadoOrden.Anulada, detalle.Estado);
        Assert.Equal("recepcion_prueba", detalle.CreadoPor);
        Assert.Equal("admin_prueba", detalle.AnuladoPor);
        Assert.Equal(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc), detalle.FechaAnulacion);
        Assert.Equal(DateTimeKind.Utc, detalle.FechaAnulacion!.Value.Kind);
        Assert.Equal(2, detalle.Productos.Count);
        Assert.Equal(31m, detalle.Total);
    }

    [MySqlAisladoFact]
    public void Detalle_InexistenteDevuelveNullYSinClienteNoOcultaLaOrden()
    {
        using var aislada = new MySqlAisladoFixture();
        int id = CrearOrden(aislada, new DateTime(2026, 10, 12));
        aislada.Ejecutar("UPDATE Vehiculos SET ClienteId = NULL;");
        var fachada = Fachada(aislada);

        Assert.Null(fachada.ObtenerDetalle(int.MaxValue));
        var detalle = fachada.ObtenerDetalle(id)!;
        Assert.Null(detalle.NombreCliente);
        Assert.Null(detalle.CiCliente);
        Assert.Null(detalle.CelularCliente);
        Assert.Equal(2, detalle.Productos.Count);
    }

    [MySqlAisladoFact]
    public void Detalle_ConsultasRepetidasNoCambianStockEstadoAuditoriaNiImportes()
    {
        using var aislada = new MySqlAisladoFixture();
        int id = CrearOrden(aislada, new DateTime(2026, 10, 12), EstadoOrden.Anulada);
        object? stock = aislada.Escalar("SELECT SUM(Stock) FROM Productos;");
        object? orden = aislada.Escalar("SELECT CONCAT_WS('|', Estado, Total, Token, CreadoPor, FechaCreacion, AnuladoPor, FechaAnulacion) FROM Ordenes;");
        object? detalles = aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|', Id, ProductoId, Cantidad, PrecioUnitario, Subtotal) ORDER BY Id) FROM DetalleOrdenes;");
        var fachada = Fachada(aislada);

        Assert.Equal(2, fachada.ObtenerDetalle(id)!.Productos.Count);
        Assert.Equal(2, fachada.ObtenerDetalle(id)!.Productos.Count);
        Assert.Equal(stock, aislada.Escalar("SELECT SUM(Stock) FROM Productos;"));
        Assert.Equal(orden, aislada.Escalar("SELECT CONCAT_WS('|', Estado, Total, Token, CreadoPor, FechaCreacion, AnuladoPor, FechaAnulacion) FROM Ordenes;"));
        Assert.Equal(detalles, aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|', Id, ProductoId, Cantidad, PrecioUnitario, Subtotal) ORDER BY Id) FROM DetalleOrdenes;"));
    }

    internal static int CrearOrden(MySqlAisladoFixture aislada, DateTime fecha, EstadoOrden estado = EstadoOrden.Activa)
    {
        string ci = Interlocked.Increment(ref _ci).ToString();
        int clienteId = checked((int)aislada.Ejecutar("""
            INSERT INTO Clientes (Ci, Nombres, PrimerApellido, SegundoApellido, Celular)
            VALUES (@Ci, 'Ana María', 'Arce', 'Pérez', '71234567');
            """, ("@Ci", ci)));
        int vehiculoId = checked((int)aislada.Ejecutar("""
            INSERT INTO Vehiculos (Placa, Marca, Modelo, Kilometraje, ClienteId, EsPlacaExtranjera)
            VALUES (@Placa, 'Toyota', 'Corolla', 100, @Cliente, TRUE);
            """, ("@Placa", Guid.NewGuid().ToString("N")[..15].ToUpperInvariant()), ("@Cliente", clienteId)));
        int mecanicoId = checked((int)aislada.Ejecutar("""
            INSERT INTO Mecanicos (Ci, Nombres, PrimerApellido, SegundoApellido, Genero, Especialidad, Celular)
            VALUES (@Ci, 'José', 'Muñoz', 'Núñez', 'Masculino', 'Sin Especialidad', '71234567');
            """, ("@Ci", ci)));
        bool anulada = estado == EstadoOrden.Anulada;
        int ordenId = checked((int)aislada.Ejecutar("""
            INSERT INTO Ordenes (VehiculoId, MecanicoId, Fecha, Estado, Total, Token,
                CreadoPor, FechaCreacion, AnuladoPor, FechaAnulacion)
            VALUES (@Vehiculo, @Mecanico, @Fecha, @Estado, 31, @Token,
                'recepcion_prueba', '2026-01-01', @AnuladoPor, @FechaAnulacion);
            """, ("@Vehiculo", vehiculoId), ("@Mecanico", mecanicoId), ("@Fecha", fecha),
            ("@Estado", estado.ToString()), ("@Token", Guid.NewGuid().ToString("D")),
            ("@AnuladoPor", anulada ? "admin_prueba" : null),
            ("@FechaAnulacion", anulada ? new DateTime(2026, 10, 14) : null)));

        foreach (decimal precio in new[] { 10m, 21m })
        {
            int productoId = checked((int)aislada.Ejecutar("""
                INSERT INTO Productos (Codigo, Nombre, Precio, Stock, StockMinimo)
                VALUES (@Codigo, 'Producto de prueba', @Precio, 100, 0);
                """, ("@Codigo", Guid.NewGuid().ToString("N")), ("@Precio", precio)));
            aislada.Ejecutar("""
                INSERT INTO DetalleOrdenes (OrdenId, ProductoId, Cantidad, PrecioUnitario, Subtotal)
                VALUES (@Orden, @Producto, 1, @Precio, @Precio);
                """, ("@Orden", ordenId), ("@Producto", productoId), ("@Precio", precio));
        }

        return ordenId;
    }
}
