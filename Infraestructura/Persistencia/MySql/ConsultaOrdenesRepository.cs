using System.Data.Common;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

public sealed class ConsultaOrdenesRepository(DatabaseConnectionFactory connectionFactory)
    : IConsultaOrdenesPort
{
    private const int MaximoSugerencias = 10;

    public IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        string? terminoBusqueda = null,
        int? ordenSeleccionadaId = null)
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
                    COALESCE(
                        (SELECT GROUP_CONCAT(DetalleServicio.NombreServicio ORDER BY DetalleServicio.Id SEPARATOR ', ')
                         FROM DetalleServiciosOrdenes AS DetalleServicio
                         WHERE DetalleServicio.OrdenId = Orden.Id),
                        Servicio.Nombre) AS NombreServicio,
                    Orden.Total,
                    Orden.Estado,
                    Orden.CreadoPor
             FROM Ordenes AS Orden
             JOIN Vehiculos AS Vehiculo ON Vehiculo.Id = Orden.VehiculoId
             LEFT JOIN Clientes AS Cliente ON Cliente.Id = Vehiculo.ClienteId
             JOIN Mecanicos AS Mecanico ON Mecanico.Id = Orden.MecanicoId
             LEFT JOIN Servicios AS Servicio ON Servicio.Id = Orden.ServicioId
            WHERE (@Estado IS NULL OR Orden.Estado = @Estado)
              AND (@Fecha IS NULL OR Orden.Fecha >= @Fecha)
              AND (@FechaSiguiente IS NULL OR Orden.Fecha < @FechaSiguiente)
              AND (@OrdenId IS NULL OR Orden.Id = @OrdenId)
              AND (@Prefijo IS NULL
                   OR CAST(Orden.Id AS CHAR) LIKE @Prefijo ESCAPE '!'
                   OR Vehiculo.Placa COLLATE utf8mb4_0900_ai_ci LIKE @Prefijo ESCAPE '!')
            ORDER BY Orden.Fecha DESC, Orden.Id DESC;
            """;

        using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = consulta;
        AgregarFiltros(comando, estado, fecha);
        AgregarParametro(comando, "@OrdenId", ordenSeleccionadaId);
        AgregarParametro(comando, "@Prefijo",
            ordenSeleccionadaId.HasValue || string.IsNullOrWhiteSpace(terminoBusqueda)
                ? null
                : $"{EscaparPatron(terminoBusqueda)}%");

        using DbDataReader lector = comando.ExecuteReader();
        var ordenes = new List<OrdenResumen>();

        while (lector.Read())
        {
            ordenes.Add(MapearResumen(lector));
        }

        return ordenes;
    }

    public async Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(
        string termino,
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection conexion = connectionFactory.CreateConnection();
        await conexion.OpenAsync(cancellationToken);
        await using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Orden.Id, Vehiculo.Placa,
                   NULLIF(CONCAT_WS(' ', NULLIF(Cliente.Nombres, ''),
                       NULLIF(Cliente.PrimerApellido, ''), NULLIF(Cliente.SegundoApellido, '')), '')
            FROM Ordenes AS Orden
            JOIN Vehiculos AS Vehiculo ON Vehiculo.Id = Orden.VehiculoId
            LEFT JOIN Clientes AS Cliente ON Cliente.Id = Vehiculo.ClienteId
            WHERE (@Estado IS NULL OR Orden.Estado = @Estado)
              AND (@Fecha IS NULL OR Orden.Fecha >= @Fecha)
              AND (@FechaSiguiente IS NULL OR Orden.Fecha < @FechaSiguiente)
              AND (CAST(Orden.Id AS CHAR) LIKE @Prefijo ESCAPE '!'
                   OR Vehiculo.Placa COLLATE utf8mb4_0900_ai_ci LIKE @Prefijo ESCAPE '!')
            ORDER BY Orden.Fecha DESC, Orden.Id DESC
            LIMIT @Limite;
            """;
        AgregarFiltros(comando, estado, fecha);
        AgregarParametro(comando, "@Prefijo", $"{EscaparPatron(termino)}%");
        AgregarParametro(comando, "@Limite", MaximoSugerencias);

        var sugerencias = new List<SugerenciaOrden>();
        await using DbDataReader lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            int id = lector.GetInt32(0);
            string placa = lector.GetString(1);
            string textoSecundario = lector.IsDBNull(2) ? placa : $"{placa} · {lector.GetString(2)}";
            sugerencias.Add(new SugerenciaOrden(id, $"Orden #{id}", textoSecundario));
        }

        return sugerencias;
    }

    private static string EscaparPatron(string termino) =>
        termino.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");

    private static void AgregarFiltros(DbCommand comando, EstadoOrden? estado, DateOnly? fecha)
    {
        AgregarParametro(comando, "@Estado", estado?.ToString());
        AgregarParametro(comando, "@Fecha", fecha?.ToDateTime(TimeOnly.MinValue));
        AgregarParametro(comando, "@FechaSiguiente",
            fecha.HasValue && fecha.Value < DateOnly.MaxValue
                ? fecha.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
                : null);
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
                    Servicio.Nombre AS ServicioNombre, Servicio.Descripcion AS ServicioDescripcion,
                    COALESCE(Orden.CostoServicio, Servicio.Costo) AS ServicioCosto,
                    Servicio.TiempoEstimadoHoras AS ServicioTiempo,
                    Detalle.Id AS DetalleId, Detalle.Cantidad, Detalle.PrecioUnitario, Detalle.Subtotal,
                    Producto.Codigo, Producto.Nombre
             FROM Ordenes AS Orden
             JOIN Vehiculos AS Vehiculo ON Vehiculo.Id = Orden.VehiculoId
             LEFT JOIN Clientes AS Cliente ON Cliente.Id = Vehiculo.ClienteId
             JOIN Mecanicos AS Mecanico ON Mecanico.Id = Orden.MecanicoId
             LEFT JOIN Servicios AS Servicio ON Servicio.Id = Orden.ServicioId
             LEFT JOIN DetalleOrdenes AS Detalle ON Detalle.OrdenId = Orden.Id
            LEFT JOIN Productos AS Producto ON Producto.Id = Detalle.ProductoId
            WHERE Orden.Id = @Id
            ORDER BY Detalle.Id;
            """;

        using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = consulta;
        AgregarParametro(comando, "@Id", id);
        OrdenDetalle detalle;
        var productos = new List<ProductoOrdenDetalle>();
        using (DbDataReader lector = comando.ExecuteReader())
        {
            if (!lector.Read()) return null;

            detalle = MapearDetalle(lector, productos.AsReadOnly());
            int indiceDetalle = lector.GetOrdinal("DetalleId");
            do
            {
                if (!lector.IsDBNull(indiceDetalle))
                    productos.Add(MapearProducto(lector));
            } while (lector.Read());
        }

        IReadOnlyList<ServicioOrdenDetalle> servicios = ObtenerServicios(id);
        if (servicios.Count == 0 && detalle.Servicio is not null)
            servicios = [detalle.Servicio];
        return detalle with
        {
            Servicios = servicios,
            Servicio = servicios.FirstOrDefault()
        };
    }

    private IReadOnlyList<ServicioOrdenDetalle> ObtenerServicios(int ordenId)
    {
        using DbConnection conexion = connectionFactory.CreateConnection();
        conexion.Open();
        using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT NombreServicio, DescripcionServicio, Costo, TiempoEstimadoHoras
            FROM DetalleServiciosOrdenes
            WHERE OrdenId = @Id
            ORDER BY Id;
            """;
        AgregarParametro(comando, "@Id", ordenId);
        using DbDataReader lector = comando.ExecuteReader();
        var servicios = new List<ServicioOrdenDetalle>();
        while (lector.Read())
        {
            servicios.Add(new ServicioOrdenDetalle
            {
                Nombre = lector.GetString(0),
                Descripcion = lector.GetString(1),
                Costo = lector.GetDecimal(2),
                TiempoEstimadoHoras = lector.GetDecimal(3)
            });
        }
        return servicios;
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
        Servicio = MapearServicio(lector),
        Productos = productos
    };

    private static ServicioOrdenDetalle? MapearServicio(DbDataReader lector)
    {
        int indice = lector.GetOrdinal("ServicioNombre");
        if (lector.IsDBNull(indice)) return null;
        return new ServicioOrdenDetalle
        {
            Nombre = lector.GetString(indice),
            Descripcion = LeerTextoOpcional(lector, "ServicioDescripcion") ?? string.Empty,
            Costo = lector.GetDecimal(lector.GetOrdinal("ServicioCosto")),
            TiempoEstimadoHoras = lector.GetDecimal(lector.GetOrdinal("ServicioTiempo"))
        };
    }

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
        var indiceServicio = lector.GetOrdinal(nameof(OrdenResumen.NombreServicio));

        return new OrdenResumen(
            lector.GetInt32(lector.GetOrdinal(nameof(OrdenResumen.Id))),
            lector.GetDateTime(lector.GetOrdinal(nameof(OrdenResumen.Fecha))),
            lector.GetString(lector.GetOrdinal(nameof(OrdenResumen.Placa))),
            lector.IsDBNull(indiceCliente) ? null : lector.GetString(indiceCliente),
            lector.GetString(lector.GetOrdinal(nameof(OrdenResumen.NombreMecanico))),
            lector.IsDBNull(indiceServicio) ? null : lector.GetString(indiceServicio),
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
