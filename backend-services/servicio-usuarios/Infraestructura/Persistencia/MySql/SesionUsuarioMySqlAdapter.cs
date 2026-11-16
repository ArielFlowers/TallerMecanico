using System.Data.Common;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

public sealed class SesionUsuarioMySqlAdapter : ISesionUsuarioPort
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public SesionUsuarioMySqlAdapter(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Iniciar(int usuarioId, DateTime venceEn)
    {
        const string query = """
            UPDATE Usuarios
            SET SesionVenceEn = @VenceEn
            WHERE Id = @Id;
            """;

        Ejecutar(query, usuarioId, venceEn);
    }

    public bool Renovar(int usuarioId, DateTime venceEn)
    {
        // Si la sesión ya venció (o se cerró), no se renueva.
        const string query = """
            UPDATE Usuarios
            SET SesionVenceEn = @VenceEn
            WHERE Id = @Id
              AND SesionVenceEn > UTC_TIMESTAMP(6);
            """;

        return Ejecutar(query, usuarioId, venceEn) == 1;
    }

    public void Cerrar(int usuarioId)
    {
        const string query = """
            UPDATE Usuarios
            SET SesionVenceEn = NULL
            WHERE Id = @Id;
            """;

        Ejecutar(query, usuarioId, null);
    }

    private int Ejecutar(string query, int usuarioId, DateTime? venceEn)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        AddParameter(command, "@Id", usuarioId);
        AddParameter(command, "@VenceEn", venceEn);

        return command.ExecuteNonQuery();
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object? valor)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = nombre;
        parameter.Value = valor ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
