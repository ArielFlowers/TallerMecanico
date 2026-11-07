using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using TallerMecanico.Api;
using TallerMecanico.Contratos.Usuarios;

namespace TallerMecanico.Pages.Administracion.Usuarios;

[Authorize(Policy = "SoloAdministrador")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel : PageModel
{
    private const int TamanoPaginaPredeterminado = 10;
    private const int TamanoPaginaMaximo = 100;

    private readonly UsuariosApi _usuarios;

    public IndexModel(UsuariosApi usuarios) => _usuarios = usuarios;

    public IReadOnlyList<UsuarioListViewModel> Usuarios { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int TamanoPagina { get; set; } = TamanoPaginaPredeterminado;

    [BindProperty(SupportsGet = true)]
    public string? Busqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RolFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? ActivoFiltro { get; set; }

    public int TotalUsuarios { get; private set; }

    public int UsuariosActivos { get; private set; }

    public int AdministradoresActivos { get; private set; }

    public int TotalRegistros { get; private set; }

    public int TotalPaginas { get; private set; }

    public bool HayPaginaAnterior => Pagina > 1;

    public bool HayPaginaSiguiente => Pagina < TotalPaginas;

    public Task<IActionResult> OnGetAsync() => AtenderAsync(async () =>
    {
        await CargarResultadosAsync();
        var resumen = await _usuarios.ObtenerResumenAsync(HttpContext.RequestAborted);
        TotalUsuarios = resumen.Total;
        UsuariosActivos = resumen.Activos;
        AdministradoresActivos = resumen.AdministradoresActivos;
        return Page();
    });

    public Task<IActionResult> OnGetResultadosAsync() => AtenderAsync(async () =>
    {
        await CargarResultadosAsync();
        return Partial("_ResultadosUsuarios", this);
    });

    private async Task CargarResultadosAsync()
    {
        ValidarPaginacion();
        Busqueda = string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda.Trim();
        RolFiltro = RolFiltro is "Administrador" or "Recepcionista" ? RolFiltro : null;
        TotalRegistros = await _usuarios.ContarAsync(Busqueda, RolFiltro, ActivoFiltro, HttpContext.RequestAborted);
        TotalPaginas = (int)Math.Ceiling((double)TotalRegistros / TamanoPagina);
        if (Pagina > TotalPaginas && TotalPaginas > 0) Pagina = TotalPaginas;
        if (TotalRegistros == 0) Pagina = 1;
        Usuarios = TotalRegistros == 0 ? [] : MapearAViewModel(await _usuarios.ListarAsync(
            Busqueda, RolFiltro, ActivoFiltro, Pagina, TamanoPagina, HttpContext.RequestAborted));
    }

    public Task<IActionResult> OnPostDesactivarAsync(int id) => CambiarEstadoAsync(id, false);

    public Task<IActionResult> OnPostReactivarAsync(int id) => CambiarEstadoAsync(id, true);

    private Task<IActionResult> CambiarEstadoAsync(int id, bool activo) => AtenderAsync(async () =>
    {
        var resultado = activo
            ? await _usuarios.ReactivarAsync(id, HttpContext.RequestAborted)
            : await _usuarios.DesactivarAsync(id, HttpContext.RequestAborted);
        if (resultado.Exito)
            TempData["MensajeExito"] = activo ? "Usuario reactivado correctamente." : "Usuario desactivado correctamente.";
        else
            TempData["MensajeError"] = resultado.Mensaje ?? (activo ? "Error al reactivar el usuario." : "Error al desactivar el usuario.");
        return RedirectToPage(new { Pagina, TamanoPagina, Busqueda, RolFiltro, ActivoFiltro });
    });

    private async Task<IActionResult> AtenderAsync(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Unauthorized) { return Challenge(); }
        catch (Exception error) when (error is HttpRequestException ||
            error is TaskCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "El servicio de usuarios no está disponible. Intenta nuevamente.");
        }
    }

    private void ValidarPaginacion()
    {
        if (Pagina < 1) Pagina = 1;
        if (TamanoPagina < 1) TamanoPagina = TamanoPaginaPredeterminado;
        if (TamanoPagina > TamanoPaginaMaximo) TamanoPagina = TamanoPaginaMaximo;
        if (Pagina > 10000) Pagina = 10000;
    }

    private static IReadOnlyList<UsuarioListViewModel> MapearAViewModel(
        IReadOnlyList<UsuarioListadoDto> dtos)
    {
        var lista = new List<UsuarioListViewModel>(dtos.Count);

        foreach (var dto in dtos)
        {
            lista.Add(new UsuarioListViewModel
            {
                Id = dto.Id,
                Username = dto.Username,
                Email = dto.Email,
                Rol = dto.Rol,
                Activo = dto.Activo,
                FechaCreacion = dto.FechaCreacion
            });
        }

        return lista;
    }
}
