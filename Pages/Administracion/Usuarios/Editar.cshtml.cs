using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Pages.Administracion.Usuarios;

namespace TallerMecanico.Pages.Administracion.Usuarios;

[Authorize(Policy = "SoloAdministrador")]
public sealed class EditarModel : PageModel
{
    private readonly IUsuarioAdministracionPort _usuarioPort;
    private readonly UsuarioAdministracionService _usuarioAdministracion;

    public EditarModel(
        IUsuarioAdministracionPort usuarioPort,
        UsuarioAdministracionService usuarioAdministracion)
    {
        _usuarioPort = usuarioPort;
        _usuarioAdministracion = usuarioAdministracion;
    }

    public Usuario? Usuario { get; private set; }

    [BindProperty]
    public UsuarioEditInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Usuario = _usuarioPort.ObtenerPorId(id);

        if (Usuario is null)
        {
            return NotFound();
        }

        Input = new UsuarioEditInput
        {
            Email = Usuario.Email ?? string.Empty,
            Rol = Usuario.Rol,
            Activo = Usuario.Activo
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken = default)
    {
        Usuario = _usuarioPort.ObtenerPorId(id);

        if (Usuario is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        bool resultado = _usuarioAdministracion.ActualizarUsuario(
            id,
            Input.Email,
            Input.Rol,
            Input.Activo,
            out string? errorMessage);

        if (!resultado)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Error al actualizar el usuario.");
            return Page();
        }

        TempData["MensajeExito"] = "Usuario actualizado correctamente.";
        return RedirectToPage("./Index");
    }
}