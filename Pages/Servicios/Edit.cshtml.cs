using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Servicios;

public class EditModel : PageModel
{
    private readonly ServicioService _servicioService;
    private readonly ValidacionServicios _validacionServicios;

    public EditModel(
        ServicioService servicioService,
        ValidacionServicios validacionServicios)
    {
        _servicioService = servicioService;
        _validacionServicios = validacionServicios;
    }

    [BindProperty]
    public ServicioFormViewModel Formulario { get; set; } = new();

    public int ServicioId { get; private set; }

    public IActionResult OnGet(int id)
    {
        Servicio? servicio = _servicioService.ObtenerPorId(id);

        if (servicio is null)
        {
            return NotFound();
        }

        ServicioId = servicio.Id;
        Formulario = CrearFormularioDesdeServicio(servicio);

        return Page();
    }

    public IActionResult OnPost(int id)
    {
        ServicioId = id;

        AgregarErroresDeFormato();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Servicio servicio = CrearServicioDesdeFormulario(id);

        _servicioService.Actualizar(servicio);

        return RedirectToPage("./Control");
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

    private static ServicioFormViewModel CrearFormularioDesdeServicio(
        Servicio servicio)
    {
        return new ServicioFormViewModel
        {
            Nombre = servicio.Nombre,
            Descripcion = servicio.Descripcion,
            Costo = servicio.Costo,
            TiempoEstimadoHoras = servicio.TiempoEstimadoHoras
        };
    }

    private Servicio CrearServicioDesdeFormulario(int id)
    {
        return new Servicio
        {
            Id = id,
            Nombre = Formulario.Nombre,
            Descripcion = Formulario.Descripcion,
            Costo = Formulario.Costo,
            TiempoEstimadoHoras = Formulario.TiempoEstimadoHoras
        };
    }
}