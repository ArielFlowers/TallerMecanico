using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

var configuracion = new ConfigurationBuilder()
    .AddJsonFile("/app/appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var conexion = new MySqlConnectionStringBuilder(
    configuracion.GetConnectionString("MySqlConnection")
    ?? throw new InvalidOperationException("Falta la conexión H2 explícita."));

if (conexion.Server != "mysql" ||
    conexion.Database != "autotaller_h2_integracion" ||
    conexion.UserID != "h2_app" ||
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
{
    throw new InvalidOperationException("Se permite provisionar solo el MySQL aislado H2.");
}

string usuario = Console.ReadLine() ?? string.Empty;
string password = Console.ReadLine() ?? string.Empty;
string rol = usuario switch
{
    "h2_recepcionista" => "Recepcionista",
    "h2_administrador" => "Administrador",
    _ => throw new InvalidOperationException("Solo se permiten las cuentas aisladas H2.")
};
if (password.Length < 12)
{
    throw new InvalidOperationException("Falta una contraseña exclusiva de pruebas.");
}

var opciones = new Argon2idOptions();
configuracion.GetSection(Argon2idOptions.SectionName).Bind(opciones);
var hasher = new Argon2idPasswordHasher(Options.Create(opciones));
var repositorio = new UsuarioRepository(new MySqlConnectionFactory(configuracion));
var inicializador = new InicializadorUsuarios(repositorio, hasher);

if (repositorio.ExisteUsername(usuario))
{
    var existente = repositorio.ObtenerPorUsername(usuario);
    if (existente is null || !existente.Activo || existente.Rol != rol ||
        !hasher.VerificarPassword(password, existente.PasswordHash))
    {
        throw new InvalidOperationException("La cuenta existente no coincide; no se modificó.");
    }
    Console.WriteLine("Cuenta H2 existente verificada; sin cambios.");
}
else
{
    inicializador.CrearUsuarioInicial(usuario, password, rol);
    Console.WriteLine($"Cuenta {rol} H2 creada con el inicializador y Argon2id reales.");
}
