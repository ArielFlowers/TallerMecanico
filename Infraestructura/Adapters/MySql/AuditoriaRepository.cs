
using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class AuditoriaRepository : IAuditoriaPort
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public AuditoriaRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Registrar(RegistroAuditoria registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Auditoria
            (
                UsuarioId,
                Username,
                Accion,
                Entidad,
                EntidadId,
                Fecha
            )
            VALUES
            (
                @UsuarioId,
                @Username,
                @Accion,
                @Entidad,
                @EntidadId,
                @Fecha
            );
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@UsuarioId", registro.UsuarioId);
        AddParameter(command, "@Username", registro.Username);
        AddParameter(command, "@Accion", registro.Accion);
        AddParameter(command, "@Entidad", registro.Entidad);
        AddParameter(command, "@EntidadId", registro.EntidadId);
        AddParameter(command, "@Fecha", registro.Fecha);

        command.ExecuteNonQuery();
    }

    public IReadOnlyList<RegistroAuditoria> ObtenerRecientes(
        int cantidad)
    {
        if (cantidad <= 0 || cantidad > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidad),
                "La cantidad debe estar entre 1 y 500.");
        }

        List<RegistroAuditoria> registros = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   UsuarioId,
                   Username,
                   Accion,
                   Entidad,
                   EntidadId,
                   Fecha
            FROM Auditoria
            ORDER BY Fecha DESC, Id DESC
            LIMIT @Cantidad;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@Cantidad", cantidad);

        using DbDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            registros.Add(MapRegistro(reader));
        }

        return registros;
    }

    private static RegistroAuditoria MapRegistro(
        DbDataReader reader)
    {
        int indiceUsuarioId =
            reader.GetOrdinal("UsuarioId");

        int indiceUsername =
            reader.GetOrdinal("Username");

        int indiceEntidadId =
            reader.GetOrdinal("EntidadId");

        return new RegistroAuditoria
        {
            Id = reader.GetInt64(
                reader.GetOrdinal("Id")),

            UsuarioId = reader.IsDBNull(indiceUsuarioId)
                ? null
                : reader.GetInt32(indiceUsuarioId),

            Username = reader.IsDBNull(indiceUsername)
                ? null
                : reader.GetString(indiceUsername),

            Accion = reader.GetString(
                reader.GetOrdinal("Accion")),

            Entidad = reader.GetString(
                reader.GetOrdinal("Entidad")),

            EntidadId = reader.IsDBNull(indiceEntidadId)
                ? null
                : reader.GetString(indiceEntidadId),

            Fecha = DateTime.SpecifyKind(
                reader.GetDateTime(
                    reader.GetOrdinal("Fecha")),
                DateTimeKind.Utc)
        };
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object? valor)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName = nombre;
        parameter.Value = valor ?? DBNull.Value;

        command.Parameters.Add(parameter);
    }
}
