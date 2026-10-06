using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Clientes;

public class IndexModel : PageModel
{
    private const string FormularioCrear = "crear";
    private const string FormularioEditar = "editar";

    private const string MensajeRegistroNoEncontrado =
        "El cliente seleccionado ya no existe.";

    private readonly ClienteService _clienteService;

    public IndexModel(ClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    public IReadOnlyList<Cliente> Clientes { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }

    [BindProperty]
    public ClienteFormViewModel ClienteInput { get; set; } = new();

    [BindProperty]
    public int ClienteId { get; set; }

    public string? FormularioActivo { get; private set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public void OnGet()
    {
        CargarClientes();
    }

    public IActionResult OnPostCrear()
    {
        var errores =
            _clienteService.Crear(ClienteInput);

        if (errores.Count > 0)
        {
            AgregarErroresAlModelState(errores);

            FormularioActivo = FormularioCrear;

            CargarClientes();

            return Page();
        }

        MensajeExito =
            "Cliente registrado correctamente.";

        return RedirigirAlListado();
    }

    public IActionResult OnPostActualizar()
    {
        var (actualizado, errores) =
            _clienteService.Actualizar(
                ClienteId,
                ClienteInput);

        if (errores.Count > 0)
        {
            AgregarErroresAlModelState(errores);

            FormularioActivo = FormularioEditar;

            CargarClientes();

            return Page();
        }

        if (!actualizado)
        {
            ModelState.AddModelError(
                string.Empty,
                MensajeRegistroNoEncontrado);

            FormularioActivo =
                FormularioEditar;

            CargarClientes();

            return Page();
        }

        MensajeExito =
            "Cliente actualizado correctamente.";

        return RedirigirAlListado();
    }

    public IActionResult OnPostEliminar()
    {
        bool eliminado =
            _clienteService.Eliminar(ClienteId);

        if (!eliminado)
        {
            MensajeError =
                MensajeRegistroNoEncontrado;

            return RedirigirAlListado();
        }

        MensajeExito =
            "Cliente eliminado correctamente.";

        return RedirigirAlListado();
    }

    private void CargarClientes()
    {
        Clientes =
            _clienteService.Obtener(
                TerminoBusqueda);
    }

    private void AgregarErroresAlModelState(
        IReadOnlyDictionary<string, string> errores)
    {
        foreach (var (campo, mensaje) in errores)
        {
            string claveModelState =
                $"{nameof(ClienteInput)}.{campo}";

            ModelState.AddModelError(
                claveModelState,
                mensaje);
        }
    }

    private IActionResult RedirigirAlListado()
    {
        return RedirectToPage(
            new
            {
                terminoBusqueda =
                    TerminoBusqueda
            });
    }
}