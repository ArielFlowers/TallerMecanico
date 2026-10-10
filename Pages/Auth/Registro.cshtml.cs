using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("RegistroRateLimit")]
public sealed class RegistroModel : PageModel
{
    private readonly RegistroConEmailService _registro;
    private readonly ILogger<RegistroModel> _logger;

    public RegistroModel(
        RegistroConEmailService registro,
        ILogger<RegistroModel> logger)
    {
        _registro = registro;
        _logger = logger;
    }

    [BindProperty]
    public RegistroInput Input { get; set; } = new();

    public bool SolicitudRecibida { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _registro.SolicitarRegistroAsync(
                Input.Username,
                Input.Email,
                Input.Password,
                cancellationToken);

            SolicitudRecibida = true;

            Input = new RegistroInput();

            ModelState.Clear();

            return Page();
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Los datos ingresados no cumplen los requisitos.");
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(
                string.Empty,
                "No se pudo completar la solicitud. " +
                "Si ya te registraste, revisa tu correo o " +
                "solicita un nuevo enlace cuando este disponible.");
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Error al procesar la solicitud de registro. Tipo: {TipoError}",
                ex.GetType().Name);

            ModelState.AddModelError(
                string.Empty,
                "No se pudo completar la solicitud en este momento. " +
                "Si recibes un correo de verificacion, utiliza ese enlace.");
        }

        return Page();
    }
}