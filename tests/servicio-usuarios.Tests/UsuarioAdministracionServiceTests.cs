
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Infraestructura.Seguridad;
using TallerMecanico.Aplicacion.Validaciones;
using Xunit;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public class UsuarioAdministracionServiceTests
{
    private readonly UsuariosFalsos _usuarios;
    private readonly SolicitudesFalsas _solicitudes;
    private readonly EmailFalso _email;
    private readonly ConfigurationFalsa _configuration;
    private readonly AuditoriaFalsa _auditoria;
    private readonly CurrentUserFalso _currentUser;
    private readonly HasherFalso _hasher;
    private readonly IActivacionInvitacionPort _activacionInvitacion;
    private readonly IGeneradorNombreUsuarioPort _generador;
    private readonly UsuarioAdministracionService _service;

    public UsuarioAdministracionServiceTests()
    {
        _usuarios = new UsuariosFalsos();
        _solicitudes = new SolicitudesFalsas();
        _email = new EmailFalso();
        _configuration = new ConfigurationFalsa();
        _auditoria = new AuditoriaFalsa();
        _currentUser = new CurrentUserFalso
        {
            UsuarioId = 1,
            Username = "admin",
            Rol = "Administrador",
            EstaAutenticado = true
        };
        _hasher = new HasherFalso();

        _activacionInvitacion =
            new ActivacionInvitacionFalsa(_usuarios, _solicitudes);

        _generador = new GeneradorNombreUsuarioService(
            _usuarios,
            _solicitudes);

        _service = new UsuarioAdministracionService(
            _usuarios,
            _solicitudes,
            _email,
            _configuration,
            _auditoria,
            _currentUser,
            _hasher,
            _activacionInvitacion,
            _generador);
    }

    [Fact]
    public void CrearInvitacion_GeneraSolicitudYEnviaCorreo()
    {
        string token = _service.CrearInvitacion(
            "nuevo_usuario",
            "nuevo@test.com",
            "Recepcionista",
            "ClaveSegura123!");

        Assert.NotNull(token);
        Assert.Equal(64, token.Length);

        var solicitud = _solicitudes.Guardada;
        Assert.NotNull(solicitud);
        Assert.Equal("nuevo_usuario", solicitud!.Username);
        Assert.Equal("nuevo@test.com", solicitud.Email);
        Assert.Equal("PendienteVerificacion", solicitud.Estado);
        Assert.Equal("HASH_ClaveSegura123!", solicitud.PasswordHash);
        Assert.Equal("Recepcionista", solicitud.Rol);

        Assert.True(_email.Enviado);
        Assert.Equal("nuevo@test.com", _email.Destinatario);
        Assert.Contains("/Auth/ActivarInvitacion?token=", _email.Enlace);

        var auditoria = _auditoria.UltimoRegistro;
        Assert.NotNull(auditoria);
        Assert.Equal("USUARIO_INVITADO", auditoria!.Accion);
        Assert.Equal("nuevo_usuario", auditoria.EntidadId);
        Assert.Contains("Rol: Recepcionista", auditoria.Detalle);
    }

    [Fact]
    public void CrearInvitacion_GeneraSolicitudConRolAdministrador()
    {
        string token = _service.CrearInvitacion(
            "admin_user",
            "admin@test.com",
            "Administrador",
            "ClaveSegura123!");

        Assert.NotNull(token);

        var solicitud = _solicitudes.Guardada;
        Assert.NotNull(solicitud);
        Assert.Equal("admin_user", solicitud!.Username);
        Assert.Equal("admin@test.com", solicitud.Email);
        Assert.Equal("Administrador", solicitud.Rol);

        var auditoria = _auditoria.UltimoRegistro;
        Assert.NotNull(auditoria);
        Assert.Contains("Rol: Administrador", auditoria.Detalle);
    }

    [Theory]
    [InlineData("ab", "nuevo@test.com", "Recepcionista")]
    [InlineData("nuevo_usuario", "correo-invalido", "Recepcionista")]
    [InlineData("nuevo_usuario", "nuevo@test.com", "Invalido")]
    public void CrearInvitacion_RechazaDatosInvalidos(
        string username,
        string email,
        string rol)
    {
        Assert.Throws<ArgumentException>(() =>
            _service.CrearInvitacion(
                username,
                email,
                rol,
                "ClaveSegura123!"));

        Assert.Null(_solicitudes.Guardada);
    }

    [Fact]
    public void CrearInvitacion_RechazaUsuarioDuplicado()
    {
        _usuarios.UsernameExiste = true;

        Assert.Throws<SolicitudRegistroDuplicadaException>(() =>
            _service.CrearInvitacion(
                "existente",
                "nuevo@test.com",
                "Recepcionista",
                "ClaveSegura123!"));

        Assert.Null(_solicitudes.Guardada);
    }

    [Fact]
    public void CrearInvitacion_RechazaEmailDuplicado()
    {
        _solicitudes.EmailExiste = true;

        Assert.Throws<SolicitudRegistroDuplicadaException>(() =>
            _service.CrearInvitacion(
                "nuevo_usuario",
                "existente@test.com",
                "Recepcionista",
                "ClaveSegura123!"));

        Assert.Null(_solicitudes.Guardada);
    }

    [Fact]
    public void CrearInvitacion_RequiereAdministrador()
    {
        _currentUser.EstaAutenticado = false;

        Assert.Throws<UnauthorizedAccessException>(() =>
            _service.CrearInvitacion(
                "nuevo_usuario",
                "nuevo@test.com",
                "Recepcionista",
                "ClaveSegura123!"));
    }

    [Fact]
    public void ActivarUsuarioDesdeInvitacion_CreaUsuarioConRolRecepcionista()
    {
        string rawToken =
            "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        byte[] tokenBytes = Convert.FromHexString(rawToken);
        string tokenHash =
            Convert.ToHexString(SHA256.HashData(tokenBytes));

        var solicitud = new SolicitudRegistro
        {
            Id = 1,
            Username = "nuevo_usuario",
            Email = "nuevo@test.com",
            PasswordHash = "HASH_Password123456",
            Estado = "PendienteVerificacion",
            TokenVerificacionHash = tokenHash,
            TokenExpiraEn = DateTime.UtcNow.AddHours(1),
            FechaSolicitud = DateTime.UtcNow,
            Rol = "Recepcionista"
        };

        _solicitudes.SolicitudPorToken = solicitud;
        _solicitudes.TokenHashParaConfirmar = tokenHash;

        bool resultado = _service.ActivarUsuarioDesdeInvitacion(
            rawToken,
            out string? error);

        Assert.True(resultado);
        Assert.Null(error);

        var usuario = _usuarios.Usuarios.Values
            .FirstOrDefault(u => u.Username == "nuevo_usuario");

        Assert.NotNull(usuario);
        Assert.Equal("Recepcionista", usuario!.Rol);
        Assert.True(usuario.Activo);
        Assert.Equal("HASH_Password123456", usuario.PasswordHash);

        var auditoria = _auditoria.UltimoRegistro;
        Assert.NotNull(auditoria);
        Assert.Equal("USUARIO_ACTIVADO", auditoria!.Accion);
        Assert.Contains("Rol asignado: Recepcionista", auditoria.Detalle);
    }

    [Fact]
    public void ActivarUsuarioDesdeInvitacion_CreaUsuarioConRolAdministrador()
    {
        string rawToken =
            "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        byte[] tokenBytes = Convert.FromHexString(rawToken);
        string tokenHash =
            Convert.ToHexString(SHA256.HashData(tokenBytes));

        var solicitud = new SolicitudRegistro
        {
            Id = 2,
            Username = "nuevo_admin",
            Email = "nuevo_admin@test.com",
            PasswordHash = "HASH_Password123456",
            Estado = "PendienteVerificacion",
            TokenVerificacionHash = tokenHash,
            TokenExpiraEn = DateTime.UtcNow.AddHours(1),
            FechaSolicitud = DateTime.UtcNow,
            Rol = "Administrador"
        };

        _solicitudes.SolicitudPorToken = solicitud;
        _solicitudes.TokenHashParaConfirmar = tokenHash;

        bool resultado = _service.ActivarUsuarioDesdeInvitacion(
            rawToken,
            out string? error);

        Assert.True(resultado, error);
        Assert.Null(error);

        var usuario = _usuarios.Usuarios.Values
            .FirstOrDefault(u => u.Username == "nuevo_admin");

        Assert.NotNull(usuario);
        Assert.Equal("Administrador", usuario!.Rol);
        Assert.True(usuario.Activo);

        var auditoria = _auditoria.UltimoRegistro;
        Assert.NotNull(auditoria);
        Assert.Contains("Rol asignado: Administrador", auditoria.Detalle);
    }

    [Fact]
    public void ActivarUsuarioDesdeInvitacion_RechazaTokenInvalido()
    {
        bool resultado = _service.ActivarUsuarioDesdeInvitacion(
            "token_invalido",
            out string? error);

        Assert.False(resultado);
        Assert.Equal("Token inválido.", error);
    }

    [Fact]
    public void CrearInvitacion_RechazaPasswordCorto()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.CrearInvitacion(
                "nuevo_usuario",
                "nuevo@test.com",
                "Recepcionista",
                "corto"));

        Assert.Null(_solicitudes.Guardada);
    }

    [Fact]
    public void ActivarUsuarioDesdeInvitacion_RechazaSolicitudYaProcesada()
    {
        string rawToken =
            "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        byte[] tokenBytes = Convert.FromHexString(rawToken);
        string tokenHash =
            Convert.ToHexString(SHA256.HashData(tokenBytes));

        var solicitud = new SolicitudRegistro
        {
            Id = 3,
            Username = "usuario_procesado",
            Email = "procesado@test.com",
            PasswordHash = string.Empty,
            Estado = "Aprobada",
            TokenVerificacionHash = null,
            TokenExpiraEn = null,
            FechaSolicitud = DateTime.UtcNow,
            Rol = "Recepcionista"
        };

        _solicitudes.SolicitudPorToken = solicitud;
        _solicitudes.TokenHashParaConfirmar = tokenHash;

        bool resultado = _service.ActivarUsuarioDesdeInvitacion(
            rawToken,
            out string? error);

        Assert.False(resultado);
        Assert.Equal(
            "El enlace de invitación no es válido o ha expirado.",
            error);
    }

    [Fact]
    public void ActualizarUsuario_ModificaDatosCorrectamente()
    {
        var usuario = new Usuario
        {
            Id = 2,
            Username = "usuario2",
            Email = "usuario2@test.com",
            Rol = "Recepcionista",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[2] = usuario;

        bool resultado = _service.ActualizarUsuario(
            2,
            "nuevo@test.com",
            "Administrador",
            true,
            out string? error);

        Assert.True(resultado);
        Assert.Null(error);

        var actualizado = _usuarios.Usuarios[2];
        Assert.Equal("nuevo@test.com", actualizado.Email);
        Assert.Equal("Administrador", actualizado.Rol);
        Assert.True(actualizado.Activo);
        Assert.Equal("admin", actualizado.ModificadoPor);

        var auditoria = _auditoria.UltimoRegistro;
        Assert.Equal("USUARIO_ACTUALIZADO", auditoria!.Accion);
    }

    [Fact]
    public void ActualizarUsuario_RechazaDesactivarPropiaCuenta()
    {
        var usuario = new Usuario
        {
            Id = 1,
            Username = "admin",
            Email = "admin@test.com",
            Rol = "Administrador",
            Activo = true,
            CreadoPor = "sistema",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[1] = usuario;

        bool resultado = _service.ActualizarUsuario(
            1,
            "admin@test.com",
            "Administrador",
            false,
            out string? error);

        Assert.False(resultado);
        Assert.Equal("No puede desactivar su propia cuenta.", error);
    }

    [Fact]
    public void ActualizarUsuario_RechazaCambiarPropioRol()
    {
        var usuario = new Usuario
        {
            Id = 1,
            Username = "admin",
            Email = "admin@test.com",
            Rol = "Administrador",
            Activo = true,
            CreadoPor = "sistema",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[1] = usuario;

        bool resultado = _service.ActualizarUsuario(
            1,
            "admin@test.com",
            "Recepcionista",
            true,
            out string? error);

        Assert.False(resultado);
        Assert.Equal("No puede cambiar su propio rol.", error);
    }

    [Fact]
    public void ActualizarUsuario_RechazaDesactivarUltimoAdministrador()
    {
        var admin1 = new Usuario
        {
            Id = 1,
            Username = "admin",
            Email = "admin@test.com",
            Rol = "Administrador",
            Activo = false,
            CreadoPor = "sistema",
            FechaCreacion = DateTime.UtcNow
        };

        var admin2 = new Usuario
        {
            Id = 2,
            Username = "admin2",
            Email = "admin2@test.com",
            Rol = "Administrador",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[1] = admin1;
        _usuarios.Usuarios[2] = admin2;

        _currentUser.UsuarioId = 1;

        bool resultado = _service.ActualizarUsuario(
            2,
            "admin2@test.com",
            "Administrador",
            false,
            out string? error);

        Assert.False(resultado);
        Assert.Equal(
            "No puede desactivar el último administrador activo.",
            error);
    }

    [Fact]
    public void DesactivarUsuario_DesactivaCorrectamente()
    {
        var usuario = new Usuario
        {
            Id = 2,
            Username = "usuario2",
            Email = "usuario2@test.com",
            Rol = "Recepcionista",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[2] = usuario;

        bool resultado = _service.DesactivarUsuario(
            2,
            out string? error);

        Assert.True(resultado);
        Assert.Null(error);
        Assert.False(_usuarios.Usuarios[2].Activo);

        var auditoria = _auditoria.UltimoRegistro;
        Assert.Equal("USUARIO_DESACTIVADO", auditoria!.Accion);
    }

    [Fact]
    public void DesactivarUsuario_RechazaDesactivarPropiaCuenta()
    {
        var usuario = new Usuario
        {
            Id = 1,
            Username = "admin",
            Email = "admin@test.com",
            Rol = "Administrador",
            Activo = true,
            CreadoPor = "sistema",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[1] = usuario;

        bool resultado = _service.DesactivarUsuario(
            1,
            out string? error);

        Assert.False(resultado);
        Assert.Equal("No puede desactivar su propia cuenta.", error);
    }

    [Fact]
    public void DesactivarUsuario_RechazaDesactivarUltimoAdministrador()
    {
        var admin1 = new Usuario
        {
            Id = 1,
            Username = "admin",
            Email = "admin@test.com",
            Rol = "Administrador",
            Activo = false,
            CreadoPor = "sistema",
            FechaCreacion = DateTime.UtcNow
        };

        var admin2 = new Usuario
        {
            Id = 2,
            Username = "admin2",
            Email = "admin2@test.com",
            Rol = "Administrador",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[1] = admin1;
        _usuarios.Usuarios[2] = admin2;

        _currentUser.UsuarioId = 1;

        bool resultado = _service.DesactivarUsuario(
            2,
            out string? error);

        Assert.False(resultado);
        Assert.Equal(
            "No puede desactivar el último administrador activo.",
            error);
    }

    [Fact]
    public void ReactivarUsuario_ReactivarCorrectamente()
    {
        var usuario = new Usuario
        {
            Id = 2,
            Username = "usuario2",
            Email = "usuario2@test.com",
            Rol = "Recepcionista",
            Activo = false,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[2] = usuario;

        bool resultado = _service.ReactivarUsuario(
            2,
            out string? error);

        Assert.True(resultado);
        Assert.Null(error);
        Assert.True(_usuarios.Usuarios[2].Activo);

        var auditoria = _auditoria.UltimoRegistro;
        Assert.Equal("USUARIO_REACTIVADO", auditoria!.Accion);
    }

    [Fact]
    public void ReactivarUsuario_RechazaSiYaActivo()
    {
        var usuario = new Usuario
        {
            Id = 2,
            Username = "usuario2",
            Email = "usuario2@test.com",
            Rol = "Recepcionista",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[2] = usuario;

        bool resultado = _service.ReactivarUsuario(
            2,
            out string? error);

        Assert.False(resultado);
        Assert.Equal("El usuario ya está activo.", error);
    }

    [Fact]
    public void ActualizarUsuario_RechazaDegradarUltimoAdministrador()
    {
        var admin1 = new Usuario
        {
            Id = 1,
            Username = "admin",
            Email = "admin@test.com",
            Rol = "Administrador",
            Activo = false,
            CreadoPor = "sistema",
            FechaCreacion = DateTime.UtcNow
        };

        var admin2 = new Usuario
        {
            Id = 2,
            Username = "admin2",
            Email = "admin2@test.com",
            Rol = "Administrador",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[1] = admin1;
        _usuarios.Usuarios[2] = admin2;

        _currentUser.UsuarioId = 1;

        bool resultado = _service.ActualizarUsuario(
            2,
            "admin2@test.com",
            "Recepcionista",
            true,
            out string? error);

        Assert.False(resultado);
        Assert.Equal(
            "No puede desactivar el último administrador activo.",
            error);
    }

    [Fact]
    public void ActualizarUsuario_PermiteDegradarAdministradorSiHayOtroActivo()
    {
        var admin1 = new Usuario
        {
            Id = 1,
            Username = "admin",
            Email = "admin@test.com",
            Rol = "Administrador",
            Activo = true,
            CreadoPor = "sistema",
            FechaCreacion = DateTime.UtcNow
        };

        var admin2 = new Usuario
        {
            Id = 2,
            Username = "admin2",
            Email = "admin2@test.com",
            Rol = "Administrador",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[1] = admin1;
        _usuarios.Usuarios[2] = admin2;
        _usuarios.AdminActivosCount = 2;

        _currentUser.UsuarioId = 1;

        bool resultado = _service.ActualizarUsuario(
            2,
            "admin2@test.com",
            "Recepcionista",
            true,
            out string? error);

        Assert.True(resultado);
        Assert.Null(error);
        Assert.Equal("Recepcionista", _usuarios.Usuarios[2].Rol);
        Assert.True(_usuarios.Usuarios[2].Activo);
    }

    [Fact]
    public void ActualizarUsuario_UsaMetodoAtomico()
    {
        var usuario = new Usuario
        {
            Id = 2,
            Username = "usuario2",
            Email = "usuario2@test.com",
            Rol = "Recepcionista",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[2] = usuario;

        bool resultado = _service.ActualizarUsuario(
            2,
            "nuevo@test.com",
            "Administrador",
            true,
            out string? error);

        Assert.True(resultado);
        Assert.True(_usuarios.UltimoActualizacionAtomicaExitosa);
    }

    [Fact]
    public void DesactivarUsuario_UsaMetodoAtomico()
    {
        var usuario = new Usuario
        {
            Id = 2,
            Username = "usuario2",
            Email = "usuario2@test.com",
            Rol = "Recepcionista",
            Activo = true,
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarios.Usuarios[2] = usuario;

        bool resultado = _service.DesactivarUsuario(
            2,
            out string? error);

        Assert.True(resultado);
        Assert.True(_usuarios.UltimaDesactivacionAtomicaExitosa);
    }

    [Fact]
    public void CrearInvitacion_GeneraNombreUsuarioDesdeCorreoCuandoEstaVacio()
    {
        string token = _service.CrearInvitacion(
            "",
            "Juan.Pérez@empresa.com",
            "Recepcionista",
            "ClaveSegura123!");

        Assert.NotNull(token);

        var solicitud = _solicitudes.Guardada;
        Assert.NotNull(solicitud);
        Assert.Equal("juan.perez", solicitud!.Username);
        Assert.Equal("juan.pérez@empresa.com", solicitud.Email);

        var auditoria = _auditoria.UltimoRegistro;
        Assert.NotNull(auditoria);
        Assert.Equal("USUARIO_INVITADO", auditoria!.Accion);
    }

    [Fact]
    public void CrearInvitacion_GeneraSufijoIncrementalCuandoColisiona()
    {
        _usuarios.Usuarios[9] = new Usuario
        {
            Id = 9,
            Username = "ana.lopez",
            Email = "otro@empresa.com",
            PasswordHash = "hash",
            Rol = "Recepcionista",
            Activo = true,
            CreadoPor = "sistema",
            FechaCreacion = DateTime.UtcNow
        };

        string token = _service.CrearInvitacion(
            "",
            "Ana.Lopez@empresa.com",
            "Recepcionista",
            "ClaveSegura123!");

        Assert.NotNull(token);

        var solicitud = _solicitudes.Guardada;
        Assert.NotNull(solicitud);
        Assert.Equal("ana.lopez-1", solicitud!.Username);
    }

    private sealed class UsuariosFalsos : IUsuarioAdministracionPort
    {
        public Dictionary<int, Usuario> Usuarios { get; } = new();
        public int AdminActivosCount { get; set; } = 1;
        public bool UsernameExiste { get; set; }
        public bool EmailExiste { get; set; }
        public bool UltimoActualizacionAtomicaExitosa { get; private set; }
        public bool UltimaDesactivacionAtomicaExitosa { get; private set; }

        public Usuario? ObtenerPorUsername(string username)
            => Usuarios.Values.FirstOrDefault(u => u.Username == username);

        public Usuario? ObtenerPorId(int id)
            => Usuarios.TryGetValue(id, out var u) ? u : null;

        public Usuario? ObtenerPorIdConCredenciales(int id)
            => Usuarios.TryGetValue(id, out var u) ? u : null;

        public bool ExisteUsername(string username)
            => UsernameExiste || Usuarios.Values.Any(u => u.Username == username);

        public bool ExisteEmail(string email)
            => EmailExiste || Usuarios.Values.Any(u => u.Email == email);

        public void Agregar(Usuario usuario)
        {
            usuario.Id = Usuarios.Count + 1;
            Usuarios[usuario.Id] = usuario;
        }

        public void Actualizar(Usuario usuario)
        {
            if (Usuarios.ContainsKey(usuario.Id))
            {
                Usuarios[usuario.Id] = usuario;
            }
        }

        public IReadOnlyList<UsuarioListadoDto> Listar(
            int pagina,
            int tamanoPagina)
            => Usuarios.Values
                .OrderByDescending(u => u.FechaCreacion)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(u => new UsuarioListadoDto(
                    u.Id,
                    u.Username,
                    u.Email ?? "",
                    u.Rol,
                    u.Activo,
                    u.FechaCreacion))
                .ToList();

        public int ContarTotal() => Usuarios.Count;

        public int ContarAdministradoresActivos() => AdminActivosCount;

        public bool ActualizarUsuarioAtomico(
            int id,
            string email,
            string rol,
            bool activo,
            string modificadoPor,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!Usuarios.TryGetValue(id, out var usuario))
            {
                errorMessage = "Usuario no encontrado.";
                return false;
            }

            var adminsActivos = Usuarios.Values.Count(
                u => u.Rol == "Administrador" && u.Activo);

            bool esAdminActual =
                usuario.Rol == "Administrador" && usuario.Activo;

            bool seraAdminActivo =
                activo && rol == "Administrador";

            if (esAdminActual && !seraAdminActivo && adminsActivos <= 1)
            {
                errorMessage =
                    "No puede desactivar el último administrador activo.";
                return false;
            }

            usuario.Email = email;
            usuario.Rol = rol;
            usuario.Activo = activo;
            usuario.ModificadoPor = modificadoPor;
            usuario.FechaModificacion = DateTime.UtcNow;
            Usuarios[id] = usuario;

            UltimoActualizacionAtomicaExitosa = true;
            return true;
        }

        public bool DesactivarUsuarioAtomico(
            int id,
            string modificadoPor,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!Usuarios.TryGetValue(id, out var usuario))
            {
                errorMessage =
                    "El usuario ya está desactivado o no existe.";
                return false;
            }

            var adminsActivos = Usuarios.Values.Count(
                u => u.Rol == "Administrador" && u.Activo);

            bool esAdminActual =
                usuario.Rol == "Administrador" && usuario.Activo;

            if (esAdminActual && adminsActivos <= 1)
            {
                errorMessage =
                    "No puede desactivar el último administrador activo.";
                return false;
            }

            if (!usuario.Activo)
            {
                errorMessage =
                    "El usuario ya está desactivado o no existe.";
                return false;
            }

            usuario.Activo = false;
            usuario.ModificadoPor = modificadoPor;
            usuario.FechaModificacion = DateTime.UtcNow;
            Usuarios[id] = usuario;

            UltimaDesactivacionAtomicaExitosa = true;
            return true;
        }
    }

    private sealed class SolicitudesFalsas : ISolicitudRegistroPort
    {
        public SolicitudRegistro? Guardada { get; private set; }
        public bool UsernameExiste { get; set; }
        public bool EmailExiste { get; set; }
        public string? TokenHashParaConfirmar { get; set; }
        public SolicitudRegistro? SolicitudPorToken { get; set; }

        public bool ExisteUsername(string username) => UsernameExiste;

        public bool ExisteEmail(string email) => EmailExiste;

        public void Agregar(SolicitudRegistro solicitud)
        {
            solicitud.Id = 1;
            Guardada = solicitud;
        }

        public SolicitudRegistro? ObtenerPorId(long id) => null;

        public SolicitudRegistro? ObtenerPorTokenHash(string tokenHash)
            => tokenHash == TokenHashParaConfirmar
                ? SolicitudPorToken
                : null;

        public SolicitudRegistro? ObtenerPendientePorEmail(string email)
            => null;

        public bool IntentarRenovarTokenConLimite(
            long solicitudId,
            string nuevoTokenHash,
            DateTime nuevaExpiracion)
            => false;

        public SolicitudRegistro? ConfirmarEmail(string tokenHash)
        {
            if (tokenHash == TokenHashParaConfirmar &&
                SolicitudPorToken != null &&
                SolicitudPorToken.Estado == "PendienteVerificacion")
            {
                SolicitudPorToken.Estado = "PendienteAprobacion";
                SolicitudPorToken.EmailVerificadoEn = DateTime.UtcNow;
                SolicitudPorToken.TokenVerificacionHash = null;
                SolicitudPorToken.TokenExpiraEn = null;

                return SolicitudPorToken;
            }

            return null;
        }

        public void Actualizar(SolicitudRegistro solicitud) { }
    }

    private sealed class EmailFalso : IEmailService
    {
        public bool Enviado { get; private set; }
        public string? Destinatario { get; private set; }
        public string? Enlace { get; private set; }
        public bool ErrorConfiguracion { get; set; }
        public bool ErrorEnvio { get; set; }

        public void ValidarConfiguracion()
        {
            if (ErrorConfiguracion)
            {
                throw new ConfiguracionRegistroException("test");
            }
        }

        public Task EnviarVerificacionAsync(
            string destinatario,
            string enlaceVerificacion,
            CancellationToken cancellationToken = default, string? username = null)
        {
            if (ErrorEnvio)
            {
                throw new EnvioVerificacionException("test", "test", null);
            }

            Enviado = true;
            Destinatario = destinatario;
            Enlace = enlaceVerificacion;

            return Task.CompletedTask;
        }
    }

    private sealed class ConfigurationFalsa : IConfiguration
    {
        public string? this[string key]
        {
            get => key == "App:PublicBaseUrl"
                ? "https://localhost"
                : string.Empty;
            set { }
        }

        public IEnumerable<IConfigurationSection> GetChildren()
            => Array.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken()
            => new CancellationChangeToken(
                new CancellationTokenSource().Token);

        public IConfigurationSection GetSection(string key)
            => new ConfigurationSectionFalsa(key);
    }

    private sealed class ConfigurationSectionFalsa : IConfigurationSection
    {
        public string Key { get; }
        public string Path { get; }

        public string? this[string key]
        {
            get => string.Empty;
            set { }
        }

        public string? Value
        {
            get => string.Empty;
            set { }
        }

        public IEnumerable<IConfigurationSection> GetChildren()
            => Array.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken()
            => new CancellationChangeToken(
                new CancellationTokenSource().Token);

        public IConfigurationSection GetSection(string key)
            => new ConfigurationSectionFalsa(key);

        public ConfigurationSectionFalsa(string key)
        {
            Key = key;
            Path = key;
        }
    }

    private sealed class AuditoriaFalsa : IAuditoriaPort
    {
        public RegistroAuditoria? UltimoRegistro { get; private set; }

        public void Registrar(RegistroAuditoria registro)
            => UltimoRegistro = registro;

        public IReadOnlyList<RegistroAuditoria> ObtenerRecientes(int cantidad)
            => [];
    }

    private sealed class CurrentUserFalso : ICurrentUser
    {
        public bool EstaAutenticado { get; set; }
        public int? UsuarioId { get; set; }
        public string? Username { get; set; }
        public string? Rol { get; set; }
    }

    private sealed class HasherFalso : IPasswordHasher
    {
        public string GenerarHash(string password)
            => "HASH_" + password;

        public bool VerificarPassword(string password, string hash)
            => hash == "HASH_" + password;
    }

    private sealed class ActivacionInvitacionFalsa
        : IActivacionInvitacionPort
    {
        private readonly UsuariosFalsos _usuarios;
        private readonly SolicitudesFalsas _solicitudes;

        public ActivacionInvitacionFalsa(
            UsuariosFalsos usuarios,
            SolicitudesFalsas solicitudes)
        {
            _usuarios = usuarios;
            _solicitudes = solicitudes;
        }

        public ResultadoActivacionInvitacion ActivarPorTokenHash(
            string tokenHash)
        {
            const string errorInvitacion =
                "El enlace de invitación no es válido o ha expirado.";

            var solicitud = _solicitudes.SolicitudPorToken;

            if (solicitud is null ||
                tokenHash != _solicitudes.TokenHashParaConfirmar ||
                tokenHash != solicitud.TokenVerificacionHash ||
                solicitud.Estado != "PendienteVerificacion" ||
                solicitud.EmailVerificadoEn != null ||
                !solicitud.TokenExpiraEn.HasValue ||
                solicitud.TokenExpiraEn.Value <= DateTime.UtcNow)
            {
                return new ResultadoActivacionInvitacion(
                    false,
                    null,
                    null,
                    errorInvitacion);
            }

            if (string.IsNullOrWhiteSpace(solicitud.PasswordHash) ||
                (solicitud.Rol != "Administrador" &&
                 solicitud.Rol != "Recepcionista"))
            {
                return new ResultadoActivacionInvitacion(
                    false,
                    null,
                    null,
                    "La invitación no tiene credenciales o rol válidos.");
            }

            var usuario = new Usuario
            {
                Username = solicitud.Username,
                PasswordHash = solicitud.PasswordHash,
                Email = solicitud.Email,
                EmailVerificado = true,
                Rol = solicitud.Rol!,
                Activo = true,
                CreadoPor = "sistema",
                FechaCreacion = DateTime.UtcNow
            };

            _usuarios.Agregar(usuario);

            solicitud.Estado = "Aprobada";
            solicitud.EmailVerificadoEn = DateTime.UtcNow;
            solicitud.FechaResolucion = DateTime.UtcNow;
            solicitud.TokenVerificacionHash = null;
            solicitud.TokenExpiraEn = null;
            solicitud.RevisadoPorUsuarioId = null;

            return new ResultadoActivacionInvitacion(
                true,
                solicitud.Username,
                solicitud.Rol,
                null);
        }
    }

    private sealed class GeneradorNombreUsuarioFalso
        : IGeneradorNombreUsuarioPort
    {
        public string GenerarUsuarioUnico(
            string correoElectronico,
            CancellationToken cancellationToken = default)
        {
            string baseUsuario = correoElectronico
                .Split('@', 2)[0]
                .ToLowerInvariant();

            return baseUsuario.Length > 0
                ? baseUsuario
                : "usuario";
        }
    }
}
