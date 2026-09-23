using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TallerMecanico.Pages.Servicios;

public class IndexModel : PageModel
{
    [TempData]
    public string? MensajeExito { get; set; }
}
