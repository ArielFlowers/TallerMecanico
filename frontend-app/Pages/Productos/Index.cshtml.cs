using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Sesion;

namespace TallerMecanico.Pages.Productos;

public class IndexModel(ProductosApi productosApi, IBorradorOrden borradoresOrden) : PageModel
{
    [BindProperty] public ProductoFormViewModel ProductoInput { get; set; } = new();
    [BindProperty] public int ProductoId { get; set; }
    [BindProperty(SupportsGet = true)] public string? TerminoBusqueda { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProductoSeleccionadoId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? OrdenBorradorId { get; set; }
    public IReadOnlyList<ProductoDto> Productos { get; private set; } = [];
    public string? FormularioActivo { get; private set; }
    public string? MensajeExito { get; private set; }
    public string? MensajeError { get; private set; }

    public Task<IActionResult> OnGetAsync() => AtenderAsync(async () =>
    {
        MensajeExito = TempData["ProductoExito"] as string;
        MensajeError = TempData["ProductoError"] as string;
        await CargarProductosAsync();
        return Page();
    });

    public Task<IActionResult> OnGetBuscarAsync() => OnGetAsync();

    public Task<IActionResult> OnGetSugerenciasAsync(string? termino) => AtenderAsync(async () =>
    {
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(await productosApi.SugerirAsync(termino, HttpContext.RequestAborted));
    });

    public Task<IActionResult> OnPostCrearAsync() => AtenderAsync(() => GuardarFormularioAsync(actualizar: false));
    public Task<IActionResult> OnPostActualizarAsync() => AtenderAsync(() => GuardarFormularioAsync(actualizar: true));

    public Task<IActionResult> OnPostEliminarAsync() => AtenderAsync(async () =>
    {
        if (!User.IsInRole(Roles.Administrador)) return Forbid();
        var resultado = await productosApi.EliminarAsync(ProductoId, HttpContext.RequestAborted);
        if (resultado.Exito) TempData["ProductoExito"] = "Producto eliminado correctamente.";
        else TempData["ProductoError"] = resultado.Mensaje;
        return RedirigirAlListado();
    });

    private async Task<IActionResult> GuardarFormularioAsync(bool actualizar)
    {
        bool altaDesdeOrdenValida = !actualizar && User.IsInRole(Roles.Recepcionista)
            && OrdenBorradorId.HasValue && borradoresOrden.Obtener(OrdenBorradorId.Value) is not null;
        if (!User.IsInRole(Roles.Administrador) && !altaDesdeOrdenValida) return Forbid();
        FormularioActivo = actualizar ? "editar" : "crear";
        if (actualizar) ProductoInput.Id = ProductoId;
        if (!ModelState.IsValid) return await MostrarFormularioAsync();

        try
        {
            var resultado = actualizar
                ? await productosApi.ActualizarAsync(ProductoInput)
                : await productosApi.CrearAsync(ProductoInput);
            ProductoInput = resultado.Formulario;
            ModelState.Clear();
            foreach (var error in resultado.Errores)
                ModelState.AddModelError(string.IsNullOrWhiteSpace(error.Key) ? string.Empty : $"ProductoInput.{error.Key}", error.Value);
        }
        catch (ValidationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }
        catch (HttpRequestException error) when (actualizar && error.StatusCode == HttpStatusCode.NotFound)
        {
            ModelState.AddModelError(string.Empty, "El producto solicitado no existe.");
        }

        if (!ModelState.IsValid)
        {
            if (actualizar) ProductoId = ProductoInput.Id;
            return await MostrarFormularioAsync();
        }

        TempData["ProductoExito"] = actualizar ? "Producto actualizado correctamente." : "Producto registrado correctamente.";
        if (!actualizar && OrdenBorradorId.HasValue)
        {
            int id = ProductoInput.Id;
            if (id <= 0)
                id = (await productosApi.ObtenerAsync(ProductoInput.Codigo ?? string.Empty, HttpContext.RequestAborted))
                    .FirstOrDefault(producto => string.Equals(producto.Codigo, ProductoInput.Codigo, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
            if (id > 0) return RedirectToPage("/Ordenes/Crear", new { BorradorId = OrdenBorradorId, ProductoNuevoId = id });
        }
        return RedirigirAlListado();
    }

    private async Task<IActionResult> MostrarFormularioAsync()
    {
        await CargarProductosAsync();
        return Page();
    }

    private async Task CargarProductosAsync()
    {
        if (ProductoSeleccionadoId.HasValue)
        {
            var producto = await productosApi.ObtenerPorIdAsync(ProductoSeleccionadoId.Value, HttpContext.RequestAborted);
            Productos = producto is null ? [] : [producto];
        }
        else
            Productos = await productosApi.ObtenerAsync(string.IsNullOrWhiteSpace(TerminoBusqueda) ? null : TerminoBusqueda.Trim(), HttpContext.RequestAborted);
    }

    private IActionResult RedirigirAlListado() =>
        RedirectToPage(new { TerminoBusqueda, ProductoSeleccionadoId, OrdenBorradorId });

    private async Task<IActionResult> AtenderAsync(Func<Task<IActionResult>> accion)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        if (!User.IsInRole(Roles.Administrador) && !User.IsInRole(Roles.Recepcionista)) return Forbid();
        try { return await accion(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized) { return Challenge(); }
        catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El servicio de productos no está disponible. Intenta nuevamente."); }
        catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El servicio de productos no está disponible. Intenta nuevamente."); }
    }
}
