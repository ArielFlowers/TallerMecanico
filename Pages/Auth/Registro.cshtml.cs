using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using TallerMecanico.Application.Exceptions;
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

            return MostrarSolicitudRecibida();
        }
        catch (ConfiguracionRegistroException ex)
        {
            _logger.LogError(
                "Configuración de registro inválida. Clave: {Clave}; Solicitud HTTP: {Id}",
                ex.Clave, HttpContext.TraceIdentifier);

            ModelState.AddModelError(
                string.Empty,
                "El registro no está disponible en este momento. Intenta más tarde.");
        }
        catch (SolicitudRegistroDuplicadaException ex)
        {
            _logger.LogInformation(
                "Datos de registro no disponibles. Campo: {Campo}; Solicitud HTTP: {Id}",
                ex.Campo, HttpContext.TraceIdentifier);

            return MostrarSolicitudRecibida();
        }
        catch (SolicitudGuardadaCorreoPendienteException ex)
        {
            _logger.LogWarning(
                "Solicitud guardada; aceptación SMTP no confirmada. Etapa: {Etapa}; " +
                "Tipo: {Tipo}; Código: {Codigo}; Solicitud HTTP: {Id}",
                ex.Etapa, ex.TipoError, ex.CodigoSmtp, HttpContext.TraceIdentifier);

            return MostrarSolicitudRecibida();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                "Validación de registro rechazada. Campo: {Campo}; Solicitud HTTP: {Id}",
                ex.ParamName, HttpContext.TraceIdentifier);

            ModelState.AddModelError(
                string.Empty,
                "Los datos ingresados no cumplen los requisitos.");
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                "Fallo al crear la solicitud de registro. Tipo: {Tipo}; " +
                "Pila: {Pila}; Solicitud HTTP: {Id}",
                ex.GetType().FullName, ex.StackTrace, HttpContext.TraceIdentifier);

            ModelState.AddModelError(
                string.Empty,
                "No se pudo completar la solicitud en este momento. " +
                "Si recibes un correo de verificacion, utiliza ese enlace.");
        }

        return Page();
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
