using System.Data.Common;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

public sealed class ConsultaOrdenesRepository(DatabaseConnectionFactory connectionFactory)
    : IConsultaOrdenesPort
{
    public IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null)
    {
        using DbConnection conexion = connectionFactory.CreateConnection();
        conexion.Open();

        const string consulta = """
            SELECT Orden.Id,
                   Orden.Fecha,
                   Vehiculo.Placa,
                   NULLIF(CONCAT_WS(' ', Cliente.Nombres,
                       Cliente.PrimerApellido, Cliente.SegundoApellido), '') AS NombreCliente,
                   CONCAT_WS(' ', Mecanico.Nombres,
                       Mecanico.PrimerApellido, Mecanico.SegundoApellido) AS NombreMecanico,
                   Orden.Total,
                   Orden.Estado,
                   Orden.CreadoPor
            FROM Ordenes AS Orden
            JOIN Vehiculos AS Vehiculo ON Vehiculo.Id = Orden.VehiculoId
            LEFT JOIN Clientes AS Cliente ON Cliente.Id = Vehiculo.ClienteId
            JOIN Mecanicos AS Mecanico ON Mecanico.Id = Orden.MecanicoId
            WHERE (@Estado IS NULL OR Orden.Estado = @Estado)
              AND (@Fecha IS NULL OR Orden.Fecha >= @Fecha)
              AND (@FechaSiguiente IS NULL OR Orden.Fecha < @FechaSiguiente)
            ORDER BY Orden.Fecha DESC, Orden.Id DESC;
            """;

        using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = consulta;
        AgregarParametro(comando, "@Estado", estado?.ToString());
        AgregarParametro(comando, "@Fecha", fecha?.ToDateTime(TimeOnly.MinValue));
        AgregarParametro(comando, "@FechaSiguiente",
            fecha.HasValue && fecha.Value < DateOnly.MaxValue
                ? fecha.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
                : null);

        using DbDataReader lector = comando.ExecuteReader();
        var ordenes = new List<OrdenResumen>();

        while (lector.Read())
        {
            ordenes.Add(MapearResumen(lector));
        }

        return ordenes;
    }

    public OrdenDetalle? ObtenerDetalle(int id)
    {
        using DbConnection conexion = connectionFactory.CreateConnection();
        conexion.Open();

        const string consulta = """
            SELECT Orden.Id, Orden.Fecha, Orden.Estado, Orden.Total,
                   Orden.CreadoPor, Orden.FechaCreacion, Orden.AnuladoPor, Orden.FechaAnulacion,
                   Vehiculo.Placa, Vehiculo.Marca, Vehiculo.Modelo,
                   NULLIF(CONCAT_WS(' ', Cliente.Nombres,
                       Cliente.PrimerApellido, Cliente.SegundoApellido), '') AS NombreCliente,
                   Cliente.Ci AS CiCliente, Cliente.ComplementoCi AS ComplementoCiCliente,
                   Cliente.Celular AS CelularCliente,
                   CONCAT_WS(' ', Mecanico.Nombres,
                       Mecanico.PrimerApellido, Mecanico.SegundoApellido) AS NombreMecanico,
                   Mecanico.Ci AS CiMecanico, Mecanico.ComplementoCi AS ComplementoCiMecanico,
                   Detalle.Id AS DetalleId, Detalle.Cantidad, Detalle.PrecioUnitario, Detalle.Subtotal,
                   Producto.Codigo, Producto.Nombre
            FROM Ordenes AS Orden
            JOIN Vehiculos AS Vehiculo ON Vehiculo.Id = Orden.VehiculoId
            LEFT JOIN Clientes AS Cliente ON Cliente.Id = Vehiculo.ClienteId
            JOIN Mecanicos AS Mecanico ON Mecanico.Id = Orden.MecanicoId
            LEFT JOIN DetalleOrdenes AS Detalle ON Detalle.OrdenId = Orden.Id
            LEFT JOIN Productos AS Producto ON Producto.Id = Detalle.ProductoId
            WHERE Orden.Id = @Id
            ORDER BY Detalle.Id;
            """;

        using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = consulta;
        AgregarParametro(comando, "@Id", id);
        using DbDataReader lector = comando.ExecuteReader();

        if (!lector.Read()) return null;

        var productos = new List<ProductoOrdenDetalle>();
        var detalle = MapearDetalle(lector, productos.AsReadOnly());
        int indiceDetalle = lector.GetOrdinal("DetalleId");

        do
        {
            if (!lector.IsDBNull(indiceDetalle))
            {
                productos.Add(MapearProducto(lector));
            }
        } while (lector.Read());

        return detalle;
    }

    private static OrdenDetalle MapearDetalle(
        DbDataReader lector, IReadOnlyList<ProductoOrdenDetalle> productos) => new()
    {
        Id = lector.GetInt32(lector.GetOrdinal(nameof(OrdenDetalle.Id))),
        Fecha = lector.GetDateTime(lector.GetOrdinal(nameof(OrdenDetalle.Fecha))),
        Estado = Enum.Parse<EstadoOrden>(lector.GetString(lector.GetOrdinal(nameof(OrdenDetalle.Estado)))),
        Total = lector.GetDecimal(lector.GetOrdinal(nameof(OrdenDetalle.Total))),
        Placa = lector.GetString(lector.GetOrdinal(nameof(OrdenDetalle.Placa))),
        Marca = lector.GetString(lector.GetOrdinal(nameof(OrdenDetalle.Marca))),
        Modelo = lector.GetString(lector.GetOrdinal(nameof(OrdenDetalle.Modelo))),
        NombreCliente = LeerTextoOpcional(lector, nameof(OrdenDetalle.NombreCliente)),
        CiCliente = LeerTextoOpcional(lector, nameof(OrdenDetalle.CiCliente)),
        ComplementoCiCliente = LeerTextoOpcional(lector, nameof(OrdenDetalle.ComplementoCiCliente)),
        CelularCliente = LeerTextoOpcional(lector, nameof(OrdenDetalle.CelularCliente)),
        NombreMecanico = lector.GetString(lector.GetOrdinal(nameof(OrdenDetalle.NombreMecanico))),
        CiMecanico = lector.GetString(lector.GetOrdinal(nameof(OrdenDetalle.CiMecanico))),
        ComplementoCiMecanico = lector.GetString(lector.GetOrdinal(nameof(OrdenDetalle.ComplementoCiMecanico))),
        CreadoPor = lector.GetString(lector.GetOrdinal(nameof(OrdenDetalle.CreadoPor))),
        FechaCreacion = LeerFechaUtc(lector, nameof(OrdenDetalle.FechaCreacion))!.Value,
        AnuladoPor = LeerTextoOpcional(lector, nameof(OrdenDetalle.AnuladoPor)),
        FechaAnulacion = LeerFechaUtc(lector, nameof(OrdenDetalle.FechaAnulacion)),
        Productos = productos
    };

    private static ProductoOrdenDetalle MapearProducto(DbDataReader lector) => new(
        lector.GetString(lector.GetOrdinal(nameof(ProductoOrdenDetalle.Codigo))),
        lector.GetString(lector.GetOrdinal(nameof(ProductoOrdenDetalle.Nombre))),
        lector.GetInt32(lector.GetOrdinal(nameof(ProductoOrdenDetalle.Cantidad))),
        lector.GetDecimal(lector.GetOrdinal(nameof(ProductoOrdenDetalle.PrecioUnitario))),
        lector.GetDecimal(lector.GetOrdinal(nameof(ProductoOrdenDetalle.Subtotal))));

    private static string? LeerTextoOpcional(DbDataReader lector, string columna)
    {
        int indice = lector.GetOrdinal(columna);
        return lector.IsDBNull(indice) ? null : lector.GetString(indice);
    }

    private static DateTime? LeerFechaUtc(DbDataReader lector, string columna)
    {
        int indice = lector.GetOrdinal(columna);
        return lector.IsDBNull(indice) ? null : DateTime.SpecifyKind(lector.GetDateTime(indice), DateTimeKind.Utc);
    }

    private static OrdenResumen MapearResumen(DbDataReader lector)
    {
        var indiceCliente = lector.GetOrdinal(nameof(OrdenResumen.NombreCliente));

        return new OrdenResumen(
            lector.GetInt32(lector.GetOrdinal(nameof(OrdenResumen.Id))),
            lector.GetDateTime(lector.GetOrdinal(nameof(OrdenResumen.Fecha))),
            lector.GetString(lector.GetOrdinal(nameof(OrdenResumen.Placa))),
            lector.IsDBNull(indiceCliente) ? null : lector.GetString(indiceCliente),
            lector.GetString(lector.GetOrdinal(nameof(OrdenResumen.NombreMecanico))),
            lector.GetDecimal(lector.GetOrdinal(nameof(OrdenResumen.Total))),
            Enum.Parse<EstadoOrden>(lector.GetString(lector.GetOrdinal(nameof(OrdenResumen.Estado)))),
            lector.GetString(lector.GetOrdinal(nameof(OrdenResumen.CreadoPor))));
    }

    private static void AgregarParametro(DbCommand comando, string nombre, object? contenido)
    {
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = contenido ?? DBNull.Value;
        comando.Parameters.Add(parametro);
    }
}
