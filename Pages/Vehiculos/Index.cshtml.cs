using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Pages.Vehiculos;

public class IndexModel : PageModel
{
    private readonly VehiculoService _vehiculoService;
    private readonly ClienteService _clienteService;
    private readonly IBorradorVehiculoPort _borradores;

    public IndexModel(
        VehiculoService vehiculoService,
        ClienteService clienteService,
        IBorradorVehiculoPort borradores)
    {
        _vehiculoService =
            vehiculoService;

        _clienteService =
            clienteService;
        _borradores = borradores;
    }

    [BindProperty]
    public VehiculoFormViewModel Formulario { get; set; } =
        new();

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    public List<Vehiculo> Vehiculos { get; private set; } =
        [];

    public IReadOnlyList<Cliente> Clientes { get; private set; } =
        [];

    public string? ModalAbierto { get; private set; }

    public string? ModeloAnterior { get; private set; }

    [BindProperty]
    public Guid? BorradorId { get; set; }

    public string? MensajeError => TempData["VehiculoError"] as string;

    public void OnGet()
    {
        CargarDatos();
    }

    public IActionResult OnGetBuscar()
    {
        CargarDatos();

        return Page();
    }

    public IActionResult OnPostCreate()
    {
        return GuardarFormulario(
            actualizar: false);
    }

    public IActionResult OnPostUpdate()
    {
        return GuardarFormulario(
            actualizar: true);
    }

    public IActionResult OnPostRegistrarCliente(string modal)
    {
        if (modal is not ("crear" or "editar")) return BadRequest();
        if (BorradorId.HasValue) _borradores.Eliminar(BorradorId.Value);
        Guid borradorId = _borradores.Guardar(Formulario, modal, Buscar);
        return RedirectToPage("/Clientes/Index", new { BorradorVehiculoId = borradorId });
    }

    public IActionResult OnGetRetomar(Guid borradorId)
    {
        BorradorVehiculo? borrador = _borradores.Obtener(borradorId);
        if (borrador is null)
        {
            TempData["VehiculoError"] = "El borrador del vehículo venció. Abre un formulario nuevo para continuar.";
            return RedirectToPage();
        }

        Formulario = borrador.Formulario;
        Buscar = borrador.Buscar;
        BorradorId = borradorId;
        return MostrarModal(borrador.Modal);
    }

    public IActionResult OnPostDelete(
        int id)
    {
        string? error = _vehiculoService.Delete(id);
        if (error is not null) TempData["VehiculoError"] = error;

        return RedirectToPage(new { Buscar });
    }

    public string ObtenerNombreCliente(
        int? clienteId)
    {
        if (!clienteId.HasValue)
        {
            return "Sin cliente";
        }

        Cliente? cliente =
            Clientes.FirstOrDefault(
                cliente =>
                    cliente.Id ==
                    clienteId.Value);

        if (cliente is null)
        {
            return "Cliente no disponible";
        }

        return $"{cliente.PrimerApellido} {cliente.SegundoApellido} {cliente.Nombres}".Trim();
    }

    public string ObtenerCiCliente(
        int clienteId)
    {
        Cliente? cliente =
            Clientes.FirstOrDefault(
                cliente =>
                    cliente.Id ==
                    clienteId);

        if (cliente is null)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(
                cliente.ComplementoCi))
        {
            return cliente.Ci;
        }

        return
            $"{cliente.Ci}-{cliente.ComplementoCi}";
    }

    private IActionResult GuardarFormulario(
        bool actualizar)
    {
        string modal =
            actualizar
                ? "editar"
                : "crear";

        // Los errores de conversión del Model Binder,
        // por ejemplo kilometraje inválido,
        // impiden guardar.
        if (!ModelState.IsValid)
        {
            return MostrarModal(
                modal);
        }

        var resultado =
            actualizar
                ? _vehiculoService.Update(
                    Formulario)
                : _vehiculoService.Create(
                    Formulario);

        Formulario =
            resultado.Formulario;

        ModelState.Clear();

        foreach (var error in
                 resultado.Errores)
        {
            string campo =
                string.IsNullOrEmpty(
                    error.Key)
                    ? string.Empty
                    : $"Formulario.{error.Key}";

            ModelState.AddModelError(
                campo,
                error.Value);
        }

        if (ModelState.IsValid)
        {
            if (BorradorId.HasValue) _borradores.Eliminar(BorradorId.Value);
            return RedirectToPage();
        }

        return MostrarModal(
            modal);
    }

    private IActionResult MostrarModal(
        string modal)
    {
        ModalAbierto =
            modal;

        if (modal == "editar")
        {
            Vehiculo? anterior =
                _vehiculoService.GetById(
                    Formulario.Id);

            ModeloAnterior =
                string.IsNullOrWhiteSpace(
                    anterior?.Marca)
                    ? anterior?.Modelo
                    : null;
        }

        CargarDatos();

        return Page();
    }

    private void CargarDatos()
    {
        CargarClientes();
        CargarVehiculos();
    }

    private void CargarClientes()
    {
        Clientes =
            _clienteService.Obtener()
                .OrderBy(cliente => cliente.PrimerApellido, CatalogoVehiculos.ComparadorAlfabetico)
                .ThenBy(cliente => cliente.SegundoApellido, CatalogoVehiculos.ComparadorAlfabetico)
                .ThenBy(cliente => cliente.Nombres, CatalogoVehiculos.ComparadorAlfabetico)
                .ThenBy(cliente => cliente.Ci)
                .ThenBy(cliente => cliente.ComplementoCi)
                .ThenBy(cliente => cliente.Id)
                .ToArray();
    }

    private void CargarVehiculos()
    {
        if (string.IsNullOrWhiteSpace(
                Buscar))
        {
            Vehiculos =
                _vehiculoService.GetAll();

            return;
        }

        Vehiculos =
            _vehiculoService.Search(
                Buscar.Trim());
    }
}
