using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Vehiculos;

public class IndexModel : PageModel
{
    private readonly VehiculoService _vehiculoService;

    public IndexModel(VehiculoService vehiculoService)
    {
        _vehiculoService = vehiculoService;
    }

    [BindProperty]
    public VehiculoFormViewModel Formulario { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    public List<Vehiculo> Vehiculos { get; private set; } = [];

    public string? ModalAbierto { get; private set; }

    public string? ModeloAnterior { get; private set; }

    public void OnGet()
    {
        CargarVehiculos();
    }

    public IActionResult OnPostCreate() => GuardarFormulario(actualizar: false);

    public IActionResult OnPostUpdate() => GuardarFormulario(actualizar: true);

    private IActionResult GuardarFormulario(bool actualizar)
    {
        string modal = actualizar ? "editar" : "crear";
        // Los errores de conversión (por ejemplo, kilometraje no numérico) impiden guardar.
        if (!ModelState.IsValid)
        {
            return MostrarModal(modal);
        }

        var resultado = actualizar
            ? _vehiculoService.Update(Formulario)
            : _vehiculoService.Create(Formulario);
        Formulario = resultado.Formulario;
        ModelState.Clear();

        foreach (var error in resultado.Errores)
        {
            string campo = string.IsNullOrEmpty(error.Key) ? string.Empty : $"Formulario.{error.Key}";
            ModelState.AddModelError(campo, error.Value);
        }

        return ModelState.IsValid ? RedirectToPage() : MostrarModal(modal);
    }

    public IActionResult OnPostDelete(int id)
    {
        _vehiculoService.Delete(id);

        return RedirectToPage();
    }

    private IActionResult MostrarModal(string modal)
    {
        ModalAbierto = modal;

        if (modal == "editar")
        {
            var anterior = _vehiculoService.GetById(Formulario.Id);
            ModeloAnterior = string.IsNullOrWhiteSpace(anterior?.Marca) ? anterior?.Modelo : null;
        }

        CargarVehiculos();

        return Page();
    }

    private void CargarVehiculos()
    {
        if (string.IsNullOrWhiteSpace(Buscar))
        {
            Vehiculos = _vehiculoService.GetAll();
            return;
        }

        Vehiculos = _vehiculoService.Search(Buscar.Trim());
    }
}
