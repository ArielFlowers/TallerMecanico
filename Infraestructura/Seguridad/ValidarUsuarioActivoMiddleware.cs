using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Seguridad;

public sealed class ValidarUsuarioActivoMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidarUsuarioActivoMiddleware> _logger;

    public ValidarUsuarioActivoMiddleware(
        RequestDelegate next,
        ILogger<ValidarUsuarioActivoMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUsuarioPort usuarioPort)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            string? userIdClaim = context.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(userIdClaim, out int userId) && userId > 0)
            {
                Usuario? usuario = usuarioPort.ObtenerPorId(userId);

                if (usuario is null || !usuario.Activo)
                {
                    _logger.LogWarning(
                        "Usuario desactivado o inexistente intento de acceso: {UserId}",
                        userId);

                    await context.SignOutAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);

                    context.Session.Clear();

                    context.Response.Redirect("/Auth/Login?expired=1");
                    return;
                }
            }
        }

        await _next(context);
    }
}

public static class ValidarUsuarioActivoMiddlewareExtensions
{
    public static IApplicationBuilder UseValidarUsuarioActivo(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ValidarUsuarioActivoMiddleware>();
    }
}