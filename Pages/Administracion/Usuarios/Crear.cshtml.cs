using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Pages.Administracion.Usuarios;

namespace TallerMecanico.Pages.Administracion.Usuarios;

[Authorize(Policy = "SoloAdministrador")]
public sealed class CrearModel : PageModel
{
    private readonly UsuarioAdministracionService _usuarioAdministracion;

    public CrearModel(UsuarioAdministracionService usuarioAdministracion)
    {
        _usuarioAdministracion = usuarioAdministracion;
    }

    [BindProperty]
    public UsuarioCreateInput Input { get; set; } = new();

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await Task.Run(() =>
                _usuarioAdministracion.CrearInvitacion(
                    Input.Username,
                    Input.Email,
                    Input.Rol,
                    cancellationToken),
                cancellationToken);

            TempData["MensajeExito"] = $"Se ha enviado una invitación a {Input.Email} para crear su cuenta.";
            return RedirectToPage("./Index");
        }
        catch (SolicitudRegistroDuplicadaException ex)
        {
            string campo = ex.Campo switch
            {
                nameof(SolicitudRegistro.Username) => nameof(Input.Username),
                nameof(SolicitudRegistro.Email) => nameof(Input.Email),
                _ => string.Empty
            };

            ModelState.AddModelError(campo, "Ya existe un usuario o solicitud pendiente con ese valor.");
            return Page();
        }
        catch (SolicitudGuardadaCorreoPendienteException)
        {
            ModelState.AddModelError(string.Empty,
                "La invitación se guardó pero no se pudo enviar el correo. Intente reenviar la verificación más tarde.");
            return Page();
        }
        catch (ConfiguracionRegistroException)
        {
            ModelState.AddModelError(string.Empty,
                "Error de configuración del servidor de correo. Contacte al administrador del sistema.");
            return Page();
        }
        catch (EnvioVerificacionException)
        {
            ModelState.AddModelError(string.Empty,
                "No se pudo enviar el correo de invitación. La solicitud se guardó para reintento posterior.");
            return Page();
        }
        catch (OperationCanceledException)
        {
            ModelState.AddModelError(string.Empty, "La operación fue cancelada.");
            return Page();
        }
    }
}