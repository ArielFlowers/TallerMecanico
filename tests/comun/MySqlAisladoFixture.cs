using System.Diagnostics;
using MySqlConnector;

namespace TallerMecanico.IntegrationTests;

public sealed class MySqlAisladoFactAttribute : FactAttribute
{
    public MySqlAisladoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_TEST_MYSQL")))
            Skip = "Configura TALLER_TEST_MYSQL para crear una base aislada de pruebas.";
    }
}

public sealed class MySqlAisladoTheoryAttribute : TheoryAttribute
{
    public MySqlAisladoTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_TEST_MYSQL")))
            Skip = "Configura TALLER_TEST_MYSQL para crear una base aislada de pruebas.";
    }
}

[CollectionDefinition("MySQL aislado")]
public sealed class MySqlAisladoCollection : ICollectionFixture<MySqlAisladoFixture> { }

// La parte con la fábrica de conexiones de cada servicio está en MySqlAisladoFixture.Factory.cs de cada proyecto.
public sealed partial class MySqlAisladoFixture : IDisposable
{
    public static string RaizProyecto
    {
        get
        {
            for (var directorio = new DirectoryInfo(AppContext.BaseDirectory); directorio is not null; directorio = directorio.Parent)
                if (File.Exists(Path.Combine(directorio.FullName, "TallerMecanico.sln"))) return directorio.FullName;
            throw new DirectoryNotFoundException("No se encontró la raíz del proyecto.");
        }
    }
    public static string RaizFrontend => Path.Combine(RaizProyecto, "frontend-app");
    private readonly string? _servidor;
    private readonly string _base = $"autotaller_test_{Guid.NewGuid():N}";
    private bool _creada;
    private ServiciosEnPrueba? _servicios;
    public string CadenaConexion { get; private set; } = string.Empty;

    public MySqlAisladoFixture()
    {
        string? configurada = Environment.GetEnvironmentVariable("TALLER_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(configurada)) return;

        const string prefijoEsperado = "autotaller_test_";
        _base = $"{prefijoEsperado}{Guid.NewGuid():N}";

        if (string.IsNullOrWhiteSpace(_base) || _base.Length <= prefijoEsperado.Length)
        {
            throw new InvalidOperationException("Nombre de base de datos generado inválido o vacío.");
        }

        if (string.Equals(_base, "taller_mecanico", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Nombre de base de datos prohibido: 'taller_mecanico' está reservado para la aplicación principal.");
        }

        if (!_base.StartsWith(prefijoEsperado, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Nombre de base de datos no usa el prefijo obligatorio '{prefijoEsperado}'.");
        }

        var cadena = new MySqlConnectionStringBuilder(configurada) { Database = string.Empty, AllowUserVariables = true };
        string[] servidoresLocales = ["localhost", "127.0.0.1", "::1", "mysql", "mysql-test", "host.docker.internal"];
        if (!servidoresLocales.Contains(cadena.Server, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("TALLER_TEST_MYSQL debe apuntar a un MySQL local aislado, nunca a Aiven ni a un servidor remoto.");
        _servidor = cadena.ConnectionString;
        using var conexion = new MySqlConnection(_servidor);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"CREATE DATABASE `{_base}` CHARACTER SET utf8mb4;";
        comando.ExecuteNonQuery();
        _creada = true;
        cadena.Database = _base;
        CadenaConexion = cadena.ConnectionString;
        try { AppsPrueba.CrearEsquema(CadenaConexion); }
        catch { Dispose(); throw; }
    }

    // Levanta las APIs contra esta base y le pasa al Frontend sus URL y la clave interna.
    public void ConfigurarFrontend(ProcessStartInfo inicio)
    {
        _servicios ??= ServiciosEnPrueba.Iniciar(CadenaConexion);
        _servicios.Configurar(inicio);
    }

    // Guarda un usuario directamente: las pruebas web no usan los repositorios del servicio.
    public void AgregarUsuario(UsuarioSemilla usuario) => Ejecutar("""
        INSERT INTO Usuarios (Username, Email, PasswordHash, Rol, Activo, EmailVerificado, CreadoPor, FechaCreacion)
        VALUES (@Username, @Email, @PasswordHash, @Rol, @Activo, @EmailVerificado, @CreadoPor, @FechaCreacion);
        """,
        ("@Username", usuario.Username), ("@Email", usuario.Email), ("@PasswordHash", usuario.PasswordHash),
        ("@Rol", usuario.Rol), ("@Activo", usuario.Activo), ("@EmailVerificado", usuario.EmailVerificado),
        ("@CreadoPor", usuario.CreadoPor), ("@FechaCreacion", usuario.FechaCreacion));

    public long Ejecutar(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        using var conexion = new MySqlConnection(CadenaConexion);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        comando.ExecuteNonQuery();
        return comando.LastInsertedId;
    }

    public object? Escalar(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        using var conexion = new MySqlConnection(CadenaConexion);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        return comando.ExecuteScalar();
    }

    public void Dispose()
    {
        _servicios?.Dispose();
        if (!_creada) return;
        if (string.IsNullOrWhiteSpace(_base)) return;
        if (string.Equals(_base, "taller_mecanico", StringComparison.OrdinalIgnoreCase)) return;

        const string prefijoEsperado = "autotaller_test_";
        if (!_base.StartsWith(prefijoEsperado, StringComparison.Ordinal)) return;

        MySqlConnection.ClearAllPools();
        using var conexion = new MySqlConnection(_servidor);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"DROP DATABASE `{_base}`;";
        comando.ExecuteNonQuery();
        _creada = false;
    }
}

public sealed class UsuarioSemilla
{
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    // Sin activo y verificado, el login rechaza al usuario.
    public bool Activo { get; set; } = true;
    public bool EmailVerificado { get; set; } = true;
    public string CreadoPor { get; set; } = "pruebas";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
