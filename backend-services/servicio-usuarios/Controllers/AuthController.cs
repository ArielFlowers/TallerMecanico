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
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<UsuarioSesionDto> Login(CredencialesLogin solicitud)
    {
        var usuario = autenticacion.ValidarCredenciales(solicitud.Username, solicitud.Password);
        if (usuario is null || usuario.Rol is not ("Administrador" or "Recepcionista"))
        {
            Registrar("LOGIN_FALLIDO", null, null);
            return Unauthorized(new ErrorApi("Usuario o contraseña incorrectos."));
        }

        Registrar("LOGIN_EXITOSO", usuario.Id, usuario.Username);
        return Ok(new UsuarioSesionDto(usuario.Id, usuario.Username, usuario.Rol));
    }

    [HttpPost("logout")]
    [Authorize(Roles = "Administrador,Recepcionista")]
    public IActionResult Logout()
    {
        Registrar("LOGOUT", usuarioActual.UsuarioId, usuarioActual.Username);
        return NoContent();
    }

    // Solo exige la clave interna: también se usa antes de autorizar al Recepcionista.
    [HttpGet("estado/{id:int}")]
    public ActionResult<EstadoUsuarioDto> Estado(int id)
    {
        var usuario = id > 0 ? usuarios.ObtenerPorId(id) : null;
        return Ok(new EstadoUsuarioDto(usuario?.Activo == true, usuario?.Rol, usuario?.Username));
    }

    [HttpPost("cambiar-contrasena")]
    [Authorize(Roles = "Administrador,Recepcionista")]
    public IActionResult CambiarContrasena(CambiarContrasenaSolicitud solicitud)
    {
        return cambioContrasena.Cambiar(solicitud.Actual, solicitud.Nueva, solicitud.Confirmacion, out string error)
            ? NoContent()
            : BadRequest(new ErrorApi(error));
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
