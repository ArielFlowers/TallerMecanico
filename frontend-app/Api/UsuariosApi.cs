using System.Net;
using System.Net.Http.Json;
using TallerMecanico.Contratos.Usuarios;

namespace TallerMecanico.Api;

// Cliente del Servicio 2: las cookies y los formularios permanecen en el Frontend.
public sealed class UsuariosApi(HttpClient http) : ApiBase(http)
{
    public async Task<UsuarioSesionDto?> IniciarSesionAsync(string username, string password,
        CancellationToken cancelacion = default)
    {
        using var respuesta = await Http.PostAsJsonAsync("api/auth/login",
            new CredencialesLogin(username, password), cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized) return null;
        await ValidarAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<UsuarioSesionDto>(cancelacion);
    }

    public Task<ResultadoApi> CerrarSesionAsync(CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Post, "api/auth/logout", cancelacion: cancelacion);

    public Task<EstadoUsuarioDto?> ObtenerEstadoAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<EstadoUsuarioDto>($"api/auth/estado/{id}", cancelacion);

    public Task<ResultadoApi> CambiarContrasenaAsync(CambiarContrasenaSolicitud solicitud,
        CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Post, "api/auth/cambiar-contrasena", solicitud, cancelacion);

    public Task<ResultadoApi> VerificarEmailAsync(string token, CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Post, "api/auth/verificar-email", new TokenSolicitud(token), cancelacion);

    public Task<ResultadoApi> ActivarInvitacionAsync(string token, CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Post, "api/auth/activar-invitacion", new TokenSolicitud(token), cancelacion);

    public Task<ResultadoApi> ReenviarVerificacionAsync(string email, CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Post, "api/auth/reenviar-verificacion", new EmailSolicitud(email), cancelacion);

    public Task<IReadOnlyList<UsuarioListadoDto>> ListarAsync(string? busqueda, string? rol, bool? activo,
        int pagina, int tamanoPagina, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<UsuarioListadoDto>>(ConFiltros("api/usuarios",
            ("busqueda", busqueda), ("rol", rol), ("activo", activo),
            ("pagina", pagina), ("tamanoPagina", tamanoPagina)), cancelacion);

    public Task<int> ContarAsync(string? busqueda, string? rol, bool? activo, CancellationToken cancelacion = default) =>
        LeerAsync<int>(ConFiltros("api/usuarios/conteo",
            ("busqueda", busqueda), ("rol", rol), ("activo", activo)), cancelacion);

    public Task<ResumenUsuarios> ObtenerResumenAsync(CancellationToken cancelacion = default) =>
        LeerAsync<ResumenUsuarios>("api/usuarios/resumen", cancelacion);

    public Task<UsuarioDto?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<UsuarioDto>($"api/usuarios/{id}", cancelacion);

    public Task<ResultadoApi> CrearInvitacionAsync(CrearInvitacionSolicitud solicitud,
        CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Post, "api/usuarios", solicitud, cancelacion);

    public Task<ResultadoApi> ActualizarAsync(int id, ActualizarUsuarioSolicitud solicitud,
        CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Put, $"api/usuarios/{id}", solicitud, cancelacion);

    public Task<ResultadoApi> DesactivarAsync(int id, CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Post, $"api/usuarios/{id}/desactivar", cancelacion: cancelacion);

    public Task<ResultadoApi> ReactivarAsync(int id, CancellationToken cancelacion = default) =>
        EnviarUsuarioAsync(HttpMethod.Post, $"api/usuarios/{id}/reactivar", cancelacion: cancelacion);

    private Task<ResultadoApi> EnviarUsuarioAsync(HttpMethod metodo, string ruta,
        object? cuerpo = null, CancellationToken cancelacion = default) =>
        EnviarAsync(metodo, ruta, cuerpo, cancelacion, servicioNoDisponibleComoMensaje: true);
}
