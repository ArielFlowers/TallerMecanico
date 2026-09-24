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

    public void OnGet()
    {
        CargarMecanicos();
    }

    public IActionResult OnPostCrear()
    {
        var errores = _mecanicoService.Crear(MecanicoInput);

        if (errores.Count > 0)
        {
            AgregarErroresAlModelState(errores);
            FormularioActivo = FormularioCrear;
            CargarMecanicos();
            return Page();
        }

        MensajeExito = "Mecánico registrado correctamente.";
        return RedirigirAlListado();
    }

    public IActionResult OnPostActualizar()
    {
        var (actualizado, errores) =
            _mecanicoService.Actualizar(MecanicoId, MecanicoInput);

        if (errores.Count > 0)
        {
            AgregarErroresAlModelState(errores);
            FormularioActivo = FormularioEditar;
            CargarMecanicos();
            return Page();
        }

        if (!actualizado)
        {
            ModelState.AddModelError(string.Empty, MensajeRegistroNoEncontrado);
            FormularioActivo = FormularioEditar;
            CargarMecanicos();
            return Page();
        }

        MensajeExito = "Mecánico actualizado correctamente.";
        return RedirigirAlListado();
    }

    public IActionResult OnPostEliminar()
    {
        var eliminado = _mecanicoService.Eliminar(MecanicoId);

        if (!eliminado)
        {
            MensajeError = MensajeRegistroNoEncontrado;
            return RedirigirAlListado();
        }

        MensajeExito = "Mecánico eliminado correctamente.";
        return RedirigirAlListado();
    }

    private void CargarMecanicos()
    {
        Mecanicos = _mecanicoService.Obtener(TerminoBusqueda);
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
