using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Pages.Clientes;

public class IndexModel : PageModel
{
    private readonly ClienteService _clienteService;
    private readonly IBorradorVehiculoPort _borradores;
    private readonly IBorradorOrdenPort _borradoresOrden;

    public IndexModel(
        ClienteService clienteService,
        IBorradorVehiculoPort borradores,
        IBorradorOrdenPort borradoresOrden)
    {
        _clienteService =
            clienteService;
        _borradores = borradores;
        _borradoresOrden = borradoresOrden;
    }

    [BindProperty]
    public ClienteFormViewModel ClienteInput { get; set; } =
        new();

    [BindProperty]
    public int ClienteId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ClienteSeleccionadoId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? BorradorVehiculoId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? OrdenBorradorId { get; set; }

    public IReadOnlyList<Cliente> Clientes { get; private set; } =
        [];

    public string? FormularioActivo { get; private set; }

    public string? MensajeExito { get; private set; }

    public string? MensajeError { get; private set; }

    public void OnGet()
    {
        CargarMensajes();
        CargarClientes();
        if (BorradorVehiculoId.HasValue && _borradores.Obtener(BorradorVehiculoId.Value) is not null)
            FormularioActivo = "crear";
        else if (OrdenBorradorId.HasValue && _borradoresOrden.Obtener(OrdenBorradorId.Value) is not null)
            FormularioActivo = "crear";
    }

    public async Task<JsonResult> OnGetSugerenciasAsync(string? termino)
    {
        Response.Headers.CacheControl = "no-store";
        var sugerencias = await _clienteService.SugerirAsync(termino, HttpContext.RequestAborted);
        return new JsonResult(sugerencias);
    }

    public IActionResult OnPostCrear()
    {
        if (BorradorVehiculoId.HasValue && _borradores.Obtener(BorradorVehiculoId.Value) is null)
        {
            ModelState.AddModelError(string.Empty, "El borrador del vehículo venció. Regresa a Vehículos para continuar.");
        }
        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "crear";

            CargarClientes();

            return Page();
        }

        var resultado =
            _clienteService.Crear(
                ClienteInput);

        ClienteInput =
            resultado.Formulario;

        ModelState.Clear();

        AgregarErrores(
            resultado.Errores);

        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "crear";

            CargarClientes();

            return Page();
        }

        TempData["ClienteExito"] =
            "Cliente registrado correctamente.";

        if (BorradorVehiculoId.HasValue &&
            _borradores.AsignarCliente(BorradorVehiculoId.Value, ClienteInput.Id))
        {
            return RedirectToPage("/Vehiculos/Index", "Retomar", new { borradorId = BorradorVehiculoId.Value });
        }

        if (OrdenBorradorId.HasValue && _borradoresOrden.Obtener(OrdenBorradorId.Value) is not null)
            return RedirectToPage("/Ordenes/Crear", new { BorradorId = OrdenBorradorId.Value, ClienteNuevoId = ClienteInput.Id });

        return RedirigirAlListado();
    }

    public IActionResult OnPostActualizar()
    {
        ClienteInput.Id =
            ClienteId;

        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "editar";

            CargarClientes();

            return Page();
        }

        var resultado =
            _clienteService.Actualizar(
                ClienteInput);

        ClienteInput =
            resultado.Formulario;

        ModelState.Clear();

        AgregarErrores(
            resultado.Errores);

        if (!ModelState.IsValid)
        {
            FormularioActivo =
                "editar";

            ClienteId =
                ClienteInput.Id;

            CargarClientes();

            return Page();
        }

        TempData["ClienteExito"] =
            "Cliente actualizado correctamente.";

        return RedirigirAlListado();
    }

    public IActionResult OnPostEliminar()
    {
        string? error =
            _clienteService.Eliminar(
                ClienteId);

        if (!string.IsNullOrWhiteSpace(
                error))
        {
            TempData["ClienteError"] =
                error;

            return RedirigirAlListado();
        }

        TempData["ClienteExito"] =
            "Cliente eliminado correctamente.";

        return RedirigirAlListado();
    }

    private void CargarClientes()
    {
        if (ClienteSeleccionadoId.HasValue)
        {
            var cliente = _clienteService.ObtenerPorId(ClienteSeleccionadoId.Value);
            Clientes = cliente is null ? [] : [cliente];
            return;
        }

        if (string.IsNullOrWhiteSpace(
                TerminoBusqueda))
        {
            Clientes =
                _clienteService.Obtener();

            return;
        }

        Clientes =
            _clienteService.Buscar(
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
                    : $"ClienteInput.{error.Key}";

            ModelState.AddModelError(
                campo,
                error.Value);
        }
    }

    private IActionResult RedirigirAlListado()
    {
        return RedirectToPage(new
        {
            TerminoBusqueda,
            ClienteSeleccionadoId,
            BorradorVehiculoId,
            OrdenBorradorId
        });
    }

    private void CargarMensajes()
    {
        MensajeExito =
            TempData["ClienteExito"] as string;

        MensajeError =
            TempData["ClienteError"] as string;
    }
}
