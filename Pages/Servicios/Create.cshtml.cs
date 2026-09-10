using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Servicios;

public class CreateModel : PageModel
{
    private readonly ServicioService _servicioService;
    private readonly ValidacionServicios _validacionServicios;

    public CreateModel(
        ServicioService servicioService,
        ValidacionServicios validacionServicios)
    {
        _servicioService = servicioService;
        _validacionServicios = validacionServicios;
    }

    [BindProperty]
    public ServicioFormViewModel Formulario { get; set; } = new();

    public IActionResult OnPost()
    {
        AgregarErroresDeFormato();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Servicio servicio = CrearServicioDesdeFormulario();

        _servicioService.Crear(servicio);

        return RedirectToPage("./Index");
    }

    private void AgregarErroresDeFormato()
    {
        IReadOnlyDictionary<string, string> errores =
            _validacionServicios.Validar(Formulario);

        foreach (KeyValuePair<string, string> error in errores)
        {
            ModelState.AddModelError(
                $"Formulario.{error.Key}",
                error.Value);
        }
    }

    private Servicio CrearServicioDesdeFormulario()
    {
        return new Servicio
        {
            Nombre = Formulario.Nombre,
            Descripcion = Formulario.Descripcion,
            Costo = Formulario.Costo,
            TiempoEstimadoHoras = Formulario.TiempoEstimadoHoras
        };
    }
}
