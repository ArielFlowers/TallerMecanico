using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public class ProductoRepository :
    IRepository<Producto>,
    IProductoPort
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public ProductoRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory;
    }

    public List<Producto> GetAll()
    {
        List<Producto> productos = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Codigo,
                   Nombre,
                   Precio,
                   Stock,
                   StockMinimo,
                   CreadoPor,
                   FechaCreacion
            FROM Productos
            ORDER BY Nombre ASC,
                     Codigo ASC;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        using DbDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            productos.Add(
                MapProducto(reader));
        }

        return productos;
    }

    IReadOnlyList<Producto> IProductoPort.GetAll()
    {
        return GetAll();
    }

    public Producto? GetById(
        int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Codigo,
                   Nombre,
                   Precio,
                   Stock,
                   StockMinimo,
                   CreadoPor,
                   FechaCreacion
            FROM Productos
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        AddParameter(
            command,
            "@Id",
            id);

        using DbDataReader reader =
            command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return MapProducto(
            reader);
    }

    public IReadOnlyList<Producto> Search(
        string terminoBusqueda)
    {
        List<Producto> productos = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Codigo,
                   Nombre,
                   Precio,
                   Stock,
                   StockMinimo,
                   CreadoPor,
                   FechaCreacion
            FROM Productos
            WHERE Codigo LIKE @PatronBusqueda ESCAPE '!'
               OR Nombre LIKE @PatronBusqueda ESCAPE '!'
            ORDER BY Nombre ASC,
                     Codigo ASC;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        AddParameter(
            command,
            "@PatronBusqueda",
            $"%{EscaparPatron(terminoBusqueda)}%");

        using DbDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            productos.Add(
                MapProducto(reader));
        }

        return productos;
    }

    public bool ExistsByCodigo(
        string codigo,
        int idExcluido = 0)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Productos
            WHERE Codigo = @Codigo
              AND Id <> @IdExcluido;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        AddParameter(
            command,
            "@Codigo",
            codigo);

        AddParameter(
            command,
            "@IdExcluido",
            idExcluido);

        return Convert.ToInt32(
            command.ExecuteScalar()) > 0;
    }

    public void Add(
        Producto producto)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Productos
            (
                Codigo,
                Nombre,
                Precio,
                Stock,
                StockMinimo,
                CreadoPor,
                FechaCreacion
            )
            VALUES
            (
                @Codigo,
                @Nombre,
                @Precio,
                @Stock,
                @StockMinimo,
                @CreadoPor,
                @FechaCreacion
            );
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        AddParameters(
            command,
            producto,
            incluirAuditoria: true);

        command.ExecuteNonQuery();
    }

    public void Update(
        Producto producto)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Productos
            SET Codigo = @Codigo,
                Nombre = @Nombre,
                Precio = @Precio,
                Stock = @Stock,
                StockMinimo = @StockMinimo
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        AddParameters(
            command,
            producto,
            incluirAuditoria: false);

        AddParameter(
            command,
            "@Id",
            producto.Id);

        command.ExecuteNonQuery();
    }

    public void Delete(
        int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            DELETE FROM Productos
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        AddParameter(
            command,
            "@Id",
            id);

        command.ExecuteNonQuery();
    }

    public bool Descontar(
        int productoId,
        int cantidad)
    {
        if (cantidad <= 0)
        {
            return false;
        }

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Productos
            SET Stock = Stock - @Cantidad
            WHERE Id = @Id
              AND Stock >= @Cantidad;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        AddParameter(
            command,
            "@Cantidad",
            cantidad);

        AddParameter(
            command,
            "@Id",
            productoId);

        return command.ExecuteNonQuery() == 1;
    }

    public void Restituir(
        int productoId,
        int cantidad)
    {
        if (cantidad <= 0)
        {
            return;
        }

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Productos
            SET Stock = Stock + @Cantidad
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        AddParameter(
            command,
            "@Cantidad",
            cantidad);

        AddParameter(
            command,
            "@Id",
            productoId);

        command.ExecuteNonQuery();
    }

    public int Count()
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Productos;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText =
            query;

        return Convert.ToInt32(
            command.ExecuteScalar());
    }

    // Estas sobrecargas no abren conexiones: participan en la transacción de la orden.
    public Producto? ObtenerParaActualizar(int id, DbConnection conexion, DbTransaction transaccion)
    {
        using DbCommand comando = RepositorioOrdenSql.CrearComandoCompartido(conexion, transaccion, """
            SELECT Id, Codigo, Nombre, Precio, Stock, StockMinimo, CreadoPor, FechaCreacion
            FROM Productos WHERE Id = @Id FOR UPDATE;
            """, ("@Id", id));
        using DbDataReader lector = comando.ExecuteReader();
        return lector.Read() ? MapProducto(lector) : null;
    }

    public bool Descontar(int productoId, int cantidad, DbConnection conexion, DbTransaction transaccion)
    {
        if (cantidad <= 0) return false;
        using DbCommand comando = RepositorioOrdenSql.CrearComandoCompartido(conexion, transaccion, """
            UPDATE Productos SET Stock = Stock - @Cantidad WHERE Id = @Id AND Stock >= @Cantidad;
            """, ("@Cantidad", cantidad), ("@Id", productoId));
        return comando.ExecuteNonQuery() == 1;
    }

    public bool Restituir(int productoId, int cantidad, DbConnection conexion, DbTransaction transaccion)
    {
        if (cantidad <= 0) return false;
        using DbCommand comando = RepositorioOrdenSql.CrearComandoCompartido(conexion, transaccion, """
            UPDATE Productos SET Stock = Stock + @Cantidad
            WHERE Id = @Id AND Stock <= 2147483647 - @Cantidad;
            """, ("@Cantidad", cantidad), ("@Id", productoId));
        return comando.ExecuteNonQuery() == 1;
    }

    private static void AddParameters(
        DbCommand command,
        Producto producto,
        bool incluirAuditoria)
    {
        AddParameter(
            command,
            "@Codigo",
            producto.Codigo);

        AddParameter(
            command,
            "@Nombre",
            producto.Nombre);

        AddParameter(
            command,
            "@Precio",
            producto.Precio);

        AddParameter(
            command,
            "@Stock",
            producto.Stock);

        AddParameter(
            command,
            "@StockMinimo",
            producto.StockMinimo);

        if (!incluirAuditoria)
        {
            return;
        }

        AddParameter(
            command,
            "@CreadoPor",
            producto.CreadoPor);

        AddParameter(
            command,
            "@FechaCreacion",
            producto.FechaCreacion);
    }

    private static Producto MapProducto(
        DbDataReader reader)
    {
        return new Producto
        {
            Id =
                reader.GetInt32(
                    reader.GetOrdinal("Id")),

            Codigo =
                reader.GetString(
                    reader.GetOrdinal("Codigo")),

            Nombre =
                reader.GetString(
                    reader.GetOrdinal("Nombre")),

            Precio =
                reader.GetDecimal(
                    reader.GetOrdinal("Precio")),

            Stock =
                reader.GetInt32(
                    reader.GetOrdinal("Stock")),

            StockMinimo =
                reader.GetInt32(
                    reader.GetOrdinal("StockMinimo")),

            CreadoPor =
                reader.GetString(
                    reader.GetOrdinal("CreadoPor")),

            FechaCreacion =
                reader.GetDateTime(
                    reader.GetOrdinal("FechaCreacion"))
        };
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object valor)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName =
            nombre;

        parameter.Value =
            valor;

        command.Parameters.Add(
            parameter);
    }

    private static string EscaparPatron(
        string terminoBusqueda)
    {
        return terminoBusqueda
            .Trim()
            .Replace(
                "!",
                "!!",
                StringComparison.Ordinal)
            .Replace(
                "%",
                "!%",
                StringComparison.Ordinal)
            .Replace(
                "_",
                "!_",
                StringComparison.Ordinal);
    }
}
