using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Services;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class RenovacionTokenMySqlTests
{
    [Fact]
    public void RenovarToken_InvalidaAnteriorYRechazaSolicitudVerificada()
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

        byte[] tokenAnteriorBytes = RandomNumberGenerator.GetBytes(32);
        byte[] tokenNuevoBytes = RandomNumberGenerator.GetBytes(32);

        string tokenAnterior = Convert.ToHexString(tokenAnteriorBytes);
        string tokenNuevo = Convert.ToHexString(tokenNuevoBytes);

        string hashAnterior = Convert.ToHexString(
            SHA256.HashData(tokenAnteriorBytes));

        string hashNuevo = Convert.ToHexString(
            SHA256.HashData(tokenNuevoBytes));

        using DbConnection connection = factory.CreateConnection();
        connection.Open();

        try
        {
            const string insertar = """
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

            long solicitudId;

            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = insertar;

                Parametro(command, "@Username", username);
                Parametro(command, "@Email", username + "@example.com");
                Parametro(command, "@PasswordHash", "HASH_SOLO_PRUEBA");
                Parametro(command, "@TokenHash", hashAnterior);
                Parametro(command, "@Expiracion",
                    DateTime.UtcNow.AddMinutes(30));
                Parametro(command, "@FechaSolicitud", DateTime.UtcNow);

                solicitudId = Convert.ToInt64(
                    command.ExecuteScalar());
            }

            // Renovar el token de la solicitud pendiente.
            Assert.True(repositorio.IntentarRenovarTokenConLimite(
                solicitudId,
                hashNuevo,
                DateTime.UtcNow.AddHours(1)));

            // El token antiguo ya no puede verificar el correo.
            Assert.False(verificacion.ConfirmarEmail(tokenAnterior));

            // El nuevo token si permite verificarlo.
            Assert.True(verificacion.ConfirmarEmail(tokenNuevo));

            // Un token solo puede utilizarse una vez.
            Assert.False(verificacion.ConfirmarEmail(tokenNuevo));

            var solicitud = repositorio.ObtenerPorId(solicitudId);

            Assert.NotNull(solicitud);
            Assert.Equal("PendienteAprobacion", solicitud.Estado);
            Assert.NotNull(solicitud.EmailVerificadoEn);
            Assert.Null(solicitud.TokenVerificacionHash);

            // Una solicitud verificada no debe permitir renovacion.
            Assert.False(repositorio.IntentarRenovarTokenConLimite(
                solicitudId,
                hashAnterior,
                DateTime.UtcNow.AddHours(1)));
        }
        finally
        {
            // Eliminar exclusivamente la solicitud temporal.
            using DbCommand limpiar = connection.CreateCommand();

            limpiar.CommandText = """
                DELETE FROM SolicitudesRegistro
                WHERE Username = @Username
                  AND Email = @Email;
                """;

            Parametro(limpiar, "@Username", username);
            Parametro(limpiar, "@Email", username + "@example.com");

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