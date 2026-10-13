using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class RevisionSolicitudesRepository : IRevisionSolicitudesPort
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public RevisionSolicitudesRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<SolicitudRegistro> ObtenerPendientes()
    {
        var resultado = new List<SolicitudRegistro>();

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbCommand command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, Username, Email, Estado,
                   FechaSolicitud, EmailVerificadoEn
            FROM SolicitudesRegistro
            WHERE Estado = 'PendienteAprobacion'
              AND EmailVerificadoEn IS NOT NULL
            ORDER BY FechaSolicitud ASC;
            """;

        using DbDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            resultado.Add(new SolicitudRegistro
            {
                Id = reader.GetInt64(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                Estado = reader.GetString(3),
                FechaSolicitud = reader.GetDateTime(4),
                EmailVerificadoEn = reader.GetDateTime(5)
            });
        }

        return resultado;
    }

    public bool ResolverSolicitud(
        long solicitudId,
        int administradorId,
        bool aprobar)
    {
        if (solicitudId <= 0 || administradorId <= 0)
        {
            return false;
        }

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbTransaction transaction =
            connection.BeginTransaction();

        try
        {
            string? administrador = ObtenerAdministrador(
                connection, transaction, administradorId);

            if (administrador is null)
            {
                transaction.Rollback();
                return false;
            }

            using DbCommand consulta = connection.CreateCommand();
            consulta.Transaction = transaction;

            consulta.CommandText = """
                SELECT Username, Email, PasswordHash
                FROM SolicitudesRegistro
                WHERE Id = @Id
                  AND Estado = 'PendienteAprobacion'
                  AND EmailVerificadoEn IS NOT NULL
                FOR UPDATE;
                """;

            AgregarParametro(consulta, "@Id", solicitudId);

            string username = string.Empty;
            string email = string.Empty;
            string passwordHash = string.Empty;
            bool encontrada;

            using (DbDataReader reader = consulta.ExecuteReader())
            {
                encontrada = reader.Read();

                if (encontrada)
                {
                    username = reader.GetString(0);
                    email = reader.GetString(1);
                    passwordHash = reader.GetString(2);
                }
            }

            // El lector ya esta cerrado antes de realizar Rollback.
            if (!encontrada)
            {
                transaction.Rollback();
                return false;
            }

            if (aprobar)
            {
                using DbCommand insertar = connection.CreateCommand();
                insertar.Transaction = transaction;

                insertar.CommandText = """
                    INSERT INTO Usuarios
                    (
                        Username,
                        PasswordHash,
                        Email,
                        EmailVerificado,
                        Rol,
                        Activo,
                        CreadoPor,
                        FechaCreacion
                    )
                    VALUES
                    (
                        @Username,
                        @PasswordHash,
                        @Email,
                        1,
                        'Recepcionista',
                        1,
                        @CreadoPor,
                        UTC_TIMESTAMP()
                    );
                    """;

                AgregarParametro(insertar, "@Username", username);
                AgregarParametro(insertar, "@PasswordHash", passwordHash);
                AgregarParametro(insertar, "@Email", email);
                AgregarParametro(insertar, "@CreadoPor", administrador);

                if (insertar.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException(
                        "No se pudo crear el usuario aprobado.");
                }
            }

            using DbCommand actualizar = connection.CreateCommand();
            actualizar.Transaction = transaction;

            actualizar.CommandText = """
                UPDATE SolicitudesRegistro
                SET Estado = @NuevoEstado,
                    FechaResolucion = UTC_TIMESTAMP(6),
                    RevisadoPorUsuarioId = @AdministradorId
                WHERE Id = @SolicitudId
                  AND Estado = 'PendienteAprobacion'
                  AND EmailVerificadoEn IS NOT NULL;
                """;

            AgregarParametro(
                actualizar,
                "@NuevoEstado",
                aprobar ? "Aprobada" : "Rechazada");

            AgregarParametro(
                actualizar,
                "@AdministradorId",
                administradorId);

            AgregarParametro(
                actualizar,
                "@SolicitudId",
                solicitudId);

            if (actualizar.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException(
                    "No se pudo resolver la solicitud.");
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static string? ObtenerAdministrador(
        DbConnection connection,
        DbTransaction transaction,
        int administradorId)
    {
        using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = """
            SELECT Username
            FROM Usuarios
            WHERE Id = @AdministradorId
              AND Rol = 'Administrador'
              AND Activo = 1
            FOR UPDATE;
            """;

        AgregarParametro(
            command, "@AdministradorId", administradorId);

        return command.ExecuteScalar() as string;
    }

    private static void AgregarParametro(
        DbCommand command,
        string nombre,
        object valor)
    {
        DbParameter parametro = command.CreateParameter();

        parametro.ParameterName = nombre;
        parametro.Value = valor;

        command.Parameters.Add(parametro);
    }
}