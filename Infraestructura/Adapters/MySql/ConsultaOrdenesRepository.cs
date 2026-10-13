using System.Data.Common;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

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
