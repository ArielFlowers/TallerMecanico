using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Api;

namespace TallerMecanico.Pages.Servicios;

public class CreateModel(ServiciosApi serviciosApi) : ServiciosPageModel
{
    [BindProperty] public ServicioFormViewModel Formulario { get; set; } = new();
    [TempData] public string? MensajeExito { get; set; }

    public Task<IActionResult> OnGetAsync() => AtenderAsync(() => Task.FromResult<IActionResult>(Page()));

    public Task<IActionResult> OnPostAsync() => AtenderAsync(async () =>
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            var resultado = await serviciosApi.CrearAsync(Formulario);
            Formulario = resultado.Formulario;
            ModelState.Clear();
            foreach (var error in resultado.Errores)
                ModelState.AddModelError(string.IsNullOrWhiteSpace(error.Key) ? string.Empty : $"Formulario.{error.Key}", error.Value);
        }
        catch (ValidationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }

        if (!ModelState.IsValid) return Page();
        MensajeExito = "Servicio creado correctamente.";
        return RedirectToPage("./Index");
    });
}
