using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Application.Exceptions;
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
            await _reenvio.SolicitarReenvioAsync(
                Email,
                cancellationToken);
        }
        catch (ConfiguracionRegistroException ex)
        {
            _logger.LogError(
                "Configuración de reenvío inválida. Clave: {Clave}; Solicitud HTTP: {Id}",
                ex.Clave, HttpContext.TraceIdentifier);
        }
        catch (EnvioVerificacionException ex)
        {
            _logger.LogWarning(
                "Aceptación SMTP del reenvío no confirmada. Etapa: {Etapa}; " +
                "Tipo: {Tipo}; Código: {Codigo}; Solicitud HTTP: {Id}",
                ex.Etapa, ex.TipoError, ex.CodigoSmtp, HttpContext.TraceIdentifier);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                "Fallo del reenvío. Tipo: {Tipo}; Pila: {Pila}; Solicitud HTTP: {Id}",
                ex.GetType().FullName, ex.StackTrace, HttpContext.TraceIdentifier);
        }

        SolicitudRecibida = true;
        Email = string.Empty;
        ModelState.Clear();

        return Page();
    }
}
