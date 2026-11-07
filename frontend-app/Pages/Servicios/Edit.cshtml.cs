using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Servicios;

public class EditModel(ServiciosApi serviciosApi) : ServiciosPageModel
{
    [BindProperty] public ServicioFormViewModel Formulario { get; set; } = new();
    [TempData] public string? MensajeExito { get; set; }
    public int ServicioId { get; private set; }

    public Task<IActionResult> OnGetAsync(int id) => AtenderAsync(async () =>
    {
        var servicio = await serviciosApi.ObtenerPorIdAsync(id, HttpContext.RequestAborted);
        if (servicio is null) return NotFound();
        ServicioId = servicio.Id;
        Formulario = new ServicioFormViewModel
        {
            Nombre = servicio.Nombre,
            Descripcion = servicio.Descripcion,
            Costo = servicio.Costo,
            TiempoEstimadoHoras = servicio.TiempoEstimadoHoras,
            UnidadCantidad = servicio.UnidadCantidad
        };
        return Page();
    });

    public Task<IActionResult> OnPostAsync(int id) => AtenderAsync(async () =>
    {
        ServicioId = id;
        if (!ModelState.IsValid) return Page();
        try
        {
            var resultado = await serviciosApi.ActualizarAsync(id, Formulario);
            Formulario = resultado.Formulario;
            ModelState.Clear();
            foreach (var error in resultado.Errores)
                ModelState.AddModelError(string.IsNullOrWhiteSpace(error.Key) ? string.Empty : $"Formulario.{error.Key}", error.Value);
        }
        catch (ValidationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            ModelState.AddModelError(string.Empty, "El servicio solicitado no existe.");
        }

        if (!ModelState.IsValid) return Page();
        MensajeExito = "Servicio actualizado correctamente.";
        return RedirectToPage("./Index");
    });
}
