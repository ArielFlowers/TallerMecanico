using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Sesion;

namespace TallerMecanico.Pages.Vehiculos;

public class IndexModel(
    VehiculosApi vehiculosApi,
    ClientesApi clientesApi,
    IBorradorVehiculoPort borradores) : PageModel
{
    private static readonly StringComparer ComparadorAlfabetico =
        StringComparer.Create(CultureInfo.GetCultureInfo("es-BO"), ignoreCase: true);

    [BindProperty] public VehiculoFormViewModel Formulario { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public int? VehiculoSeleccionadoId { get; set; }
    [BindProperty] public Guid? BorradorId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? OrdenBorradorId { get; set; }
    [BindProperty(SupportsGet = true)] public int? ClientePreseleccionadoId { get; set; }
    public IReadOnlyList<VehiculoDto> Vehiculos { get; private set; } = [];
    public IReadOnlyList<ClienteDto> Clientes { get; private set; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Catalogo { get; private set; } =
        new Dictionary<string, IReadOnlyList<string>>();
    public IEnumerable<string> MarcasOrdenadas => Catalogo.Keys.OrderBy(marca => marca, ComparadorAlfabetico);
    public string? ModalAbierto { get; private set; }
    public string? ModeloAnterior { get; private set; }
    public string? MensajeError => TempData["VehiculoError"] as string;

    public Task<IActionResult> OnGetAsync() => AtenderAsync(async () =>
    {
        await CargarDatosAsync();
        if (OrdenBorradorId.HasValue) ModalAbierto = "crear";
        if (ClientePreseleccionadoId.HasValue) Formulario.ClienteId = ClientePreseleccionadoId;
        return Page();
    });

    public Task<IActionResult> OnGetBuscarAsync() => AtenderAsync(async () =>
    {
        await CargarDatosAsync();
        return Page();
    });

    public Task<IActionResult> OnGetSugerenciasAsync(string? termino) => AtenderAsync(async () =>
    {
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(await vehiculosApi.SugerirAsync(termino, HttpContext.RequestAborted));
    });

    public Task<IActionResult> OnPostCreateAsync() => AtenderAsync(() => GuardarFormularioAsync(actualizar: false));
    public Task<IActionResult> OnPostUpdateAsync() => AtenderAsync(() => GuardarFormularioAsync(actualizar: true));

    public IActionResult OnPostRegistrarCliente(string modal)
    {
        if (modal is not ("crear" or "editar")) return BadRequest();
        Guid? ordenBorradorId = OrdenBorradorId;
        if (BorradorId.HasValue)
        {
            ordenBorradorId = borradores.Obtener(BorradorId.Value)?.OrdenBorradorId ?? ordenBorradorId;
            borradores.Eliminar(BorradorId.Value);
        }
        Guid borradorId = borradores.Guardar(Formulario, modal, Buscar, ordenBorradorId, VehiculoSeleccionadoId);
        return RedirectToPage("/Clientes/Index", new { BorradorVehiculoId = borradorId, OrdenBorradorId = ordenBorradorId });
    }

    public Task<IActionResult> OnGetRetomarAsync(Guid borradorId) => AtenderAsync(async () =>
    {
        var borrador = borradores.Obtener(borradorId);
        if (borrador is null)
        {
            TempData["VehiculoError"] = "El borrador del vehículo venció. Abre un formulario nuevo para continuar.";
            return RedirectToPage();
        }
        Formulario = borrador.Formulario;
        Buscar = borrador.Buscar;
        VehiculoSeleccionadoId = borrador.VehiculoSeleccionadoId;
        BorradorId = borradorId;
        OrdenBorradorId = borrador.OrdenBorradorId;
        return await MostrarModalAsync(borrador.Modal);
    });

    public Task<IActionResult> OnPostDeleteAsync(int id) => AtenderAsync(async () =>
    {
        var resultado = await vehiculosApi.EliminarAsync(id, HttpContext.RequestAborted);
        if (!resultado.Exito) TempData["VehiculoError"] = resultado.Mensaje;
        return RedirigirAlListado();
    });

    public string ObtenerNombreCliente(int? clienteId)
    {
        if (!clienteId.HasValue) return "Sin cliente";
        var cliente = Clientes.FirstOrDefault(cliente => cliente.Id == clienteId.Value);
        return cliente is null ? "Cliente no disponible" : $"{cliente.PrimerApellido} {cliente.SegundoApellido} {cliente.Nombres}".Trim();
    }

    public string ObtenerCiCliente(int clienteId)
    {
        var cliente = Clientes.FirstOrDefault(cliente => cliente.Id == clienteId);
        if (cliente is null) return string.Empty;
        return string.IsNullOrWhiteSpace(cliente.ComplementoCi) ? cliente.Ci : $"{cliente.Ci}-{cliente.ComplementoCi}";
    }

    public IReadOnlyList<string> ObtenerModelosOrdenados(string marca) =>
        Catalogo.TryGetValue(marca, out var modelos) ? modelos.OrderBy(modelo => modelo, ComparadorAlfabetico).ToArray() : [];

    private async Task<IActionResult> GuardarFormularioAsync(bool actualizar)
    {
        string modal = actualizar ? "editar" : "crear";
        if (!ModelState.IsValid) return await MostrarModalAsync(modal);
        try
        {
            var resultado = actualizar
                ? await vehiculosApi.ActualizarAsync(Formulario)
                : await vehiculosApi.CrearAsync(Formulario);
            Formulario = resultado.Formulario;
            ModelState.Clear();
            foreach (var error in resultado.Errores)
                ModelState.AddModelError(string.IsNullOrEmpty(error.Key) ? string.Empty : $"Formulario.{error.Key}", error.Value);
        }
        catch (ValidationException error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }

        if (!ModelState.IsValid) return await MostrarModalAsync(modal);
        if (BorradorId.HasValue) borradores.Eliminar(BorradorId.Value);
        if (OrdenBorradorId.HasValue)
        {
            int id = Formulario.Id;
            if (id <= 0)
                id = (await vehiculosApi.ObtenerAsync(Formulario.Placa ?? string.Empty, HttpContext.RequestAborted))
                    .FirstOrDefault(vehiculo => vehiculo.Placa == Formulario.Placa)?.Id ?? 0;
            if (id > 0) return RedirectToPage("/Ordenes/Crear", new { BorradorId = OrdenBorradorId, VehiculoNuevoId = id });
        }
        return RedirigirAlListado();
    }

    private async Task<IActionResult> MostrarModalAsync(string modal)
    {
        ModalAbierto = modal;
        if (modal == "editar")
        {
            var anterior = await vehiculosApi.ObtenerPorIdAsync(Formulario.Id, HttpContext.RequestAborted);
            ModeloAnterior = string.IsNullOrWhiteSpace(anterior?.Marca) ? anterior?.Modelo : null;
        }
        await CargarDatosAsync();
        return Page();
    }

    private async Task CargarDatosAsync()
    {
        Clientes = (await clientesApi.ObtenerAsync(cancelacion: HttpContext.RequestAborted))
            .OrderBy(cliente => cliente.PrimerApellido, ComparadorAlfabetico)
            .ThenBy(cliente => cliente.SegundoApellido, ComparadorAlfabetico)
            .ThenBy(cliente => cliente.Nombres, ComparadorAlfabetico)
            .ThenBy(cliente => cliente.Ci).ThenBy(cliente => cliente.ComplementoCi).ThenBy(cliente => cliente.Id).ToArray();
        if (VehiculoSeleccionadoId.HasValue)
        {
            var vehiculo = await vehiculosApi.ObtenerPorIdAsync(VehiculoSeleccionadoId.Value, HttpContext.RequestAborted);
            Vehiculos = vehiculo is null ? [] : [vehiculo];
        }
        else
            Vehiculos = await vehiculosApi.ObtenerAsync(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim(), HttpContext.RequestAborted);
        Catalogo = (await vehiculosApi.ObtenerCatalogoAsync(HttpContext.RequestAborted))
            .OrderBy(marca => marca.Key, ComparadorAlfabetico)
            .ToDictionary(marca => marca.Key, marca => (IReadOnlyList<string>)marca.Value.OrderBy(modelo => modelo, ComparadorAlfabetico).ToArray());
    }

    private IActionResult RedirigirAlListado() =>
        RedirectToPage(pageName: null, pageHandler: OrdenBorradorId.HasValue ? "Buscar" : null,
            routeValues: new { Buscar, VehiculoSeleccionadoId, OrdenBorradorId, ClientePreseleccionadoId });

    private async Task<IActionResult> AtenderAsync(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized) { return Challenge(); }
        catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El servicio de vehículos no está disponible. Intenta nuevamente."); }
        catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable, "El servicio de vehículos no está disponible. Intenta nuevamente."); }
    }
}
