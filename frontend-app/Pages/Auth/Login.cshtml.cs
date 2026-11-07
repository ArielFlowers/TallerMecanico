using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("LoginRateLimit")]
public class LoginModel(UsuariosApi usuarios) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? MensajeExpirado { get; private set; }

    public IActionResult OnGet(string? expired)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        if (expired == "1")
            MensajeExpirado = "Su sesión ha expirado porque su cuenta cambió o fue desactivada. Inicie sesión nuevamente o contacte al administrador.";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            var usuario = await usuarios.IniciarSesionAsync(Input.Username, Input.Password, HttpContext.RequestAborted);
            if (usuario is null || usuario.Id <= 0 || string.IsNullOrWhiteSpace(usuario.Username) ||
                usuario.Rol is not ("Administrador" or "Recepcionista"))
            {
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                return Page();
            }
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Username),
                new Claim(ClaimTypes.Role, usuario.Rol)
            ], CookieAuthenticationDefaults.AuthenticationScheme));
            HttpContext.Session.Clear();
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
            HttpContext.Session.SetString("Username", usuario.Username);
            return RedirectToPage("/Index");
        }
        catch (ValidationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }
        catch (Exception error) when (error is HttpRequestException ||
            error is TaskCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "El servicio de usuarios no está disponible. Intenta nuevamente.");
        }
        return Page();
    }
}
