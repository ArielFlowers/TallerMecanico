using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Sesion;

namespace TallerMecanico.Pages.Clientes;

public class IndexModel(
    ClientesApi clientesApi,
    IBorradorVehiculoPort borradores,
    IBorradorOrden borradoresOrden) : PageModel
{
    [BindProperty] public ClienteFormViewModel ClienteInput { get; set; } = new();
    [BindProperty] public int ClienteId { get; set; }
    [BindProperty(SupportsGet = true)] public string? TerminoBusqueda { get; set; }
    [BindProperty(SupportsGet = true)] public int? ClienteSeleccionadoId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BorradorVehiculoId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? OrdenBorradorId { get; set; }
    public IReadOnlyList<ClienteDto> Clientes { get; private set; } = [];
    public string? FormularioActivo { get; private set; }
    public string? MensajeExito { get; private set; }
    public string? MensajeError { get; private set; }

    public Task<IActionResult> OnGetAsync() => AtenderAsync(async () =>
    {
        MensajeExito = TempData["ClienteExito"] as string;
        MensajeError = TempData["ClienteError"] as string;
        await CargarClientesAsync();
        if (BorradorVehiculoId.HasValue && borradores.Obtener(BorradorVehiculoId.Value) is not null
            || OrdenBorradorId.HasValue && borradoresOrden.Obtener(OrdenBorradorId.Value) is not null)
            FormularioActivo = "crear";
        return Page();
    });

    public Task<IActionResult> OnGetSugerenciasAsync(string? termino) => AtenderAsync(async () =>
    {
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(await clientesApi.SugerirAsync(termino, HttpContext.RequestAborted));
    });

    public Task<IActionResult> OnPostCrearAsync() => AtenderAsync(() => GuardarFormularioAsync(actualizar: false));
    public Task<IActionResult> OnPostActualizarAsync() => AtenderAsync(() => GuardarFormularioAsync(actualizar: true));

    public Task<IActionResult> OnPostEliminarAsync() => AtenderAsync(async () =>
    {
        var resultado = await clientesApi.EliminarAsync(ClienteId, HttpContext.RequestAborted);
        if (resultado.Exito) TempData["ClienteExito"] = "Cliente eliminado correctamente.";
        else TempData["ClienteError"] = resultado.Mensaje;
        return RedirigirAlListado();
    });

    private async Task<IActionResult> GuardarFormularioAsync(bool actualizar)
    {
        FormularioActivo = actualizar ? "editar" : "crear";
        if (actualizar) ClienteInput.Id = ClienteId;
        else if (BorradorVehiculoId.HasValue && borradores.Obtener(BorradorVehiculoId.Value) is null)
            ModelState.AddModelError(string.Empty, "El borrador del vehículo venció. Regresa a Vehículos para continuar.");

        if (!ModelState.IsValid) return await MostrarFormularioAsync();
        try
        {
            var resultado = actualizar
                ? await clientesApi.ActualizarAsync(ClienteInput)
                : await clientesApi.CrearAsync(ClienteInput);
            ClienteInput = resultado.Formulario;
            ModelState.Clear();
            foreach (var error in resultado.Errores)
                ModelState.AddModelError(string.IsNullOrWhiteSpace(error.Key) ? string.Empty : $"ClienteInput.{error.Key}", error.Value);
        }
        catch (ValidationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }

        if (!ModelState.IsValid)
        {
            if (actualizar) ClienteId = ClienteInput.Id;
            return await MostrarFormularioAsync();
        }

        TempData["ClienteExito"] = actualizar ? "Cliente actualizado correctamente." : "Cliente registrado correctamente.";
        if (!actualizar)
        {
            if (BorradorVehiculoId.HasValue && borradores.AsignarCliente(BorradorVehiculoId.Value, ClienteInput.Id))
                return RedirectToPage("/Vehiculos/Index", "Retomar", new { borradorId = BorradorVehiculoId.Value });
            if (OrdenBorradorId.HasValue && borradoresOrden.Obtener(OrdenBorradorId.Value) is not null)
                return RedirectToPage("/Ordenes/Crear", new { BorradorId = OrdenBorradorId.Value, ClienteNuevoId = ClienteInput.Id });
        }
        return RedirigirAlListado();
    }

    private async Task<IActionResult> MostrarFormularioAsync()
    {
        await CargarClientesAsync();
        return Page();
    }

    private async Task CargarClientesAsync()
    {
        if (ClienteSeleccionadoId.HasValue)
        {
            var cliente = await clientesApi.ObtenerPorIdAsync(ClienteSeleccionadoId.Value, HttpContext.RequestAborted);
            Clientes = cliente is null ? [] : [cliente];
        }
        else
            Clientes = await clientesApi.ObtenerAsync(string.IsNullOrWhiteSpace(TerminoBusqueda) ? null : TerminoBusqueda.Trim(), HttpContext.RequestAborted);
    }

    private IActionResult RedirigirAlListado() =>
        RedirectToPage(new { TerminoBusqueda, ClienteSeleccionadoId, BorradorVehiculoId, OrdenBorradorId });

    private async Task<IActionResult> AtenderAsync(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized) { return Challenge(); }
        catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El servicio de clientes no está disponible. Intenta nuevamente."); }
        catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El servicio de clientes no está disponible. Intenta nuevamente."); }
    }
}
