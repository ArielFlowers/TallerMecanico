using System.Data.Common;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class RevisionSolicitudesMySqlTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolverSolicitud_ApruebaORechazaCorrectamente(bool aprobar)
    {
        var factory = CrearFactory();
        var repositorio = new RevisionSolicitudesRepository(factory);

        using DbConnection conexion = factory.CreateConnection();
        conexion.Open();

        int administradorId = ObtenerAdministrador(conexion);
        string usuario = "it_" + Guid.NewGuid().ToString("N")[..16];
        string email = usuario + "@example.com";
        long? solicitudId = null;

        try
        {
            solicitudId = CrearSolicitud(
                conexion, usuario, email, verificada: true);

            bool resultado = repositorio.ResolverSolicitud(
                solicitudId.Value, administradorId, aprobar);

            Assert.True(resultado);

            string estado = Convert.ToString(Escalar(
                conexion,
                "SELECT Estado FROM SolicitudesRegistro WHERE Id = @Id",
                ("@Id", solicitudId.Value)))!;

            Assert.Equal(
                aprobar ? "Aprobada" : "Rechazada",
                estado);

            long cantidadUsuarios = Convert.ToInt64(Escalar(
                conexion,
                """
                SELECT COUNT(*) FROM Usuarios
                WHERE Username = @Username AND Email = @Email
                """,
                ("@Username", usuario),
                ("@Email", email)));

            Assert.Equal(aprobar ? 1L : 0L, cantidadUsuarios);

            if (aprobar)
            {
                string rol = Convert.ToString(Escalar(
                    conexion,
                    """
                    SELECT Rol FROM Usuarios
                    WHERE Username = @Username AND Email = @Email
                    """,
                    ("@Username", usuario),
                    ("@Email", email)))!;

                Assert.Equal("Recepcionista", rol);

                long habilitado = Convert.ToInt64(Escalar(
                    conexion,
                    """
                    SELECT COUNT(*) FROM Usuarios
                    WHERE Username = @Username
                      AND Email = @Email
                      AND Activo = 1
                      AND EmailVerificado = 1
                    """,
                    ("@Username", usuario),
                    ("@Email", email)));

                Assert.Equal(1L, habilitado);
            }

            Assert.False(repositorio.ResolverSolicitud(
                solicitudId.Value, administradorId, aprobar));
        }
        finally
        {
            Limpiar(conexion, solicitudId, usuario, email);
        }
    }

    [Fact]
    public void SolicitudSinVerificar_NoPuedeAprobarse()
    {
        var factory = CrearFactory();
        var repositorio = new RevisionSolicitudesRepository(factory);

        using DbConnection conexion = factory.CreateConnection();
        conexion.Open();

        int administradorId = ObtenerAdministrador(conexion);
        string usuario = "it_" + Guid.NewGuid().ToString("N")[..16];
        string email = usuario + "@example.com";
        long? solicitudId = null;

        try
        {
            solicitudId = CrearSolicitud(
                conexion, usuario, email, verificada: false);

            Assert.False(repositorio.ResolverSolicitud(
                solicitudId.Value, administradorId, aprobar: true));

            long cantidadUsuarios = Convert.ToInt64(Escalar(
                conexion,
                """
                SELECT COUNT(*) FROM Usuarios
                WHERE Username = @Username AND Email = @Email
                """,
                ("@Username", usuario),
                ("@Email", email)));

            Assert.Equal(0L, cantidadUsuarios);
        }
        finally
        {
            Limpiar(conexion, solicitudId, usuario, email);
        }
    }

    [Fact]
    public void ConflictoDeUsuario_RevierteLaAprobacion()
    {
        var factory = CrearFactory();
        var repositorio = new RevisionSolicitudesRepository(factory);

        using DbConnection conexion = factory.CreateConnection();
        conexion.Open();

        int administradorId = ObtenerAdministrador(conexion);
        string usuario = "it_" + Guid.NewGuid().ToString("N")[..16];
        string email = usuario + "@example.com";
        long? solicitudId = null;

        try
        {
            solicitudId = CrearSolicitud(
                conexion, usuario, email, verificada: true);

            // Simulamos un usuario creado antes de aprobar la solicitud.
            Ejecutar(
                conexion,
                """
                INSERT INTO Usuarios
                (Username, PasswordHash, Email, EmailVerificado,
                 Rol, Activo, CreadoPor, FechaCreacion)
                VALUES
                (@Username, @PasswordHash, @Email, 1,
                 'Recepcionista', 1, 'prueba', UTC_TIMESTAMP())
                """,
                ("@Username", usuario),
                ("@PasswordHash", "HASH_TEMPORAL_SOLO_PRUEBAS"),
                ("@Email", email));

            // La aprobacion intenta insertar un usuario duplicado.
            Assert.ThrowsAny<DbException>(() =>
            {
                repositorio.ResolverSolicitud(
                    solicitudId.Value, administradorId, aprobar: true);
            });

            // La solicitud debe permanecer pendiente.
            string estado = Convert.ToString(Escalar(
                conexion,
                """
                SELECT Estado FROM SolicitudesRegistro
                WHERE Id = @Id
                """,
                ("@Id", solicitudId.Value)))!;

            Assert.Equal("PendienteAprobacion", estado);

            // No debe registrarse una resolucion fallida.
            long resoluciones = Convert.ToInt64(Escalar(
                conexion,
                """
                SELECT COUNT(*) FROM SolicitudesRegistro
                WHERE Id = @Id
                  AND FechaResolucion IS NOT NULL
                """,
                ("@Id", solicitudId.Value)));

            Assert.Equal(0L, resoluciones);

            // Debe seguir existiendo solamente el usuario original.
            long usuarios = Convert.ToInt64(Escalar(
                conexion,
                """
                SELECT COUNT(*) FROM Usuarios
                WHERE Username = @Username
                  AND Email = @Email
                """,
                ("@Username", usuario),
                ("@Email", email)));

            Assert.Equal(1L, usuarios);
        }
        finally
        {
            Limpiar(conexion, solicitudId, usuario, email);
        }
    }
    private static MySqlConnectionFactory CrearFactory()
    {
        IConfiguration configuracion = new ConfigurationBuilder()
            .AddUserSecrets(
                typeof(UsuarioRepository).Assembly,
                optional: false)
            .Build();

        return new MySqlConnectionFactory(configuracion);
    }

    private static int ObtenerAdministrador(DbConnection conexion)
    {
        object? resultado = Escalar(
            conexion,
            """
            SELECT Id FROM Usuarios
            WHERE Rol = 'Administrador' AND Activo = 1
            ORDER BY Id LIMIT 1
            """);

        if (resultado is null || resultado == DBNull.Value)
        {
            throw new InvalidOperationException(
                "No hay un administrador activo para la prueba.");
        }

        return Convert.ToInt32(resultado);
    }

    private static long CrearSolicitud(
        DbConnection conexion,
        string usuario,
        string email,
        bool verificada)
    {
        const string sql = """
            INSERT INTO SolicitudesRegistro
            (
                Username, Email, PasswordHash,
                Estado, EmailVerificadoEn, FechaSolicitud
            )
            VALUES
            (
                @Username, @Email, @PasswordHash,
                @Estado, @EmailVerificadoEn, UTC_TIMESTAMP(6)
            );
            SELECT LAST_INSERT_ID();
            """;

        object fecha = verificada
            ? DateTime.UtcNow
            : DBNull.Value;

        object? resultado = Escalar(
            conexion,
            sql,
            ("@Username", usuario),
            ("@Email", email),
            ("@PasswordHash", "HASH_TEMPORAL_SOLO_PRUEBAS"),
            ("@Estado", verificada
                ? "PendienteAprobacion"
                : "PendienteVerificacion"),
            ("@EmailVerificadoEn", fecha));

        return Convert.ToInt64(resultado);
    }

    private static void Limpiar(
        DbConnection conexion,
        long? solicitudId,
        string usuario,
        string email)
    {
        // Solo elimina cuentas con el identificador aleatorio de esta prueba.
        Ejecutar(
            conexion,
            """
            DELETE FROM Usuarios
            WHERE Username = @Username AND Email = @Email
            """,
            ("@Username", usuario),
            ("@Email", email));

        if (solicitudId.HasValue)
        {
            Ejecutar(
                conexion,
                """
                DELETE FROM SolicitudesRegistro
                WHERE Id = @Id
                  AND Username = @Username
                  AND Email = @Email
                """,
                ("@Id", solicitudId.Value),
                ("@Username", usuario),
                ("@Email", email));
        }
    }

    private static object? Escalar(
        DbConnection conexion,
        string sql,
        params (string Nombre, object Valor)[] parametros)
    {
        using DbCommand comando = CrearComando(
            conexion, sql, parametros);

        return comando.ExecuteScalar();
    }

    private static void Ejecutar(
        DbConnection conexion,
        string sql,
        params (string Nombre, object Valor)[] parametros)
    {
        using DbCommand comando = CrearComando(
            conexion, sql, parametros);

        comando.ExecuteNonQuery();
    }

    private static DbCommand CrearComando(
        DbConnection conexion,
        string sql,
        params (string Nombre, object Valor)[] parametros)
    {
        DbCommand comando = conexion.CreateCommand();
        comando.CommandText = sql;

        foreach (var (nombre, valor) in parametros)
        {
            DbParameter parametro = comando.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor;
            comando.Parameters.Add(parametro);
        }

        return comando;
    }
}