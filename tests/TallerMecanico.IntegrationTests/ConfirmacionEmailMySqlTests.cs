using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public class ConfirmacionEmailMySqlTests
{
    [Fact]
    public void ConfirmarEmail_ValidaVencimientoYUnSoloUso()
    {
        IConfiguration configuracion = new ConfigurationBuilder()
            .AddUserSecrets(
                typeof(UsuarioRepository).Assembly,
                optional: false)
            .Build();

        var factory = new MySqlConnectionFactory(configuracion);
        var repositorio = new SolicitudRegistroRepository(factory);

        string identificador = Guid.NewGuid().ToString("N");
        string username1 = "it_" + identificador[..12];
        string username2 = "it_" + identificador[12..24];

        string tokenVigente = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        string tokenVencido = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        long? idVigente = null;
        long? idVencido = null;

        using DbConnection connection = factory.CreateConnection();
        connection.Open();

        try
        {
            idVigente = CrearSolicitud(
                connection,
                username1,
                tokenVigente,
                DateTime.UtcNow.AddMinutes(30));

            idVencido = CrearSolicitud(
                connection,
                username2,
                tokenVencido,
                DateTime.UtcNow.AddMinutes(-30));

            Assert.True(repositorio.ConfirmarEmail(tokenVigente));
            Assert.False(repositorio.ConfirmarEmail(tokenVigente));

            Assert.False(repositorio.ConfirmarEmail(tokenVencido));

            string tokenInexistente = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            Assert.False(
                repositorio.ConfirmarEmail(tokenInexistente));

            var verificada = repositorio.ObtenerPorId(idVigente.Value);
            var expirada = repositorio.ObtenerPorId(idVencido.Value);

            Assert.NotNull(verificada);
            Assert.NotNull(expirada);

            Assert.Equal("PendienteAprobacion", verificada.Estado);
            Assert.NotNull(verificada.EmailVerificadoEn);
            Assert.Null(verificada.TokenVerificacionHash);

            Assert.Equal("PendienteVerificacion", expirada.Estado);
            Assert.Null(expirada.EmailVerificadoEn);
        }
        finally
        {
            if (idVigente.HasValue)
            {
                EliminarSolicitud(connection, idVigente.Value, username1);
            }

            if (idVencido.HasValue)
            {
                EliminarSolicitud(connection, idVencido.Value, username2);
            }
        }
    }

    private static long CrearSolicitud(
        DbConnection connection,
        string username,
        string token,
        DateTime expiracion)
    {
        const string sql = """
            INSERT INTO SolicitudesRegistro
            (
                Username, Email, PasswordHash, Estado,
                TokenVerificacionHash, TokenExpiraEn, FechaSolicitud
            )
            VALUES
            (
                @Username, @Email, @PasswordHash,
                'PendienteVerificacion',
                @TokenHash, @TokenExpiraEn, @FechaSolicitud
            );
            SELECT LAST_INSERT_ID();
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        Parametro(command, "@Username", username);
        Parametro(command, "@Email", username + "@example.com");
        Parametro(command, "@PasswordHash", "HASH_SOLO_PRUEBA");
        Parametro(command, "@TokenHash", token);
        Parametro(command, "@TokenExpiraEn", expiracion);
        Parametro(command, "@FechaSolicitud", DateTime.UtcNow);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void EliminarSolicitud(
        DbConnection connection,
        long id,
        string username)
    {
        const string sql = """
            DELETE FROM SolicitudesRegistro
            WHERE Id = @Id
              AND Username = @Username;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        Parametro(command, "@Id", id);
        Parametro(command, "@Username", username);

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException(
                "No se pudo limpiar la solicitud de prueba.");
        }
    }

    private static void Parametro(
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