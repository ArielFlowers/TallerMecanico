using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Pages.Productos;

public class IndexModel : PageModel
{
    private readonly ProductoService _productoService;
    private readonly IBorradorOrdenPort _borradoresOrden;

    public IndexModel(
        ProductoService productoService,
        IBorradorOrdenPort borradoresOrden)
    {
        _productoService =
            productoService;
        _borradoresOrden = borradoresOrden;
    }

    [BindProperty]
    public ProductoFormViewModel ProductoInput { get; set; } =
        new();

    [BindProperty]
    public int ProductoId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }
    [BindProperty(SupportsGet = true)]
    public Guid? OrdenBorradorId { get; set; }

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
        bool altaDesdeOrdenValida =
            User.IsInRole("Recepcionista") &&
            OrdenBorradorId.HasValue &&
            _borradoresOrden.Obtener(OrdenBorradorId.Value) is not null;
        if (!User.IsInRole("Administrador") && !altaDesdeOrdenValida)
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

        if (OrdenBorradorId.HasValue)
        {
            Producto? creado = _productoService.Buscar(ProductoInput.Codigo ?? string.Empty)
                .FirstOrDefault(x => string.Equals(x.Codigo, ProductoInput.Codigo, StringComparison.OrdinalIgnoreCase));
            if (creado is not null)
                return RedirectToPage("/Ordenes/Crear", new { BorradorId = OrdenBorradorId, ProductoNuevoId = creado.Id });
        }

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
