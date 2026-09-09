using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Vehiculos;

public class IndexModel : PageModel
{
    private readonly IVehiculoService _vehiculoService;

    public IndexModel(IVehiculoService vehiculoService)
    {
        _vehiculoService = vehiculoService;
    }

    [BindProperty]
    public VehiculoFormViewModel Formulario { get; set; } = new();

    public List<Vehiculo> Vehiculos { get; private set; } = [];

    public string? ModalAbierto { get; private set; }

    public void OnGet()
    {
        CargarVehiculos();
    }

    public IActionResult OnPostCrear()
    {
        if (!ModelState.IsValid)
        {
            return MostrarModal("crear");
        }

        Vehiculo vehiculo = MapearVehiculo(0);

        if (_vehiculoService.PlacaRegistrada(vehiculo.Placa, vehiculo.Id))
        {
            ModelState.AddModelError(
                "Formulario.Placa",
                "Esta placa ya está registrada.");

            return MostrarModal("crear");
        }

        _vehiculoService.Crear(vehiculo);

        return RedirectToPage();
    }

    public IActionResult OnPostEditar()
    {
        if (!ModelState.IsValid)
        {
            return MostrarModal("editar");
        }

        Vehiculo vehiculo = MapearVehiculo(Formulario.Id);

        if (_vehiculoService.PlacaRegistrada(vehiculo.Placa, vehiculo.Id))
        {
            ModelState.AddModelError(
                "Formulario.Placa",
                "Esta placa ya está registrada.");

            return MostrarModal("editar");
        }

        _vehiculoService.Actualizar(vehiculo);

        return RedirectToPage();
    }

    public IActionResult OnPostEliminar(int id)
    {
        _vehiculoService.Eliminar(id);

        return RedirectToPage();
    }

    private IActionResult MostrarModal(string modal)
    {
        ModalAbierto = modal;
        CargarVehiculos();

        return Page();
    }

    private void CargarVehiculos()
    {
        Vehiculos = _vehiculoService.ObtenerTodos();
    }

    private Vehiculo MapearVehiculo(int id)
    {
        return new Vehiculo
        {
            Id = id,
            Placa = Formulario.Placa.Trim().ToUpperInvariant(),
            Modelo = Formulario.Modelo.Trim(),
            Kilometraje = Formulario.Kilometraje,
            Observaciones = Formulario.Observaciones?.Trim() ?? string.Empty
        };
    }
}
