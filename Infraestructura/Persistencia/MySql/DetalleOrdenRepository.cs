using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class DetalleOrdenRepository(DbConnection conexion, DbTransaction transaccion)
    : RepositorioOrdenSql(conexion, transaccion), IDetalleOrdenPort
{
    public IReadOnlyList<DetalleOrden> ObtenerPorOrden(int ordenId) => Ejecutar(() =>
    {
        using DbCommand comando = CrearComando("""
            SELECT Id, OrdenId, ProductoId, Cantidad, PrecioUnitario, Subtotal
            FROM DetalleOrdenes WHERE OrdenId = @Orden ORDER BY ProductoId;
            """, ("@Orden", ordenId));
        using DbDataReader lector = comando.ExecuteReader();
        var detalles = new List<DetalleOrden>();
        while (lector.Read()) detalles.Add(new DetalleOrden
        {
            Id = lector.GetInt32(0), OrdenId = lector.GetInt32(1), ProductoId = lector.GetInt32(2),
            Cantidad = lector.GetInt32(3), PrecioUnitario = lector.GetDecimal(4), Subtotal = lector.GetDecimal(5)
        });
        return detalles;
    });

    public void Insertar(DetalleOrden detalle) => Ejecutar(() =>
    {
        using DbCommand comando = CrearComando("""
            INSERT INTO DetalleOrdenes (OrdenId, ProductoId, Cantidad, PrecioUnitario, Subtotal)
            VALUES (@Orden, @Producto, @Cantidad, @Precio, @Subtotal);
            """, ("@Orden", detalle.OrdenId), ("@Producto", detalle.ProductoId), ("@Cantidad", detalle.Cantidad),
            ("@Precio", detalle.PrecioUnitario), ("@Subtotal", detalle.Subtotal));
        return comando.ExecuteNonQuery();
    });
}
