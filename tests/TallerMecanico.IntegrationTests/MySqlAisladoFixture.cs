using Microsoft.Extensions.Configuration;
using MySqlConnector;
using TallerMecanico.Data;
using TallerMecanico.Data.Factories;

namespace TallerMecanico.IntegrationTests;

public sealed class MySqlAisladoFactAttribute : FactAttribute
{
    public MySqlAisladoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_TEST_MYSQL")))
            Skip = "Configura TALLER_TEST_MYSQL para crear una base aislada de pruebas.";
    }
}

[CollectionDefinition("MySQL aislado")]
public sealed class MySqlAisladoCollection : ICollectionFixture<MySqlAisladoFixture> { }

public sealed class MySqlAisladoFixture : IDisposable
{
    public static string RaizProyecto
    {
        get
        {
            for (var directorio = new DirectoryInfo(AppContext.BaseDirectory); directorio is not null; directorio = directorio.Parent)
                if (File.Exists(Path.Combine(directorio.FullName, "TallerMecanico.csproj"))) return directorio.FullName;
            throw new DirectoryNotFoundException("No se encontró la raíz del proyecto.");
        }
    }
    private readonly string? _servidor;
    private readonly string _base = $"autotaller_test_{Guid.NewGuid():N}";
    private bool _creada;
    public string CadenaConexion { get; private set; } = string.Empty;
    public DatabaseConnectionFactory Factory { get; private set; } = null!;

    public MySqlAisladoFixture()
    {
        string? configurada = Environment.GetEnvironmentVariable("TALLER_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(configurada)) return;
        var cadena = new MySqlConnectionStringBuilder(configurada) { Database = string.Empty, AllowUserVariables = true };
        _servidor = cadena.ConnectionString;
        using var conexion = new MySqlConnection(_servidor);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"CREATE DATABASE `{_base}` CHARACTER SET utf8mb4;";
        comando.ExecuteNonQuery();
        _creada = true;
        cadena.Database = _base;
        CadenaConexion = cadena.ConnectionString;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MySqlConnection"] = CadenaConexion
        }).Build();
        Factory = new MySqlConnectionFactory(config);
        try { new DatabaseInitializer(Factory).Initialize(); }
        catch { Dispose(); throw; }
    }

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
        if (!_creada) return;
        MySqlConnection.ClearAllPools();
        using var conexion = new MySqlConnection(_servidor);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"DROP DATABASE `{_base}`;";
        comando.ExecuteNonQuery();
        _creada = false;
    }
}
