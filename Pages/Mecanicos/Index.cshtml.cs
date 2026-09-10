using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Mecanicos;

public class IndexModel : PageModel
{
    private const string FormularioCrear = "crear";
    private const string FormularioEditar = "editar";
    private const string MensajeRegistroNoEncontrado =
        "El mecánico seleccionado ya no existe.";

    private readonly MecanicoService _mecanicoService;

    public IndexModel(MecanicoService mecanicoService)
    {
        _mecanicoService = mecanicoService;
    }

    public IReadOnlyList<Mecanico> Mecanicos { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }

    [BindProperty]
    public MecanicoInputModel MecanicoInput { get; set; } = new();

    [BindProperty]
    public int MecanicoId { get; set; }

    public string? FormularioActivo { get; private set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        await CargarMecanicosAsync();
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        var (_, errores) = await _mecanicoService.CrearAsync(MecanicoInput);

        if (errores.Count > 0)
        {
            AgregarErroresAlModelState(errores);
            FormularioActivo = FormularioCrear;
            await CargarMecanicosAsync();
            return Page();
        }

        MensajeExito = "Mecánico registrado correctamente.";
        return RedirigirAlListado();
    }

    public async Task<IActionResult> OnPostActualizarAsync()
    {
        var (actualizado, errores) =
            await _mecanicoService.ActualizarAsync(MecanicoId, MecanicoInput);

        if (errores.Count > 0)
        {
            AgregarErroresAlModelState(errores);
            FormularioActivo = FormularioEditar;
            await CargarMecanicosAsync();
            return Page();
        }

        if (!actualizado)
        {
            ModelState.AddModelError(string.Empty, MensajeRegistroNoEncontrado);
            FormularioActivo = FormularioEditar;
            await CargarMecanicosAsync();
            return Page();
        }

        MensajeExito = "Mecánico actualizado correctamente.";
        return RedirigirAlListado();
    }

    public async Task<IActionResult> OnPostEliminarAsync()
    {
        var eliminado = await _mecanicoService.EliminarAsync(MecanicoId);

        if (!eliminado)
        {
            MensajeError = MensajeRegistroNoEncontrado;
            return RedirigirAlListado();
        }

        MensajeExito = "Mecánico eliminado correctamente.";
        return RedirigirAlListado();
    }

    private async Task CargarMecanicosAsync()
    {
        Mecanicos = await _mecanicoService.ObtenerAsync(TerminoBusqueda);
    }

    private void AgregarErroresAlModelState(
        IReadOnlyDictionary<string, string> errores)
    {
        foreach (var (campo, mensaje) in errores)
        {
            var claveModelState = $"{nameof(MecanicoInput)}.{campo}";
            ModelState.AddModelError(claveModelState, mensaje);
        }
    }

    private IActionResult RedirigirAlListado()
    {
        return RedirectToPage(new { terminoBusqueda = TerminoBusqueda });
    }
}
