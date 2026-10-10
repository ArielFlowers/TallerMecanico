using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("ReenvioRateLimit")]
public sealed class ReenviarVerificacionModel : PageModel
{
    private readonly ReenvioVerificacionService _reenvio;
    private readonly ILogger<ReenviarVerificacionModel> _logger;

    public ReenviarVerificacionModel(
        ReenvioVerificacionService reenvio,
        ILogger<ReenviarVerificacionModel> logger)
    {
        _reenvio = reenvio;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "Ingresa tu correo electronico.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo valido.")]
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
            await _reenvio.SolicitarReenvioAsync(
                Email,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException)
        {
            _logger.LogError(
                "No se pudo completar el reenvio. Tipo: {Tipo}",
                ex.GetType().Name);
        }

        SolicitudRecibida = true;
        Email = string.Empty;
        ModelState.Clear();

        return Page();
    }
}