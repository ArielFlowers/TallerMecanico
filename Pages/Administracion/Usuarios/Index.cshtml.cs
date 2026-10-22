using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.DTOs;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Pages.Administracion.Usuarios;

namespace TallerMecanico.Pages.Administracion.Usuarios;

[Authorize(Policy = "SoloAdministrador")]
public sealed class IndexModel : PageModel
{
    private const int TamanoPaginaPredeterminado = 10;
    private const int TamanoPaginaMaximo = 100;

    private readonly IUsuarioConsultaPort _usuarioConsultaPort;
    private readonly UsuarioAdministracionService _usuarioAdministracion;

    public IndexModel(
        IUsuarioConsultaPort usuarioConsultaPort,
        UsuarioAdministracionService usuarioAdministracion)
    {
        _usuarioConsultaPort = usuarioConsultaPort;
        _usuarioAdministracion = usuarioAdministracion;
    }

    public IReadOnlyList<UsuarioListViewModel> Usuarios { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int TamanoPagina { get; set; } = TamanoPaginaPredeterminado;

    public int TotalRegistros { get; private set; }

    public int TotalPaginas { get; private set; }

    public bool HayPaginaAnterior => Pagina > 1;

    public bool HayPaginaSiguiente => Pagina < TotalPaginas;

    public void OnGet()
    {
        ValidarPaginacion();

        TotalRegistros = _usuarioConsultaPort.ContarTotal();
        TotalPaginas = (int)Math.Ceiling((double)TotalRegistros / TamanoPagina);

        if (Pagina > TotalPaginas && TotalPaginas > 0)
        {
            Pagina = TotalPaginas;
        }

        if (TotalRegistros > 0)
        {
            var dtos = _usuarioConsultaPort.Listar(Pagina, TamanoPagina);
            Usuarios = MapearAViewModel(dtos);
        }
    }

    public IActionResult OnPostDesactivar(int id)
    {
        bool resultado = _usuarioAdministracion.DesactivarUsuario(id, out string? errorMessage);

        if (!resultado)
        {
            TempData["MensajeError"] = errorMessage ?? "Error al desactivar el usuario.";
        }
        else
        {
            TempData["MensajeExito"] = "Usuario desactivado correctamente.";
        }

        return RedirectToPage(new { Pagina, TamanoPagina });
    }

    public IActionResult OnPostReactivar(int id)
    {
        bool resultado = _usuarioAdministracion.ReactivarUsuario(id, out string? errorMessage);

        if (!resultado)
        {
            TempData["MensajeError"] = errorMessage ?? "Error al reactivar el usuario.";
        }
        else
        {
            TempData["MensajeExito"] = "Usuario reactivado correctamente.";
        }

        return RedirectToPage(new { Pagina, TamanoPagina });
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