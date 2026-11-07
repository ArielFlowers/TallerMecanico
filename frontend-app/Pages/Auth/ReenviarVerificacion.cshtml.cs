using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("ReenvioRateLimit")]
public sealed class ReenviarVerificacionModel : PageModel
{
    private readonly UsuariosApi _usuarios;
    private readonly ILogger<ReenviarVerificacionModel> _logger;

    public ReenviarVerificacionModel(
        UsuariosApi usuarios,
        ILogger<ReenviarVerificacionModel> logger)
    {
        _usuarios = usuarios;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "Ingresa tu correo electrónico.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    public bool SolicitudRecibida { get; private set; }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _usuarios.ReenviarVerificacionAsync(Email, cancellationToken);
        }
        catch (Exception error) when (
            error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Se conserva la misma respuesta pública sin revelar si el correo existe.
            _logger.LogWarning("No se pudo solicitar el reenvío al servicio de usuarios. Tipo: {Tipo}; Solicitud HTTP: {Id}",
                error.GetType().Name, HttpContext.TraceIdentifier);
        }

        SolicitudRecibida = true;
        Email = string.Empty;
        ModelState.Clear();

        return Page();
    }
}
