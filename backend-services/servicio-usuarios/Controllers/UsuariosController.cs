using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Contratos;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "Administrador")]
public sealed class UsuariosController(
    IUsuarioConsultaPort consulta,
    IUsuarioPort usuarios,
    IGestionUsuariosPort gestion) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<UsuarioListadoDto>> Listar(
        string? busqueda = null, string? rol = null, bool? activo = null, int pagina = 1, int tamanoPagina = 10)
    {
        return Ok(consulta.ListarFiltrados(NormalizarBusqueda(busqueda), NormalizarRol(rol), activo,
            Math.Clamp(pagina, 1, 10000), Math.Clamp(tamanoPagina, 1, 100)));
    }

    [HttpGet("conteo")]
    public ActionResult<int> Contar(string? busqueda = null, string? rol = null, bool? activo = null) =>
        Ok(consulta.ContarFiltrados(NormalizarBusqueda(busqueda), NormalizarRol(rol), activo));

    [HttpGet("resumen")]
    public ActionResult<ResumenUsuarios> Resumen() => Ok(new ResumenUsuarios(
        consulta.ContarTotal(), consulta.ContarUsuariosActivos(), consulta.ContarAdministradoresActivosListado()));

    [HttpGet("{id:int}")]
    public ActionResult<UsuarioDto> Obtener(int id)
    {
        var usuario = usuarios.ObtenerPorId(id);
        return usuario is null
            ? NotFound(new ErrorApi("Usuario no encontrado."))
            : Ok(new UsuarioDto(usuario.Id, usuario.Username, usuario.Email, usuario.Rol, usuario.Activo));
    }

    [HttpPost]
    public IActionResult Crear(CrearInvitacionSolicitud solicitud, CancellationToken cancelacion)
    {
        // Nunca se devuelve el token ni el hash de la contraseña al Frontend.
        gestion.CrearInvitacion(string.Empty, solicitud.Email, solicitud.Rol, solicitud.Password, cancelacion);
        return NoContent();
    }

    [HttpPut("{id:int}")]
    public IActionResult Actualizar(int id, ActualizarUsuarioSolicitud solicitud) =>
        gestion.ActualizarUsuario(id, solicitud.Email, solicitud.Rol, solicitud.Activo, out string? error)
            ? NoContent()
            : BadRequest(new ErrorApi(error ?? "No se pudo actualizar el usuario."));

    [HttpPost("{id:int}/desactivar")]
    public IActionResult Desactivar(int id) =>
        gestion.DesactivarUsuario(id, out string? error)
            ? NoContent()
            : BadRequest(new ErrorApi(error ?? "No se pudo desactivar el usuario."));

    [HttpPost("{id:int}/reactivar")]
    public IActionResult Reactivar(int id) =>
        gestion.ReactivarUsuario(id, out string? error)
            ? NoContent()
            : BadRequest(new ErrorApi(error ?? "No se pudo reactivar el usuario."));

    private static string? NormalizarBusqueda(string? busqueda) =>
        string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim();

    private static string? NormalizarRol(string? rol) => rol is "Administrador" or "Recepcionista" ? rol : null;
}
