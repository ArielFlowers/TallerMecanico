using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace TallerMecanico.Pages.Auth;

[EnableRateLimiting("RegistroRateLimit")]
public sealed class RegistroModel : PageModel
{
    [BindProperty]
    public RegistroInput Input { get; set; } = new();

    public bool SolicitudRecibida => false;

    // El alta de usuarios se realiza exclusivamente desde Administración.
    public IActionResult OnGet() => RedirectToPage("/Auth/Login");
    public Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(NotFound());
}
