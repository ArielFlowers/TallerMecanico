using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TallerMecanico.Api;

namespace TallerMecanico.Seguridad;

public sealed class ValidarUsuarioActivoMiddleware(
    RequestDelegate next, ILogger<ValidarUsuarioActivoMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, UsuariosApi usuarios)
    {
        // El cierre local debe poder ejecutarse incluso si la API está fuera de servicio.
        if (context.User.Identity?.IsAuthenticated != true ||
            HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Equals(new PathString("/Auth/Logout")))
        {
            await next(context);
            return;
        }

        string[] identificadores = context.User.FindAll(ClaimTypes.NameIdentifier)
            .Select(claim => claim.Value).ToArray();
        string[] roles = context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        int userId = 0;
        bool identidadValida = identificadores.Length == 1 &&
            int.TryParse(identificadores[0], out userId) && userId > 0 && roles.Length == 1 &&
            roles[0] is "Administrador" or "Recepcionista";

        if (identidadValida)
        {
            bool cuentaVigente;
            try
            {
                var estado = await usuarios.ObtenerEstadoAsync(userId, context.RequestAborted);
                cuentaVigente = estado?.Activo == true &&
                    string.Equals(roles[0], estado.Rol, StringComparison.Ordinal);
            }
            catch (Exception error) when (error is HttpRequestException ||
                error is TaskCanceledException && !context.RequestAborted.IsCancellationRequested)
            {
                logger.LogWarning("No se pudo comprobar la cuenta en el servicio de usuarios. Tipo: {Tipo}",
                    error.GetType().Name);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("El servicio de usuarios no está disponible. Intenta nuevamente.");
                return;
            }
            if (cuentaVigente)
            {
                await next(context);
                return;
            }
        }

        logger.LogWarning("Sesión revocada por cambios en la cuenta o identidad inválida: {UserId}", userId);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Session.Clear();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        context.Response.Redirect("/Auth/Login?expired=1");
    }
}

public static class ValidarUsuarioActivoMiddlewareExtensions
{
    public static IApplicationBuilder UseValidarUsuarioActivo(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ValidarUsuarioActivoMiddleware>();
}
