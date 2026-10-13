
using System.Security.Claims;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Infraestructura.Identity;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal =>
        _httpContextAccessor.HttpContext?.User;

    public bool EstaAutenticado =>
        Principal?.Identity?.IsAuthenticated == true;

    public int? UsuarioId
    {
        get
        {
            if (!EstaAutenticado)
                return null;

            string? valor = Principal?
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(valor, out int id) && id > 0
                ? id
                : null;
        }
    }

    public string? Username =>
        EstaAutenticado
            ? Principal?.FindFirstValue(ClaimTypes.Name)
            : null;

    public string? Rol =>
        EstaAutenticado
            ? Principal?.FindFirstValue(ClaimTypes.Role)
            : null;
}
