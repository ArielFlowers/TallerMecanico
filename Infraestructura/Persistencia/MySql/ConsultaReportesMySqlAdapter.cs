using System.Data.Common;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

public sealed class ConsultaReportesMySqlAdapter(
    DatabaseConnectionFactory connectionFactory) : IConsultaReportesPort
{
    public DocumentoReporteOrdenes ObtenerReporteOrdenes(
        FiltroReporteOrdenes filtro,
        string generadoPor)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        DateTime fechaGeneracion = DateTime.UtcNow;

        using DbConnection conexion = connectionFactory.CreateConnection();
        conexion.Open();

        DateTime? fechaHastaSiguiente = null;
        if (filtro.FechaHasta.HasValue)
        {
            // El límite superior es exclusivo para incluir todo el día
            // indicado en FechaHasta.
            fechaHastaSiguiente = filtro.FechaHasta.Value
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue);
        }

        const string consulta = """
            SELECT Orden.Id AS OrdenId,
                   Orden.Fecha,
                   Vehiculo.Placa,
                   NULLIF(CONCAT_WS(' ', Cliente.Nombres,
                       Cliente.PrimerApellido, Cliente.SegundoApellido), '')
                       AS NombreCliente,
                   CONCAT_WS(' ', Mecanico.Nombres,
                       Mecanico.PrimerApellido, Mecanico.SegundoApellido)
                       AS NombreMecanico,
                   COALESCE((
                       SELECT GROUP_CONCAT(
                           DetalleServicio.NombreServicio
                           ORDER BY DetalleServicio.Id SEPARATOR ', ')
                       FROM DetalleServiciosOrdenes AS DetalleServicio
                       WHERE DetalleServicio.OrdenId = Orden.Id
                   ), Servicio.Nombre) AS NombreServicio,
                   Orden.Total,
                   Orden.Estado,
                   Orden.CreadoPor
            FROM Ordenes AS Orden
            JOIN Vehiculos AS Vehiculo ON Vehiculo.Id = Orden.VehiculoId
            LEFT JOIN Clientes AS Cliente ON Cliente.Id = Vehiculo.ClienteId
            JOIN Mecanicos AS Mecanico ON Mecanico.Id = Orden.MecanicoId
            LEFT JOIN Servicios AS Servicio ON Servicio.Id = Orden.ServicioId
            WHERE (@FechaDesde IS NULL OR Orden.Fecha >= @FechaDesde)
              AND (@FechaHastaSiguiente IS NULL OR Orden.Fecha < @FechaHastaSiguiente)
              AND (@Estado IS NULL OR Orden.Estado = @Estado)
            ORDER BY Orden.Fecha DESC, Orden.Id DESC;
            """;

        using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = consulta;
        AgregarParametro(comando, "@FechaDesde",
            filtro.FechaDesde?.ToDateTime(TimeOnly.MinValue));
        AgregarParametro(comando, "@FechaHastaSiguiente", fechaHastaSiguiente);
        AgregarParametro(comando, "@Estado",
            string.IsNullOrWhiteSpace(filtro.Estado) ? null : filtro.Estado.Trim());

        var filas = new List<FilaReporteOrden>();
        int ordenesActivas = 0;
        int ordenesAnuladas = 0;
        decimal totalGeneral = 0;

        using DbDataReader lector = comando.ExecuteReader();
        while (lector.Read())
        {
            var fila = new FilaReporteOrden
            {
                OrdenId = lector.GetInt32(lector.GetOrdinal(nameof(FilaReporteOrden.OrdenId))),
                Fecha = lector.GetDateTime(lector.GetOrdinal(nameof(FilaReporteOrden.Fecha))),
                Placa = lector.GetString(lector.GetOrdinal(nameof(FilaReporteOrden.Placa))),
                NombreCliente = LeerTextoOpcional(lector, nameof(FilaReporteOrden.NombreCliente)),
                NombreMecanico = lector.GetString(
                    lector.GetOrdinal(nameof(FilaReporteOrden.NombreMecanico))),
                NombreServicio = LeerTextoOpcional(lector, nameof(FilaReporteOrden.NombreServicio)),
                Total = lector.GetDecimal(lector.GetOrdinal(nameof(FilaReporteOrden.Total))),
                Estado = lector.GetString(lector.GetOrdinal(nameof(FilaReporteOrden.Estado))),
                CreadoPor = lector.GetString(lector.GetOrdinal(nameof(FilaReporteOrden.CreadoPor)))
            };

            if (string.Equals(fila.Estado, "Activa", StringComparison.OrdinalIgnoreCase))
            {
                ordenesActivas++;
                totalGeneral += fila.Total;
            }
            else if (string.Equals(fila.Estado, "Anulada", StringComparison.OrdinalIgnoreCase))
            {
                ordenesAnuladas++;
            }

            filas.Add(fila);
        }

        var documento = new DocumentoReporteOrdenes
        {
            Filtro = filtro,
            Filas = filas,
            Resumen = new ResumenReporteOrdenes
            {
                TotalOrdenes = filas.Count,
                TotalGeneral = totalGeneral,
                OrdenesActivas = ordenesActivas,
                OrdenesAnuladas = ordenesAnuladas
            },
            FechaGeneracion = fechaGeneracion,
            GeneradoPor = string.IsNullOrWhiteSpace(generadoPor) ? "sistema" : generadoPor
        };

        return documento;
    }

    public DocumentoReporteMecanicos ObtenerReporteMecanicos(
        FiltroReporteMecanicos filtro,
        string generadoPor)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        DateTime fechaGeneracion = DateTime.UtcNow;

        DateTime? fechaHastaSiguiente = null;
        if (filtro.FechaHasta.HasValue)
        {
            fechaHastaSiguiente = filtro.FechaHasta.Value
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue);
        }

        // LEFT JOIN sobre Ordenes para incluir mecánicos sin órdenes
        // dentro del rango filtrado y tratar la relación como opcional.
        const string consulta = """
            SELECT Mecanico.Id AS MecanicoId,
                   CONCAT_WS(' ', Mecanico.Nombres,
                       Mecanico.PrimerApellido, Mecanico.SegundoApellido)
                       AS NombreMecanico,
                   Mecanico.Ci AS CiMecanico,
                   COUNT(Orden.Id) AS TotalOrdenes,
                   COALESCE(SUM(Orden.Total), 0) AS TotalFacturado
            FROM Mecanicos AS Mecanico
            LEFT JOIN Ordenes AS Orden ON Orden.MecanicoId = Mecanico.Id
                AND Orden.Estado = 'Activa'
                AND (@FechaDesde IS NULL OR Orden.Fecha >= @FechaDesde)
                AND (@FechaHastaSiguiente IS NULL OR Orden.Fecha < @FechaHastaSiguiente)
            WHERE (@MecanicoId IS NULL OR Mecanico.Id = @MecanicoId)
            GROUP BY Mecanico.Id, Mecanico.Nombres,
                     Mecanico.PrimerApellido, Mecanico.SegundoApellido, Mecanico.Ci
            ORDER BY TotalFacturado DESC, Mecanico.Id;
            """;

        using DbConnection conexion = connectionFactory.CreateConnection();
        conexion.Open();

        using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = consulta;
        AgregarParametro(comando, "@FechaDesde",
            filtro.FechaDesde?.ToDateTime(TimeOnly.MinValue));
        AgregarParametro(comando, "@FechaHastaSiguiente", fechaHastaSiguiente);
        AgregarParametro(comando, "@MecanicoId", filtro.MecanicoId);

        var filas = new List<FilaReporteMecanico>();
        int totalOrdenes = 0;
        decimal totalFacturado = 0;

        using DbDataReader lector = comando.ExecuteReader();
        while (lector.Read())
        {
            int totalOrdenesMecanico = lector.GetInt32(
                lector.GetOrdinal(nameof(FilaReporteMecanico.TotalOrdenes)));
            decimal totalFacturadoMecanico = lector.GetDecimal(
                lector.GetOrdinal(nameof(FilaReporteMecanico.TotalFacturado)));

            var fila = new FilaReporteMecanico
            {
                MecanicoId = lector.GetInt32(lector.GetOrdinal(nameof(FilaReporteMecanico.MecanicoId))),
                NombreMecanico = lector.GetString(
                    lector.GetOrdinal(nameof(FilaReporteMecanico.NombreMecanico))),
                CiMecanico = LeerTextoOpcional(lector, nameof(FilaReporteMecanico.CiMecanico)) ?? string.Empty,
                TotalOrdenes = totalOrdenesMecanico,
                TotalFacturado = totalFacturadoMecanico,
                PromedioPorOrden = totalOrdenesMecanico > 0
                    ? totalFacturadoMecanico / totalOrdenesMecanico
                    : 0
            };

            totalOrdenes += totalOrdenesMecanico;
            totalFacturado += totalFacturadoMecanico;
            filas.Add(fila);
        }

        var documento = new DocumentoReporteMecanicos
        {
            Filtro = filtro,
            Filas = filas,
            Resumen = new ResumenReporteMecanicos
            {
                TotalMecanicos = filas.Count,
                TotalOrdenes = totalOrdenes,
                TotalFacturado = totalFacturado
            },
            FechaGeneracion = fechaGeneracion,
            GeneradoPor = string.IsNullOrWhiteSpace(generadoPor) ? "sistema" : generadoPor
        };

        return documento;
    }

    public IReadOnlyList<MecanicoListaDto> ObtenerMecanicos()
    {
        using DbConnection conexion = connectionFactory.CreateConnection();
        conexion.Open();

        const string consulta = """
            SELECT Id,
                   CONCAT_WS(' ', Nombres, PrimerApellido, SegundoApellido) AS Nombre
            FROM Mecanicos
            ORDER BY Nombre, Id;
            """;

        using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = consulta;

        var lista = new List<MecanicoListaDto>();
        using DbDataReader lector = comando.ExecuteReader();
        while (lector.Read())
        {
            lista.Add(new MecanicoListaDto
            {
                Id = lector.GetInt32(lector.GetOrdinal(nameof(MecanicoListaDto.Id))),
                Nombre = lector.GetString(lector.GetOrdinal(nameof(MecanicoListaDto.Nombre)))
            });
        }
        return lista;
    }

    private static string? LeerTextoOpcional(DbDataReader lector, string columna)
    {
        int indice = lector.GetOrdinal(columna);
        return lector.IsDBNull(indice) ? null : lector.GetString(indice);
    }

    private static void AgregarParametro(
        DbCommand comando,
        string nombre,
        object? valor)
    {
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = valor ?? DBNull.Value;
        comando.Parameters.Add(parametro);
    }
}
