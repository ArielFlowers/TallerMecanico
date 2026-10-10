using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Pages.Clientes;

public class IndexModel : PageModel
{
    private readonly ClienteService _clienteService;
    private readonly IBorradorVehiculoPort _borradores;

    public IndexModel(
        ClienteService clienteService,
        IBorradorVehiculoPort borradores)
    {
        _clienteService =
            clienteService;
        _borradores = borradores;
    }

    [BindProperty]
    public ClienteFormViewModel ClienteInput { get; set; } =
        new();

    [BindProperty]
    public int ClienteId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? BorradorVehiculoId { get; set; }

    public IReadOnlyList<Cliente> Clientes { get; private set; } =
        [];

    public string? FormularioActivo { get; private set; }

    public string? MensajeExito { get; private set; }

    public string? MensajeError { get; private set; }

    public void OnGet()
    {
        CargarMensajes();
        CargarClientes();
        if (BorradorVehiculoId.HasValue && _borradores.Obtener(BorradorVehiculoId.Value) is not null)
            FormularioActivo = "crear";
    }

    public IActionResult OnPostCrear()
    {
        if (BorradorVehiculoId.HasValue && _borradores.Obtener(BorradorVehiculoId.Value) is null)
        {
            ModelState.AddModelError(string.Empty, "El borrador del vehículo venció. Regresa a Vehículos para continuar.");
        }
        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "crear";

            CargarClientes();

            return Page();
        }

        var resultado =
            _clienteService.Crear(
                ClienteInput);

        ClienteInput =
            resultado.Formulario;

        ModelState.Clear();

        AgregarErrores(
            resultado.Errores);

        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "crear";

            CargarClientes();

            return Page();
        }

        TempData["ClienteExito"] =
            "Cliente registrado correctamente.";

        if (BorradorVehiculoId.HasValue &&
            _borradores.AsignarCliente(BorradorVehiculoId.Value, ClienteInput.Id))
        {
            return RedirectToPage("/Vehiculos/Index", "Retomar", new { borradorId = BorradorVehiculoId.Value });
        }

        return RedirectToPage(
            new
            {
                TerminoBusqueda
            });
    }

    public IActionResult OnPostActualizar()
    {
        ClienteInput.Id =
            ClienteId;

        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "editar";

            CargarClientes();

            return Page();
        }

        var resultado =
            _clienteService.Actualizar(
                ClienteInput);

        ClienteInput =
            resultado.Formulario;

        ModelState.Clear();

        AgregarErrores(
            resultado.Errores);

        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "editar";

            ClienteId =
                ClienteInput.Id;

            CargarClientes();

            return Page();
        }

        TempData["ClienteExito"] =
            "Cliente actualizado correctamente.";

        return RedirectToPage(
            new
            {
                TerminoBusqueda
            });
    }

    public IActionResult OnPostEliminar()
    {
        string? error =
            _clienteService.Eliminar(
                ClienteId);

        if (!string.IsNullOrWhiteSpace(
                error))
        {
            TempData["ClienteError"] =
                error;

            return RedirectToPage(
                new
                {
                    TerminoBusqueda
                });
        }

        TempData["ClienteExito"] =
            "Cliente eliminado correctamente.";

        return RedirectToPage(
            new
            {
                TerminoBusqueda
            });
    }

    private void CargarClientes()
    {
        if (string.IsNullOrWhiteSpace(
                TerminoBusqueda))
        {
            Clientes =
                _clienteService.Obtener();

            return;
        }

        Clientes =
            _clienteService.Buscar(
                TerminoBusqueda);
    }

    private void AgregarErrores(
        IReadOnlyDictionary<string, string> errores)
    {
        foreach (var error in errores)
        {
            string campo =
                string.IsNullOrWhiteSpace(
                    error.Key)
                    ? string.Empty
                    : $"ClienteInput.{error.Key}";

            ModelState.AddModelError(
                campo,
                error.Value);
        }
    }

    private void CargarMensajes()
    {
        MensajeExito =
            TempData["ClienteExito"] as string;

        MensajeError =
            TempData["ClienteError"] as string;
    }
}
