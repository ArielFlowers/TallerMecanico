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
if (usuario != "h2_recepcionista")
{
    throw new InvalidOperationException("Solo se permite la cuenta de prueba H2.");
}

var opciones = new Argon2idOptions();
configuracion.GetSection(Argon2idOptions.SectionName).Bind(opciones);
var hasher = new Argon2idPasswordHasher(Options.Create(opciones));
var repositorio = new UsuarioRepository(new MySqlConnectionFactory(configuracion));
var inicializador = new InicializadorUsuarios(repositorio, hasher);

if (repositorio.ExisteUsername(usuario))
{
    var existente = repositorio.ObtenerPorUsername(usuario);
    if (existente is null || !existente.Activo || existente.Rol != "Recepcionista" ||
        !hasher.VerificarPassword(password, existente.PasswordHash))
    {
        throw new InvalidOperationException("La cuenta existente no coincide; no se modificó.");
    }
    Console.WriteLine("Cuenta H2 existente verificada; sin cambios.");
}
else
{
    inicializador.CrearUsuarioInicial(usuario, password, "Recepcionista");
    Console.WriteLine("Cuenta Recepcionista H2 creada con el inicializador y Argon2id reales.");
}
