using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Services;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class LimiteReenvioMySqlTests
{
    [Fact]
    public void Reenvio_BloqueaSegundoIntentoYRespetaEstado()
    {
        IConfiguration configuracion = new ConfigurationBuilder()
            .AddUserSecrets(
                typeof(UsuarioRepository).Assembly,
                optional: false)
            .Build();

        var factory = new MySqlConnectionFactory(configuracion);
        var repositorio = new SolicitudRegistroRepository(factory);
        var verificacion = new VerificacionEmailService(repositorio);

        string username = "it_" + Guid.NewGuid().ToString("N")[..20];
        string email = username + "@example.com";

        byte[] bytesOriginal = RandomNumberGenerator.GetBytes(32);
        byte[] bytesNuevo = RandomNumberGenerator.GetBytes(32);
        byte[] bytesSegundo = RandomNumberGenerator.GetBytes(32);

        string tokenOriginal = Convert.ToHexString(bytesOriginal);
        string tokenNuevo = Convert.ToHexString(bytesNuevo);

        string hashOriginal = Convert.ToHexString(
            SHA256.HashData(bytesOriginal));

        string hashNuevo = Convert.ToHexString(
            SHA256.HashData(bytesNuevo));

        string hashSegundo = Convert.ToHexString(
            SHA256.HashData(bytesSegundo));

        using DbConnection connection = factory.CreateConnection();
        connection.Open();

        try
        {
            long solicitudId;

            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO SolicitudesRegistro
                    (
                        Username, Email, PasswordHash, Estado,
                        TokenVerificacionHash, TokenExpiraEn,
                        FechaSolicitud
                    )
                    VALUES
                    (
                        @Username, @Email, @PasswordHash,
                        'PendienteVerificacion',
                        @TokenHash, @Expiracion, @FechaSolicitud
                    );
                    SELECT LAST_INSERT_ID();
                    """;

                Parametro(command, "@Username", username);
                Parametro(command, "@Email", email);
                Parametro(command, "@PasswordHash", "HASH_SOLO_PRUEBA");
                Parametro(command, "@TokenHash", hashOriginal);
                Parametro(command, "@Expiracion",
                    DateTime.UtcNow.AddMinutes(30));
                Parametro(command, "@FechaSolicitud", DateTime.UtcNow);

                solicitudId = Convert.ToInt64(command.ExecuteScalar());
            }

            // Primer reenvio permitido.
            Assert.True(repositorio.IntentarRenovarTokenConLimite(
                solicitudId,
                hashNuevo,
                DateTime.UtcNow.AddHours(1)));

            var actualizada = repositorio.ObtenerPorId(solicitudId);

            Assert.NotNull(actualizada);
            Assert.NotNull(actualizada.UltimoIntentoVerificacionEn);
            Assert.Equal(hashNuevo, actualizada.TokenVerificacionHash);

            // Segundo intento inmediato bloqueado.
            Assert.False(repositorio.IntentarRenovarTokenConLimite(
                solicitudId,
                hashSegundo,
                DateTime.UtcNow.AddHours(1)));

            var despuesDelBloqueo =
                repositorio.ObtenerPorId(solicitudId);

            Assert.NotNull(despuesDelBloqueo);
            Assert.Equal(
                hashNuevo,
                despuesDelBloqueo.TokenVerificacionHash);

            // El token original ha quedado invalidado.
            Assert.False(
                verificacion.ConfirmarEmail(tokenOriginal));

            // El nuevo token permite confirmar una sola vez.
            Assert.True(
                verificacion.ConfirmarEmail(tokenNuevo));

            Assert.False(
                verificacion.ConfirmarEmail(tokenNuevo));

            // No renovar solicitudes ya verificadas.
            Assert.False(repositorio.IntentarRenovarTokenConLimite(
                solicitudId,
                hashSegundo,
                DateTime.UtcNow.AddHours(1)));
        }
        finally
        {
            using DbCommand limpiar = connection.CreateCommand();

            limpiar.CommandText = """
                DELETE FROM SolicitudesRegistro
                WHERE Username = @Username
                  AND Email = @Email;
                """;

            Parametro(limpiar, "@Username", username);
            Parametro(limpiar, "@Email", email);

            limpiar.ExecuteNonQuery();
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