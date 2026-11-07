using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

namespace TallerMecanico.Pages.Mecanicos;

public class IndexModel(MecanicosApi mecanicosApi, IBorradorOrden borradoresOrden) : PageModel
{
    private const string FormularioCrear = "crear";
    private const string FormularioEditar = "editar";
    private const string MensajeRegistroNoEncontrado = "El mecánico seleccionado ya no existe.";

    public IReadOnlyList<MecanicoDto> Mecanicos { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? TerminoBusqueda { get; set; }
    [BindProperty(SupportsGet = true)] public int? MecanicoSeleccionadoId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? OrdenBorradorId { get; set; }
    [BindProperty] public MecanicoInputModel MecanicoInput { get; set; } = new();
    [BindProperty] public int MecanicoId { get; set; }
    public string? FormularioActivo { get; private set; }
    public bool PuedeAdministrar => User.IsInRole("Administrador");
    [TempData] public string? MensajeExito { get; set; }
    [TempData] public string? MensajeError { get; set; }

    public Task<IActionResult> OnGetAsync() => AtenderAsync(async () =>
    {
        if (PuedeAdministrar && OrdenBorradorId.HasValue && borradoresOrden.Obtener(OrdenBorradorId.Value) is not null)
            FormularioActivo = FormularioCrear;
        await CargarMecanicosAsync();
        return Page();
    });

    public Task<IActionResult> OnGetSugerenciasAsync(string? termino) => AtenderAsync(async () =>
    {
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(await mecanicosApi.SugerirAsync(termino, HttpContext.RequestAborted));
    });

    public Task<IActionResult> OnPostCrearAsync() => AtenderAsync(async () =>
    {
        if (!PuedeAdministrar) return Forbid();
        if (!ModelState.IsValid) return await MostrarFormularioAsync(FormularioCrear);
        try
        {
            AgregarErroresAlModelState(await mecanicosApi.CrearAsync(MecanicoInput));
        }
        catch (ValidationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }
        if (!ModelState.IsValid) return await MostrarFormularioAsync(FormularioCrear);

        if (OrdenBorradorId.HasValue && borradoresOrden.Obtener(OrdenBorradorId.Value) is not null)
        {
            TempData["OrdenMensaje"] = "Mecánico registrado correctamente. Ya puedes elegirlo en la orden.";
            return RedirectToPage("/Ordenes/Crear", new { BorradorId = OrdenBorradorId });
        }
        MensajeExito = "Mecánico registrado correctamente.";
        return RedirigirAlListado();
    });

    public Task<IActionResult> OnPostActualizarAsync() => AtenderAsync(async () =>
    {
        if (!PuedeAdministrar) return Forbid();
        if (!ModelState.IsValid) return await MostrarFormularioAsync(FormularioEditar);
        try
        {
            var resultado = await mecanicosApi.ActualizarAsync(MecanicoId, MecanicoInput);
            AgregarErroresAlModelState(resultado.Errores);
            if (resultado.Errores.Count == 0 && !resultado.Actualizado)
                ModelState.AddModelError(string.Empty, MensajeRegistroNoEncontrado);
        }
        catch (ValidationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            ModelState.AddModelError(string.Empty, MensajeRegistroNoEncontrado);
        }
        if (!ModelState.IsValid) return await MostrarFormularioAsync(FormularioEditar);
        MensajeExito = "Mecánico actualizado correctamente.";
        return RedirigirAlListado();
    });

    public Task<IActionResult> OnPostEliminarAsync() => AtenderAsync(async () =>
    {
        if (!PuedeAdministrar) return Forbid();
        var resultado = await mecanicosApi.EliminarAsync(MecanicoId, HttpContext.RequestAborted);
        if (!resultado.Exito) MensajeError = resultado.Mensaje;
        else MensajeExito = "Mecánico eliminado correctamente.";
        return RedirigirAlListado();
    });

    private async Task<IActionResult> MostrarFormularioAsync(string formulario)
    {
        FormularioActivo = formulario;
        await CargarMecanicosAsync();
        return Page();
    }

    private async Task CargarMecanicosAsync()
    {
        if (MecanicoSeleccionadoId.HasValue)
        {
            var mecanico = await mecanicosApi.ObtenerPorIdAsync(MecanicoSeleccionadoId.Value, HttpContext.RequestAborted);
            Mecanicos = mecanico is null ? [] : [mecanico];
        }
        else
            Mecanicos = await mecanicosApi.ObtenerAsync(TerminoBusqueda, HttpContext.RequestAborted);
    }

    private void AgregarErroresAlModelState(IReadOnlyDictionary<string, string> errores)
    {
        foreach (var (campo, mensaje) in errores)
            ModelState.AddModelError(string.IsNullOrEmpty(campo) ? string.Empty : $"{nameof(MecanicoInput)}.{campo}", mensaje);
    }

    private IActionResult RedirigirAlListado() =>
        RedirectToPage(new { terminoBusqueda = TerminoBusqueda, mecanicoSeleccionadoId = MecanicoSeleccionadoId });

    private async Task<IActionResult> AtenderAsync(Func<Task<IActionResult>> accion)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        if (!PuedeAdministrar && !User.IsInRole("Recepcionista")) return Forbid();
        try { return await accion(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized) { return Challenge(); }
        catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El servicio de mecánicos no está disponible. Intenta nuevamente."); }
        catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El servicio de mecánicos no está disponible. Intenta nuevamente."); }
    }
}
