using System.Data.Common;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Data;

public class HistorialCostoServicioRepository
    : IHistorialCostoServicioRepository
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public HistorialCostoServicioRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<HistorialCostoServicio> GetAll()
    {
        List<HistorialCostoServicio> historial = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   ServicioId,
                   NombreServicio,
                   CostoAnterior,
                   CostoNuevo,
                   FechaCambio
            FROM HistorialCostoServicios
            ORDER BY FechaCambio DESC, Id DESC;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        using DbDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            historial.Add(MapHistorialCostoServicio(reader));
        }

        return historial;
    }

    private static HistorialCostoServicio MapHistorialCostoServicio(
        DbDataReader reader)
    {
        return new HistorialCostoServicio
        {
            Id = reader.GetInt32(0),
            ServicioId = reader.GetInt32(1),
            NombreServicio = reader.GetString(2),
            CostoAnterior = reader.GetDecimal(3),
            CostoNuevo = reader.GetDecimal(4),
            FechaCambio = reader.GetDateTime(5)
        };
    }
}
