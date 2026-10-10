
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

namespace TallerMecanico.Pages.Auth;

[Authorize]
public class LogoutModel : PageModel
{

    private readonly IAuditoriaPort _auditoriaPort;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(
        IAuditoriaPort auditoriaPort,
        ILogger<LogoutModel> logger)
    {
        _auditoriaPort = auditoriaPort;
        _logger = logger;
    }
    public IActionResult OnGet()
    {
        return RedirectToPage("/Index");
    }


    public async Task<IActionResult> OnPostAsync()
    {
        int? usuarioId = null;

        if (int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int id))
        {
            usuarioId = id;
        }

        try
        {
            _auditoriaPort.Registrar(new RegistroAuditoria
            {
                UsuarioId = usuarioId,
                Username = User.Identity?.Name,
                Accion = "LOGOUT",
                Entidad = "Usuario",
                EntidadId = usuarioId?.ToString(),
                Fecha = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "No se pudo registrar el evento de auditoría de cierre de sesión.");
        }
        finally
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();
        }

        return RedirectToPage("/Auth/Login");
    }

}
