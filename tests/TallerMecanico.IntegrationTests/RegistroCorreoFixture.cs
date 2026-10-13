using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.IntegrationTests;

// Todos los puertos son dobles en memoria; no usan conexiones ni secretos locales.
internal sealed class RegistroCorreoFixture
{
    public UsuariosEnMemoria Usuarios { get; } = new();
    public SolicitudesEnMemoria Solicitudes { get; } = new();
    public CorreoSimulado Correo { get; } = new();

    public RegistroFacade CrearFachada(string? url = "https://localhost:7043")
    {
        return new RegistroFacade(
            new RegistroUsuarioService(Usuarios, Solicitudes, new HasherSimulado()),
            Correo, Configuracion(url));
    }

    public ReenvioVerificacionService CrearReenvio()
        => new(Solicitudes, Correo, Configuracion("https://localhost:7043"));

    private static IConfiguration Configuracion(string? url)
        => new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["App:PublicBaseUrl"] = url }).Build();

    internal sealed class CorreoSimulado : IEmailService
    {
        public Exception? ErrorConfiguracion { get; set; }
        public Exception? ErrorEnvio { get; set; }
        public Action? AntesDeEnviar { get; set; }
        public int Envios { get; private set; }

        public void ValidarConfiguracion()
        {
            if (ErrorConfiguracion is not null) throw ErrorConfiguracion;
        }

        public Task EnviarVerificacionAsync(
            string destinatario, string enlaceVerificacion,
            CancellationToken cancellationToken = default)
        {
            Envios++;
            AntesDeEnviar?.Invoke();
            if (ErrorEnvio is not null) throw ErrorEnvio;
            return Task.CompletedTask;
        }
    }

    internal sealed class UsuariosEnMemoria : IUsuarioPort
    {
        public bool UsernameExiste { get; set; }
        public int Consultas { get; private set; }
        public bool ExisteUsername(string username) { Consultas++; return UsernameExiste; }
        public bool ExisteEmail(string email) { Consultas++; return false; }
        public Usuario? ObtenerPorUsername(string username) => null;
        public Usuario? ObtenerPorId(int id) => null;
        public void Agregar(Usuario usuario) => throw new NotSupportedException();
        public void Actualizar(Usuario usuario) => throw new NotSupportedException();
    }

    internal sealed class SolicitudesEnMemoria : ISolicitudRegistroPort
    {
        public SolicitudRegistro? Guardada { get; private set; }
        public SolicitudRegistro? Pendiente { get; set; }
        public bool PermitirRenovacion { get; set; }
        public int Consultas { get; private set; }
        public int Renovaciones { get; private set; }
        public bool ExisteUsername(string username) { Consultas++; return false; }
        public bool ExisteEmail(string email) { Consultas++; return false; }
        public void Agregar(SolicitudRegistro solicitud) => Guardada = solicitud;
        public SolicitudRegistro? ObtenerPendientePorEmail(string email)
        {
            Consultas++;
            return Pendiente;
        }
        public bool IntentarRenovarTokenConLimite(long id, string hash, DateTime expira)
        {
            Renovaciones++;
            return PermitirRenovacion;
        }
        public SolicitudRegistro? ObtenerPorId(long id) => Guardada;
        public SolicitudRegistro? ObtenerPorTokenHash(string hash) => null;
        public bool ConfirmarEmail(string hash) => false;
        public void Actualizar(SolicitudRegistro solicitud) => throw new NotSupportedException();
    }

    private sealed class HasherSimulado : IPasswordHasher
    {
        public string GenerarHash(string password) => "HASH_SIMULADO";
        public bool VerificarPassword(string password, string hash) => false;
    }
}

internal sealed class LoggerEnMemoria<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Mensaje, Exception? Error)> Entradas { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => Entradas.Add((logLevel, formatter(state, exception), exception));
}
