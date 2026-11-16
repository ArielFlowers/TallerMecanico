using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(
    IAuthenticationService autenticacion,
    IUsuarioPort usuarios,
    IAuditoriaPort auditoria,
    ICurrentUser usuarioActual,
    ICambioContrasenaPort cambioContrasena,
    VerificacionEmailService verificacion,
    IGestionUsuariosPort gestionUsuarios,
    ReenvioVerificacionService reenvio,
    RegistroFacade registro,
    SesionUsuarioService sesiones,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<UsuarioSesionDto> Login(CredencialesLogin solicitud)
    {
        var usuario = autenticacion.ValidarCredenciales(solicitud.Username, solicitud.Password);
        if (usuario is null || usuario.Rol is not (Roles.Administrador or Roles.Recepcionista))
        {
            Registrar("LOGIN_FALLIDO", null, null);
            return Unauthorized(new ErrorApi("Usuario o contraseña incorrectos."));
        }

        sesiones.Iniciar(usuario.Id);
        Registrar("LOGIN_EXITOSO", usuario.Id, usuario.Username);
        return Ok(new UsuarioSesionDto(usuario.Id, usuario.Username, usuario.Rol));
    }

    [HttpPost("logout")]
    [Authorize(Roles = Roles.Personal)]
    public IActionResult Logout()
    {
        if (usuarioActual.UsuarioId is int id) sesiones.Cerrar(id);
        Registrar("LOGOUT", usuarioActual.UsuarioId, usuarioActual.Username);
        return NoContent();
    }

    // Solo exige la clave interna: también se usa antes de autorizar al Recepcionista.
    // El Frontend lo llama en cada petición, así que también renueva la sesión (RF-07).
    [HttpGet("estado/{id:int}")]
    public ActionResult<EstadoUsuarioDto> Estado(int id)
    {
        var usuario = id > 0 ? usuarios.ObtenerPorId(id) : null;
        bool activo = usuario?.Activo == true;
        bool sesionVigente = activo && sesiones.Renovar(id);
        return Ok(new EstadoUsuarioDto(activo, usuario?.Rol, usuario?.Username, sesionVigente));
    }

    [HttpPost("cambiar-contrasena")]
    [Authorize(Roles = Roles.Personal)]
    public IActionResult CambiarContrasena(CambiarContrasenaSolicitud solicitud)
    {
        if (!cambioContrasena.Cambiar(solicitud.Actual, solicitud.Nueva, solicitud.Confirmacion, out string error))
            return BadRequest(new ErrorApi(error));

        // El Frontend cierra la sesión después de cambiar la contraseña.
        if (usuarioActual.UsuarioId is int id) sesiones.Cerrar(id);
        return NoContent();
    }

    [HttpPost("verificar-email")]
    public IActionResult VerificarEmail(TokenSolicitud solicitud)
    {
        return verificacion.ConfirmarEmail(solicitud.Token)
            ? NoContent()
            : BadRequest(new ErrorApi("El enlace no es válido, ha expirado o ya fue utilizado."));
    }

    [HttpPost("activar-invitacion")]
    public IActionResult ActivarInvitacion(TokenSolicitud solicitud)
    {
        return gestionUsuarios.ActivarUsuarioDesdeInvitacion(solicitud.Token, out string? error)
            ? NoContent()
            : BadRequest(new ErrorApi(error ?? "El enlace de invitación no es válido o ha expirado."));
    }

    [HttpPost("reenviar-verificacion")]
    public async Task<IActionResult> ReenviarVerificacion(EmailSolicitud solicitud, CancellationToken cancelacion)
    {
        try
        {
            await reenvio.SolicitarReenvioAsync(solicitud.Email, cancelacion);
        }
        catch (ConfiguracionRegistroException error)
        {
            logger.LogError("Configuración de reenvío inválida: {Clave}.", error.Clave);
        }
        catch (EnvioVerificacionException error)
        {
            logger.LogWarning("Aceptación SMTP de reenvío no confirmada. Etapa: {Etapa}; Tipo: {Tipo}; Código: {Codigo}.",
                error.Etapa, error.TipoError, error.CodigoSmtp);
        }

        // Mismo resultado para correos existentes e inexistentes y envíos pendientes.
        return NoContent();
    }

    [HttpPost("registro")]
    public async Task<IActionResult> Registro(RegistroUsuarioSolicitud solicitud, CancellationToken cancelacion)
    {
        await registro.SolicitarRegistroAsync(solicitud.Username, solicitud.Email, solicitud.Password, cancelacion);
        return NoContent();
    }

    private void Registrar(string accion, int? id, string? username) => auditoria.Registrar(new RegistroAuditoria
    {
        UsuarioId = id,
        Username = username,
        Accion = accion,
        Entidad = "Usuario",
        EntidadId = id?.ToString(),
        Fecha = DateTime.UtcNow
    });
}
