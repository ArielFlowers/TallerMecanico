using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Auth;

[Authorize]
public class LogoutModel(UsuariosApi usuarios, ILogger<LogoutModel> logger) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var resultado = await usuarios.CerrarSesionAsync(HttpContext.RequestAborted);
            if (!resultado.Exito)
                logger.LogWarning("El servicio de usuarios no pudo registrar el cierre de sesión. Estado HTTP: {Estado}",
                    (int)resultado.Codigo);
        }
        catch (Exception error)
        {
            logger.LogWarning("No se pudo registrar el cierre de sesión en el servicio de usuarios. Tipo: {Tipo}",
                error.GetType().Name);
        }
        finally
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
        }
        return RedirectToPage("/Auth/Login");
    }
}
