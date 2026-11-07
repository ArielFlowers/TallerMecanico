using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Fachadas;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("RegistroRateLimit")]
public sealed class RegistroModel : PageModel
{
    private readonly RegistroFacade _registro;
    private readonly ILogger<RegistroModel> _logger;

    public RegistroModel(
        RegistroFacade registro,
        ILogger<RegistroModel> logger)
    {
        _registro = registro;
        _logger = logger;
    }

    [BindProperty]
    public RegistroInput Input { get; set; } = new();

    public bool SolicitudRecibida { get; private set; }

    // El alta de usuarios se realiza exclusivamente
    // desde Administracion/Usuarios/Crear.

    public IActionResult OnGet()
    {
        return RedirectToPage("/Auth/Login");
    }

    public IActionResult OnPostAsync(
        CancellationToken cancellationToken)
    {
        return NotFound();
    }
    private IActionResult MostrarSolicitudRecibida()
    {
        // Mismo resultado público para alta, duplicado o envío no confirmado.
        SolicitudRecibida = true;
        Input = new RegistroInput();
        ModelState.Clear();
        return Page();
    }
}
