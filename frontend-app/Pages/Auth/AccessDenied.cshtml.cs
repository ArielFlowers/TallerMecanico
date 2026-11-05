
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TallerMecanico.Pages.Auth;

[Authorize]
public class AccessDeniedModel : PageModel
{
    public void OnGet()
    {
    }
}
