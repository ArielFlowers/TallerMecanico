using System.Security.Claims;
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
            string[] identificadores = context.User.FindAll(ClaimTypes.NameIdentifier)
                .Select(claim => claim.Value)
                .ToArray();

            int userId = 0;
            Usuario? usuario = identificadores.Length == 1 &&
                int.TryParse(identificadores[0], out userId) && userId > 0
                    ? usuarioPort.ObtenerPorId(userId)
                    : null;

            string[] roles = context.User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToArray();

            bool rolVigente = usuario is not null && roles.Length == 1 &&
                string.Equals(roles[0], usuario.Rol, StringComparison.Ordinal);

            if (usuario is null || !usuario.Activo || !rolVigente)
            {
                _logger.LogWarning(
                    "Sesión revocada por cambios en la cuenta o identidad inválida: {UserId}",
                    userId);

                await context.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                context.Session.Clear();
                context.User = new ClaimsPrincipal(new ClaimsIdentity());

                context.Response.Redirect("/Auth/Login?expired=1");
                return;
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
