using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Pages.Productos;

public class IndexModel : PageModel
{
    private readonly ProductoService _productoService;

    public IndexModel(
        ProductoService productoService)
    {
        _productoService =
            productoService;
    }

    [BindProperty]
    public ProductoFormViewModel ProductoInput { get; set; } =
        new();

    [BindProperty]
    public int ProductoId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }

    public IReadOnlyList<Producto> Productos { get; private set; } =
        [];

    public string? FormularioActivo { get; private set; }

    public string? MensajeExito { get; private set; }

    public string? MensajeError { get; private set; }

    public void OnGet()
    {
        CargarMensajes();
        CargarProductos();
    }

    public IActionResult OnGetBuscar()
    {
        CargarMensajes();
        CargarProductos();

        return Page();
    }

    public IActionResult OnPostCrear()
    {
        if (!User.IsInRole("Administrador"))
        {
            return Forbid();
        }
        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "crear";

            CargarProductos();

            return Page();
        }

        var resultado =
            _productoService.Crear(
                ProductoInput);

        ProductoInput =
            resultado.Formulario;

        ModelState.Clear();

        AgregarErrores(
            resultado.Errores);

        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "crear";

            CargarProductos();

            return Page();
        }

        TempData["ProductoExito"] =
            "Producto registrado correctamente.";

        return RedirectToPage(
            new
            {
                TerminoBusqueda
            });
    }

    public IActionResult OnPostActualizar()
    {
        if (!User.IsInRole("Administrador"))
        {
            return Forbid();
        }

        ProductoInput.Id =
            ProductoId;


        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "editar";

            CargarProductos();

            return Page();
        }

        var resultado =
            _productoService.Actualizar(
                ProductoInput);

        ProductoInput =
            resultado.Formulario;

        ModelState.Clear();

        AgregarErrores(
            resultado.Errores);

        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "editar";

            ProductoId =
                ProductoInput.Id;

            CargarProductos();

            return Page();
        }

        TempData["ProductoExito"] =
            "Producto actualizado correctamente.";

        return RedirectToPage(
            new
            {
                TerminoBusqueda
            });
    }

    public IActionResult OnPostEliminar()
    {
        if (!User.IsInRole("Administrador"))
        {
            return Forbid();
        }

        string? error =
            _productoService.Eliminar(
                ProductoId);

        if (!string.IsNullOrWhiteSpace(
                error))
        {
            TempData["ProductoError"] =
                error;

            return RedirectToPage(
                new
                {
                    TerminoBusqueda
                });
        }

        TempData["ProductoExito"] =
            "Producto eliminado correctamente.";

        return RedirectToPage(
            new
            {
                TerminoBusqueda
            });
    }

    private void CargarProductos()
    {
        if (string.IsNullOrWhiteSpace(
                TerminoBusqueda))
        {
            Productos =
                _productoService.Obtener();

            return;
        }

        Productos =
            _productoService.Buscar(
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
                    : $"ProductoInput.{error.Key}";

            ModelState.AddModelError(
                campo,
                error.Value);
        }
    }

    private void CargarMensajes()
    {
        MensajeExito =
            TempData["ProductoExito"] as string;

        MensajeError =
            TempData["ProductoError"] as string;
    }
}