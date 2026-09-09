using Microsoft.Data.Sqlite;
using TallerMecanico.Models;

namespace TallerMecanico.Data;

public class HistorialCostoServicioRepository
    : IHistorialCostoServicioRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public HistorialCostoServicioRepository(
        DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public IReadOnlyList<HistorialCostoServicio> GetAll()
    {
        List<HistorialCostoServicio> historial = [];

        using SqliteConnection connection =
            _databaseConnection.CreateConnection();

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

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            historial.Add(MapHistorialCostoServicio(reader));
        }

        return historial;
    }

    private static HistorialCostoServicio MapHistorialCostoServicio(
        SqliteDataReader reader)
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