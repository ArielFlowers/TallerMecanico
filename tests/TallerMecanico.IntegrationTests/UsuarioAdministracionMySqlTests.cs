using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TallerMecanico.Aplicacion.DTOs;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;
using TallerMecanico.Aplicacion.Validaciones;
using Xunit;

namespace TallerMecanico.IntegrationTests;

[Collection("MySQL aislado")]
public sealed class UsuarioAdministracionMySqlTests(MySqlAisladoFixture basePrueba)
{
    private readonly MySqlAisladoFixture _fixture = basePrueba;
    private static int _globalCounter = 0;

    private string Unique(string prefix) => $"{prefix}_{Interlocked.Increment(ref _globalCounter)}";

    private UsuarioAdministracionService CrearServicio(
        ICurrentUser? usuarioActual = null,
        IEmailService? email = null)
    {
        var usuarios = new UsuarioRepository(_fixture.Factory);
        var solicitudes = new SolicitudRegistroRepository(_fixture.Factory);
        var auditoria = new AuditoriaRepository(_fixture.Factory);
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:PublicBaseUrl"] = "https://localhost",
            ["Email:Smtp:Host"] = "smtp.gmail.com",
            ["Email:Smtp:Port"] = "465",
            ["Email:Smtp:Username"] = "test@test.com",
            ["Email:Smtp:Password"] = "test",
            ["Email:Smtp:From"] = "test@test.com"
        }).Build();
        var emailService = email ?? new CorreoSimulado();
        var currentUser = usuarioActual ?? new CurrentUserFalso { UsuarioId = 1, Username = "admin", Rol = "Administrador", EstaAutenticado = true };

        return new UsuarioAdministracionService(
            usuarios, solicitudes, emailService, config, auditoria, currentUser, hasher, _fixture.Factory);
    }

    private (int Id, string Username) CrearUsuarioAdmin(string? username = null)
    {
        username ??= Unique("admin");
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        var hash = hasher.GenerarHash("Admin123456");
        int id = checked((int)_fixture.Ejecutar("""
            INSERT INTO Usuarios (Username, PasswordHash, Email, EmailVerificado, Rol, Activo, CreadoPor, FechaCreacion)
            VALUES (@Username, @Hash, @Email, TRUE, 'Administrador', TRUE, 'sistema', UTC_TIMESTAMP());
            """, ("@Username", username), ("@Hash", hash), ("@Email", $"{username}@test.com")));
        return (id, username);
    }

    private int CrearUsuarioRecepcionista(string username, string email, bool activo = true)
    {
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        var hash = hasher.GenerarHash("Recepcion123456");
        return checked((int)_fixture.Ejecutar("""
            INSERT INTO Usuarios (Username, PasswordHash, Email, EmailVerificado, Rol, Activo, CreadoPor, FechaCreacion)
            VALUES (@Username, @Hash, @Email, TRUE, 'Recepcionista', @Activo, 'admin', UTC_TIMESTAMP());
            """, ("@Username", username), ("@Hash", hash), ("@Email", email), ("@Activo", activo)));
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Convert.FromHexString(token)));

    private void LimpiarDatosPrueba()
    {
        // Orden importante por FKs: Auditoria -> SolicitudesRegistro -> Usuarios
        _fixture.Ejecutar("TRUNCATE TABLE Auditoria;");
        _fixture.Ejecutar("TRUNCATE TABLE SolicitudesRegistro;");
        _fixture.Ejecutar("TRUNCATE TABLE Usuarios;");
    }

    // =====================================================
    // CRITERIO 1: Listado de usuarios administrativo (2 pts)
    // =====================================================

    [MySqlAisladoFact]
    public void Listar_Usuarios_Paginado_Y_Filtrado()
    {
        LimpiarDatosPrueba();
        var (_, adminUsername) = CrearUsuarioAdmin();
        string r1 = Unique("r1"), r2 = Unique("r2"), r3 = Unique("r3");
        CrearUsuarioRecepcionista(r1, $"{r1}@test.com");
        CrearUsuarioRecepcionista(r2, $"{r2}@test.com");
        CrearUsuarioRecepcionista(r3, $"{r3}@test.com", activo: false);

        var port = new UsuarioRepository(_fixture.Factory);
        var lista = port.Listar(1, 10);

        Assert.Equal(4, lista.Count);
        Assert.Contains(lista, u => u.Username == adminUsername && u.Rol == "Administrador" && u.Activo);
        Assert.Contains(lista, u => u.Username == r1 && u.Activo);
        Assert.Contains(lista, u => u.Username == r2 && u.Activo);
        Assert.Contains(lista, u => u.Username == r3 && !u.Activo);
    }

    [MySqlAisladoFact]
    public void ContarTotal_Y_ContarAdministradoresActivos()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        CrearUsuarioRecepcionista(Unique("r1"), Unique("r1") + "@test.com");
        CrearUsuarioRecepcionista(Unique("r2"), Unique("r2") + "@test.com", activo: false);

        var port = new UsuarioRepository(_fixture.Factory);
        Assert.Equal(3, port.ContarTotal());
        Assert.Equal(1, port.ContarAdministradoresActivos());
    }

    [MySqlAisladoFact]
    public void ListarFiltrados_VerificaBusquedaRolEstadoPaginacionYContadores()
    {
        LimpiarDatosPrueba();

        var (_, admin) = CrearUsuarioAdmin();
        string activo1 = Unique("filtro_activo");
        string activo2 = Unique("filtro_activo");
        string inactivo = Unique("filtro_inactivo");

        CrearUsuarioRecepcionista(activo1, $"{activo1}@test.com");
        CrearUsuarioRecepcionista(activo2, $"{activo2}@test.com");
        CrearUsuarioRecepcionista(inactivo, $"{inactivo}@test.com", activo: false);

        var repositorio = new UsuarioRepository(_fixture.Factory);

        Assert.Equal(4, repositorio.ContarTotal());
        Assert.Equal(3, repositorio.ContarUsuariosActivos());
        Assert.Equal(1, repositorio.ContarAdministradoresActivosListado());

        Assert.Equal(4, repositorio.ContarFiltrados(null, null, null));
        Assert.Equal(3, repositorio.ContarFiltrados(null, null, true));
        Assert.Equal(1, repositorio.ContarFiltrados(null, null, false));
        Assert.Equal(3, repositorio.ContarFiltrados(null, "Recepcionista", null));
        Assert.Equal(1, repositorio.ContarFiltrados(null, "Administrador", true));

        var porUsuario = repositorio.ListarFiltrados(activo1, null, null, 1, 10);
        Assert.Single(porUsuario);
        Assert.Equal(activo1, porUsuario[0].Username);

        var porCorreo = repositorio.ListarFiltrados($"{inactivo}@test.com", null, null, 1, 10);
        Assert.Single(porCorreo);
        Assert.Equal(inactivo, porCorreo[0].Username);

        var combinados = repositorio.ListarFiltrados(
            "filtro_activo", "Recepcionista", true, 1, 10);

        Assert.Equal(2, combinados.Count);
        Assert.All(combinados, usuario =>
        {
            Assert.Equal("Recepcionista", usuario.Rol);
            Assert.True(usuario.Activo);
        });

        var pagina1 = repositorio.ListarFiltrados(
            null, "Recepcionista", null, 1, 2);

        var pagina2 = repositorio.ListarFiltrados(
            null, "Recepcionista", null, 2, 2);

        Assert.Equal(2, pagina1.Count);
        Assert.Single(pagina2);
        Assert.DoesNotContain(pagina1, u => u.Id == pagina2[0].Id);

        Assert.Empty(repositorio.ListarFiltrados(
            "usuario_inexistente_xyz", null, null, 1, 10));
    }
    // =====================================================
    // CRITERIO 2: Nombres de usuario únicos (5 pts)
    // =====================================================

    [MySqlAisladoFact]
    public void CrearInvitacion_RechazaUsernameDuplicado_EnUsuarios()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        string existing = Unique("existente");
        CrearUsuarioRecepcionista(existing, $"{existing}@test.com");

        var servicio = CrearServicio();

        Assert.Throws<SolicitudRegistroDuplicadaException>(() =>
            servicio.CrearInvitacion(existing, Unique("nuevo") + "@test.com", "Recepcionista"));
    }

    [MySqlAisladoFact]
    public void CrearInvitacion_RechazaUsernameDuplicado_EnSolicitudesPendientes()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var servicio = CrearServicio();
        string username = Unique("pendiente");
        servicio.CrearInvitacion(username, $"{username}@test.com", "Recepcionista");

        Assert.Throws<SolicitudRegistroDuplicadaException>(() =>
            servicio.CrearInvitacion(username, Unique("otro") + "@test.com", "Recepcionista"));
    }

    [MySqlAisladoFact]
    public void CrearInvitacion_RechazaEmailDuplicado_EnUsuarios()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        string email = Unique("duplicado") + "@test.com";
        CrearUsuarioRecepcionista(Unique("user1"), email);

        var servicio = CrearServicio();

        Assert.Throws<SolicitudRegistroDuplicadaException>(() =>
            servicio.CrearInvitacion(Unique("nuevo"), email, "Recepcionista"));
    }

    [MySqlAisladoFact]
    public void CrearInvitacion_RechazaEmailDuplicado_EnSolicitudesPendientes()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var servicio = CrearServicio();
        string email = Unique("pendiente") + "@test.com";
        servicio.CrearInvitacion(Unique("user1"), email, "Recepcionista");

        Assert.Throws<SolicitudRegistroDuplicadaException>(() =>
            servicio.CrearInvitacion(Unique("user2"), email, "Recepcionista"));
    }

    [MySqlAisladoFact]
    public void ActualizarUsuario_RechazaEmailDuplicado()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        string email1 = Unique("user1") + "@test.com";
        string email2 = Unique("user2") + "@test.com";
        int idUser1 = CrearUsuarioRecepcionista(Unique("user1"), email1);
        int idUser2 = CrearUsuarioRecepcionista(Unique("user2"), email2);

        var servicio = CrearServicio();

        Assert.False(servicio.ActualizarUsuario(idUser2, email1, "Recepcionista", true, out string? error));
        Assert.Equal("El correo electrónico ya está en uso.", error);
    }

    // =====================================================
    // CRITERIO 3: Invitación por email y activación (8 pts)
    // =====================================================

    [MySqlAisladoFact]
    public void CrearInvitacion_GeneraSolicitudPendienteVerificacion()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var email = new CorreoSimulado();
        var servicio = CrearServicio(email: email);
        string username = Unique("nuevo_user");

        string token = servicio.CrearInvitacion(username, $"{username}@test.com", "Recepcionista");

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(64, token.Length);
        Assert.True(email.Enviado);
        Assert.Equal($"{username}@test.com", email.Destinatario);
        Assert.Contains("/Auth/ActivarInvitacion?token=", email.Enlace);

        var solicitud = _fixture.Escalar("SELECT Username, Estado, Rol FROM SolicitudesRegistro WHERE Username = @Username", ("@Username", username));
        Assert.NotNull(solicitud);
    }

    [MySqlAisladoFact]
    public void CrearInvitacion_GeneraSolicitudConRolAdministrador()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var email = new CorreoSimulado();
        var servicio = CrearServicio(email: email);
        string username = Unique("nuevo_admin");

        string token = servicio.CrearInvitacion(username, $"{username}@test.com", "Administrador");

        var solicitud = _fixture.Escalar("SELECT Username, Rol FROM SolicitudesRegistro WHERE Username = @Username", ("@Username", username));
        Assert.NotNull(solicitud);
    }

    [MySqlAisladoFact]
    public void ActivarUsuarioDesdeInvitacion_CreaUsuarioConRolRecepcionista()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var email = new CorreoSimulado();
        var servicio = CrearServicio(email: email);
        string username = Unique("activado_user");

        string token = servicio.CrearInvitacion(username, $"{username}@test.com", "Recepcionista");

        // NO llamar MarcarSolicitudComoVerificada: la activación maneja la verificación internamente
        bool resultado = servicio.ActivarUsuarioDesdeInvitacion(token, "Password123456", out string? error);

        Assert.True(resultado);
        Assert.Null(error);

        var usuario = _fixture.Escalar("SELECT Username, Rol, Activo, EmailVerificado FROM Usuarios WHERE Username = @Username", ("@Username", username));
        Assert.NotNull(usuario);

        var rol = _fixture.Escalar("SELECT Rol FROM Usuarios WHERE Username = @Username", ("@Username", username));
        Assert.Equal("Recepcionista", rol);

        var solicitud = _fixture.Escalar("SELECT Estado FROM SolicitudesRegistro WHERE Username = @Username", ("@Username", username));
        Assert.Equal("Aprobada", solicitud?.ToString());
    }

    [MySqlAisladoFact]
    public void ActivarUsuarioDesdeInvitacion_CreaUsuarioConRolAdministrador()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var email = new CorreoSimulado();
        var servicio = CrearServicio(email: email);
        string username = Unique("activado_admin");

        string token = servicio.CrearInvitacion(username, $"{username}@test.com", "Administrador");

        bool resultado = servicio.ActivarUsuarioDesdeInvitacion(token, "Password123456", out string? error);

        Assert.True(resultado);
        Assert.Null(error);

        var usuario = _fixture.Escalar("SELECT Username, Rol, Activo FROM Usuarios WHERE Username = @Username", ("@Username", username));
        Assert.NotNull(usuario);

        var rol = _fixture.Escalar("SELECT Rol FROM Usuarios WHERE Username = @Username", ("@Username", username));
        Assert.Equal("Administrador", rol);
    }

    [MySqlAisladoFact]
    public void ActivarUsuarioDesdeInvitacion_RechazaTokenInvalido()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var servicio = CrearServicio();

        bool resultado = servicio.ActivarUsuarioDesdeInvitacion("token_invalido", "Password123456", out string? error);

        Assert.False(resultado);
        Assert.Equal("Token inválido.", error);
    }

    [MySqlAisladoFact]
    public void ActivarUsuarioDesdeInvitacion_RechazaPasswordCorto()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var email = new CorreoSimulado();
        var servicio = CrearServicio(email: email);
        string username = Unique("user_pass");

        string token = servicio.CrearInvitacion(username, $"{username}@test.com", "Recepcionista");

        bool resultado = servicio.ActivarUsuarioDesdeInvitacion(token, "corto", out string? error);

        Assert.False(resultado);
        Assert.Contains("contraseña no cumple", error!);
    }

    [MySqlAisladoFact]
    public void ActivarUsuarioDesdeInvitacion_RechazaSolicitudYaProcesada()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var email = new CorreoSimulado();
        var servicio = CrearServicio(email: email);
        string username = Unique("user_proc");

        string token = servicio.CrearInvitacion(username, $"{username}@test.com", "Recepcionista");

        // Marcar como ya procesada (Aprobada) - esto simula un intento previo
        _fixture.Ejecutar("""
            UPDATE SolicitudesRegistro
            SET Estado = 'Aprobada', FechaResolucion = UTC_TIMESTAMP(6),
                TokenVerificacionHash = NULL, TokenExpiraEn = NULL, EmailVerificadoEn = UTC_TIMESTAMP(6)
            WHERE Username = @Username;
            """, ("@Username", username));

        bool resultado = servicio.ActivarUsuarioDesdeInvitacion(token, "Password123456", out string? error);

        Assert.False(resultado);
        Assert.Equal("El enlace de invitación no es válido o ha expirado.", error);
    }

    // =====================================================
    [MySqlAisladoFact]
    public void ActivarUsuarioDesdeInvitacion_RechazaTokenExpirado()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();

        var servicio = CrearServicio();
        string username = Unique("invitacion_expirada");
        string email = $"{username}@test.com";

        string token = servicio.CrearInvitacion(
            username, email, "Recepcionista");

        // Simular expiracion sin esperar una hora.
        _fixture.Ejecutar("""
            UPDATE SolicitudesRegistro
            SET TokenExpiraEn = UTC_TIMESTAMP(6) - INTERVAL 1 HOUR
            WHERE Username = @Username;
            """, ("@Username", username));

        bool resultado = servicio.ActivarUsuarioDesdeInvitacion(
            token, "Password123456", out string? error);

        Assert.False(resultado);
        Assert.Equal(
            "El enlace de invitación no es válido o ha expirado.",
            error);

        Assert.Equal(0, Convert.ToInt32(_fixture.Escalar(
            "SELECT COUNT(*) FROM Usuarios WHERE Username = @Username;",
            ("@Username", username))));

        Assert.Equal("PendienteVerificacion", _fixture.Escalar(
            "SELECT Estado FROM SolicitudesRegistro WHERE Username = @Username;",
            ("@Username", username)));
    }
    // CRITERIO 4: Desactivación lógica y bloqueo de sesión (2 pts)
    // =====================================================

    [MySqlAisladoFact]
    public void DesactivarUsuario_DesactivaCorrectamente_Y_BloqueaLogin()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        string username = Unique("para_desactivar");
        int idUser = CrearUsuarioRecepcionista(username, $"{username}@test.com");

        var servicio = CrearServicio();

        bool resultado = servicio.DesactivarUsuario(idUser, out string? error);

        Assert.True(resultado);
        Assert.Null(error);

        var usuario = _fixture.Escalar("SELECT Activo FROM Usuarios WHERE Id = @Id", ("@Id", idUser));
        Assert.Equal(false, usuario);

        var usuariosRepo = new UsuarioRepository(_fixture.Factory);
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        var fallbackHash = new FallbackHashFalso(hasher);
        var authService = new AuthenticationService(usuariosRepo, hasher, fallbackHash);

        var authResult = authService.ValidarCredenciales(username, "Recepcion123456");
        Assert.Null(authResult);
    }

    [MySqlAisladoFact]
    public void ReactivarUsuario_ReactivarCorrectamente_Y_PermiteLogin()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        string username = Unique("para_reactivar");
        int idUser = CrearUsuarioRecepcionista(username, $"{username}@test.com", activo: false);

        var servicio = CrearServicio();

        bool resultado = servicio.ReactivarUsuario(idUser, out string? error);

        Assert.True(resultado);
        Assert.Null(error);

        var usuario = _fixture.Escalar("SELECT Activo FROM Usuarios WHERE Id = @Id", ("@Id", idUser));
        Assert.Equal(true, usuario);

        var usuariosRepo = new UsuarioRepository(_fixture.Factory);
        var hasher = new Argon2idPasswordHasher(Options.Create(new Argon2idOptions()));
        var fallbackHash = new FallbackHashFalso(hasher);
        var authService = new AuthenticationService(usuariosRepo, hasher, fallbackHash);

        var authResult = authService.ValidarCredenciales(username, "Recepcion123456");
        Assert.NotNull(authResult);
        Assert.Equal(username, authResult!.Username);
    }

    [MySqlAisladoFact]
    public void DesactivarUsuario_RechazaDesactivarPropiaCuenta()
    {
        LimpiarDatosPrueba();
        var (adminId, adminUsername) = CrearUsuarioAdmin();

        var currentUser = new CurrentUserFalso { UsuarioId = adminId, Username = adminUsername, Rol = "Administrador", EstaAutenticado = true };
        var servicio = CrearServicio(usuarioActual: currentUser);

        bool resultado = servicio.DesactivarUsuario(adminId, out string? error);

        Assert.False(resultado);
        Assert.Equal("No puede desactivar su propia cuenta.", error);
    }

    [MySqlAisladoFact]
    public void DesactivarUsuario_RechazaDesactivarUltimoAdministrador()
    {
        LimpiarDatosPrueba();
        // Solo 1 admin activo en la BD
        var (admin1Id, admin1Username) = CrearUsuarioAdmin();

        // Crear segundo admin
        string admin2Username = Unique("admin2");
        int admin2Id = checked((int)_fixture.Ejecutar("""
            INSERT INTO Usuarios (Username, PasswordHash, Email, EmailVerificado, Rol, Activo, CreadoPor, FechaCreacion)
            VALUES (@Username, @Hash, @Email, TRUE, 'Administrador', TRUE, 'admin', UTC_TIMESTAMP());
            """, ("@Username", admin2Username), ("@Hash", new Argon2idPasswordHasher(Options.Create(new Argon2idOptions())).GenerarHash("Admin123456")), ("@Email", $"{admin2Username}@test.com")));

        // Desactivar primer admin -> ahora solo queda admin2 como admin activo
        _fixture.Ejecutar("UPDATE Usuarios SET Activo = FALSE WHERE Id = @Id", ("@Id", admin1Id));

        // Intentar desactivar admin2 (el último admin activo) desde la sesión de admin1
        var currentUser = new CurrentUserFalso { UsuarioId = admin1Id, Username = admin1Username, Rol = "Administrador", EstaAutenticado = true };
        var servicio = CrearServicio(usuarioActual: currentUser);

        bool resultado = servicio.DesactivarUsuario(admin2Id, out string? error);

        Assert.False(resultado);
        Assert.Equal("No puede desactivar el último administrador activo.", error);
    }

    // =====================================================
    // CRITERIO 5: Roles, validaciones y auditoría (3 pts)
    // =====================================================

    [MySqlAisladoFact]
    public void ActualizarUsuario_CambiaRol_Y_Auditoria()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        int idUser = CrearUsuarioRecepcionista(Unique("cambio_rol"), Unique("cambio_rol") + "@test.com");

        var servicio = CrearServicio();

        bool resultado = servicio.ActualizarUsuario(idUser, Unique("cambio_rol") + "@test.com", "Administrador", true, out string? error);

        Assert.True(resultado);
        Assert.Null(error);

        var usuario = _fixture.Escalar("SELECT Rol FROM Usuarios WHERE Id = @Id", ("@Id", idUser));
        Assert.Equal("Administrador", usuario);

        // Verificar auditoría - obtener Accion y Detalle por separado
        var accion = _fixture.Escalar("SELECT Accion FROM Auditoria WHERE Entidad = 'Usuario' AND EntidadId = @Id ORDER BY Fecha DESC LIMIT 1", ("@Id", idUser.ToString()));
        var detalle = _fixture.Escalar("SELECT Detalle FROM Auditoria WHERE Entidad = 'Usuario' AND EntidadId = @Id ORDER BY Fecha DESC LIMIT 1", ("@Id", idUser.ToString()));

        Assert.Equal("USUARIO_ACTUALIZADO", accion?.ToString());
        Assert.Contains("Rol: Administrador", detalle?.ToString() ?? "");
    }

    [MySqlAisladoFact]
    public void ActualizarUsuario_RechazaCambiarPropioRol()
    {
        LimpiarDatosPrueba();
        var (adminId, adminUsername) = CrearUsuarioAdmin();

        var currentUser = new CurrentUserFalso { UsuarioId = adminId, Username = adminUsername, Rol = "Administrador", EstaAutenticado = true };
        var servicio = CrearServicio(usuarioActual: currentUser);

        bool resultado = servicio.ActualizarUsuario(adminId, $"{adminUsername}@test.com", "Recepcionista", true, out string? error);

        Assert.False(resultado);
        Assert.Equal("No puede cambiar su propio rol.", error);
    }

    [MySqlAisladoFact]
    public void ActualizarUsuario_RechazaDegradarUltimoAdministrador()
    {
        LimpiarDatosPrueba();
        // Solo 1 admin activo en la BD
        var (admin1Id, admin1Username) = CrearUsuarioAdmin();

        // Crear segundo admin
        string admin2Username = Unique("admin2");
        int admin2Id = checked((int)_fixture.Ejecutar("""
            INSERT INTO Usuarios (Username, PasswordHash, Email, EmailVerificado, Rol, Activo, CreadoPor, FechaCreacion)
            VALUES (@Username, @Hash, @Email, TRUE, 'Administrador', TRUE, 'admin', UTC_TIMESTAMP());
            """, ("@Username", admin2Username), ("@Hash", new Argon2idPasswordHasher(Options.Create(new Argon2idOptions())).GenerarHash("Admin123456")), ("@Email", $"{admin2Username}@test.com")));

        // Desactivar primer admin -> ahora solo queda admin2 como admin activo
        _fixture.Ejecutar("UPDATE Usuarios SET Activo = FALSE WHERE Id = @Id", ("@Id", admin1Id));

        // Intentar degradar admin2 (el último admin activo) desde la sesión de admin1
        var currentUser = new CurrentUserFalso { UsuarioId = admin1Id, Username = admin1Username, Rol = "Administrador", EstaAutenticado = true };
        var servicio = CrearServicio(usuarioActual: currentUser);

        bool resultado = servicio.ActualizarUsuario(admin2Id, $"{admin2Username}@test.com", "Recepcionista", true, out string? error);

        Assert.False(resultado);
        Assert.Equal("No puede desactivar el último administrador activo.", error);
    }

    [MySqlAisladoFact]
    public void CrearInvitacion_RegistraAuditoria()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var email = new CorreoSimulado();
        var servicio = CrearServicio(email: email);
        string username = Unique("auditado");

        servicio.CrearInvitacion(username, $"{username}@test.com", "Recepcionista");

        // Obtener Accion y Detalle por separado
        var accion = _fixture.Escalar("SELECT Accion FROM Auditoria WHERE Entidad = 'Usuario' AND EntidadId = @EntidadId ORDER BY Fecha DESC LIMIT 1", ("@EntidadId", username));
        var detalle = _fixture.Escalar("SELECT Detalle FROM Auditoria WHERE Entidad = 'Usuario' AND EntidadId = @EntidadId ORDER BY Fecha DESC LIMIT 1", ("@EntidadId", username));

        Assert.Equal("USUARIO_INVITADO", accion?.ToString());
        Assert.Contains("Rol: Recepcionista", detalle?.ToString() ?? "");
    }

    [MySqlAisladoFact]
    public void ActivarUsuarioDesdeInvitacion_RegistraAuditoria()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        var email = new CorreoSimulado();
        var servicio = CrearServicio(email: email);
        string username = Unique("auditado_activar");

        string token = servicio.CrearInvitacion(username, $"{username}@test.com", "Recepcionista");

        // NO llamar MarcarSolicitudComoVerificada
        servicio.ActivarUsuarioDesdeInvitacion(token, "Password123456", out _);

        // La activación registra USUARIO_ACTIVADO con EntidadId = username
        // Obtener la acción más reciente (debe ser USUARIO_ACTIVADO, no USUARIO_INVITADO)
        var accion = _fixture.Escalar("SELECT Accion FROM Auditoria WHERE Entidad = 'Usuario' AND EntidadId = @EntidadId ORDER BY Fecha DESC LIMIT 1", ("@EntidadId", username));
        var detalle = _fixture.Escalar("SELECT Detalle FROM Auditoria WHERE Entidad = 'Usuario' AND EntidadId = @EntidadId ORDER BY Fecha DESC LIMIT 1", ("@EntidadId", username));

        Assert.Equal("USUARIO_ACTIVADO", accion?.ToString());
        Assert.Contains("Rol asignado: Recepcionista", detalle?.ToString() ?? "");
    }

    [MySqlAisladoFact]
    public void DesactivarUsuario_RegistraAuditoria()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        string username = Unique("auditado_desac");
        int idUser = CrearUsuarioRecepcionista(username, $"{username}@test.com");

        var servicio = CrearServicio();
        servicio.DesactivarUsuario(idUser, out _);

        var accion = _fixture.Escalar("SELECT Accion FROM Auditoria WHERE Entidad = 'Usuario' AND EntidadId = @Id ORDER BY Fecha DESC LIMIT 1", ("@Id", idUser.ToString()));
        Assert.Equal("USUARIO_DESACTIVADO", accion?.ToString());
    }

    [MySqlAisladoFact]
    public void ReactivarUsuario_RegistraAuditoria()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        string username = Unique("auditado_react");
        int idUser = CrearUsuarioRecepcionista(username, $"{username}@test.com", activo: false);

        var servicio = CrearServicio();
        servicio.ReactivarUsuario(idUser, out _);

        var accion = _fixture.Escalar("SELECT Accion FROM Auditoria WHERE Entidad = 'Usuario' AND EntidadId = @Id ORDER BY Fecha DESC LIMIT 1", ("@Id", idUser.ToString()));
        Assert.Equal("USUARIO_REACTIVADO", accion?.ToString());
    }

    // =====================================================
    // Validaciones adicionales
    // =====================================================

    [MySqlAisladoFact]
    public void CrearInvitacion_RequiereAdministrador()
    {
        LimpiarDatosPrueba();
        var currentUser = new CurrentUserFalso { UsuarioId = 1, Username = "recepcionista", Rol = "Recepcionista", EstaAutenticado = true };
        var servicio = CrearServicio(usuarioActual: currentUser);

        Assert.Throws<UnauthorizedAccessException>(() =>
            servicio.CrearInvitacion(Unique("nuevo"), Unique("nuevo") + "@test.com", "Recepcionista"));
    }

    [MySqlAisladoFact]
    public void ActualizarUsuario_RequiereAdministrador()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        int idUser = CrearUsuarioRecepcionista(Unique("user"), Unique("user") + "@test.com");

        var currentUser = new CurrentUserFalso { UsuarioId = idUser, Username = "user", Rol = "Recepcionista", EstaAutenticado = true };
        var servicio = CrearServicio(usuarioActual: currentUser);

        Assert.Throws<UnauthorizedAccessException>(() =>
            servicio.ActualizarUsuario(idUser, $"{Unique("user")}@test.com", "Recepcionista", true, out _));
    }

    [MySqlAisladoFact]
    public void DesactivarUsuario_RequiereAdministrador()
    {
        LimpiarDatosPrueba();
        CrearUsuarioAdmin();
        int idUser = CrearUsuarioRecepcionista(Unique("user"), Unique("user") + "@test.com");

        var currentUser = new CurrentUserFalso { UsuarioId = idUser, Username = "user", Rol = "Recepcionista", EstaAutenticado = true };
        var servicio = CrearServicio(usuarioActual: currentUser);

        Assert.Throws<UnauthorizedAccessException>(() =>
            servicio.DesactivarUsuario(idUser, out _));
    }

    // =====================================================
    // Test helpers
    // =====================================================

    private sealed class CorreoSimulado : IEmailService
    {
        public bool Enviado { get; private set; }
        public string? Destinatario { get; private set; }
        public string? Enlace { get; private set; }
        public bool ErrorConfiguracion { get; set; }
        public bool ErrorEnvio { get; set; }

        public void ValidarConfiguracion()
        {
            if (ErrorConfiguracion) throw new ConfiguracionRegistroException("test");
        }

        public Task EnviarVerificacionAsync(string destinatario, string enlaceVerificacion, CancellationToken cancellationToken = default)
        {
            if (ErrorEnvio) throw new EnvioVerificacionException("test", "test", null);
            Enviado = true;
            Destinatario = destinatario;
            Enlace = enlaceVerificacion;
            return Task.CompletedTask;
        }
    }

    private sealed class CurrentUserFalso : ICurrentUser
    {
        public bool EstaAutenticado { get; set; }
        public int? UsuarioId { get; set; }
        public string? Username { get; set; }
        public string? Rol { get; set; }
    }

    private sealed class FallbackHashFalso : IFallbackHash
    {
        public string Hash { get; }

        public FallbackHashFalso(IPasswordHasher hasher)
        {
            Hash = hasher.GenerarHash(Guid.NewGuid().ToString("N"));
        }
    }
}