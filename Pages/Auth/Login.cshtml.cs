using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using ILoginAuthenticationService =
    TallerMecanico.Application.Ports.IAuthenticationService;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("LoginRateLimit")]
public class LoginModel : PageModel
{
    private readonly ILoginAuthenticationService _authenticationService;
    private readonly IAuditoriaPort _auditoriaPort;

    public LoginModel(
    ILoginAuthenticationService authenticationService,
    IAuditoriaPort auditoriaPort)
    {
        _authenticationService = authenticationService;
        _auditoriaPort = auditoriaPort;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var usuario = _authenticationService.ValidarCredenciales(
            Input.Username,
            Input.Password);

        if (usuario is null ||
            (usuario.Rol != "Administrador" &&
             usuario.Rol != "Recepcionista"))
        {
            _auditoriaPort.Registrar(new RegistroAuditoria
            {
                UsuarioId = null,
                Username = null,
                Accion = "LOGIN_FALLIDO",
                Entidad = "Usuario",
                EntidadId = null,
                Fecha = DateTime.UtcNow
            });

            ModelState.AddModelError(
                string.Empty,
                "Usuario o contraseña incorrectos.");

            return Page();
        }


        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                usuario.Id.ToString()),

            new(
                ClaimTypes.Name,
                usuario.Username),

            new(
                ClaimTypes.Role,
                usuario.Rol)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        HttpContext.Session.Clear();

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            });

        HttpContext.Session.SetString(
            "Username",
            usuario.Username);

        try
        {
            _auditoriaPort.Registrar(new RegistroAuditoria
            {
                UsuarioId = usuario.Id,
                Username = usuario.Username,
                Accion = "LOGIN_EXITOSO",
                Entidad = "Usuario",
                EntidadId = usuario.Id.ToString(),
                Fecha = DateTime.UtcNow
            });
        }
        catch
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();

            throw;
        }

        return RedirectToPage("/Index");

    }
}
