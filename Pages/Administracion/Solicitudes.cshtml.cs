using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Models;
using TallerMecanico.Services;

namespace TallerMecanico.Pages.Administracion;

[Authorize(Policy = "SoloAdministrador")]
public sealed class SolicitudesModel : PageModel
{
    private readonly RevisionSolicitudesService _revision;
    private readonly ILogger<SolicitudesModel> _logger;

    public SolicitudesModel(
        RevisionSolicitudesService revision,
        ILogger<SolicitudesModel> logger)
    {
        _revision = revision;
        _logger = logger;
    }

    public IReadOnlyList<SolicitudRegistro> Solicitudes { get; private set; }
        = Array.Empty<SolicitudRegistro>();

    public IActionResult OnGet()
    {
        Solicitudes = _revision.ObtenerPendientes();
        return Page();
    }

    public IActionResult OnPostAprobar(long id)
    {
        return Resolver(id, aprobar: true);
    }

    public IActionResult OnPostRechazar(long id)
    {
        return Resolver(id, aprobar: false);
    }

    private IActionResult Resolver(long id, bool aprobar)
    {
        if (id <= 0)
        {
            TempData["Error"] = "La solicitud no es valida.";
            return RedirectToPage();
        }

        try
        {
            bool resultado = aprobar
                ? _revision.Aprobar(id)
                : _revision.Rechazar(id);

            if (resultado)
            {
                TempData["Mensaje"] = aprobar
                    ? "Empleado aprobado correctamente."
                    : "Solicitud rechazada correctamente.";
            }
            else
            {
                TempData["Error"] =
                    "La solicitud ya fue procesada o no esta disponible.";
            }
        }
        catch (DbException ex)
        {
            _logger.LogError(
                ex,
                "Error de base de datos al resolver la solicitud {SolicitudId}",
                id);

            TempData["Error"] =
                "No se pudo procesar la solicitud. Verifica que no exista " +
                "un usuario duplicado.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Error al resolver la solicitud {SolicitudId}",
                id);

            TempData["Error"] =
                "No se pudo procesar la solicitud.";
        }

        return RedirectToPage();
    }
}