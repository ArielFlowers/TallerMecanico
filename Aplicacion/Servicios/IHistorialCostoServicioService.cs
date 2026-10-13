using TallerMecanico.Models;

namespace TallerMecanico.Services;

public interface IHistorialCostoServicioService
{
    IReadOnlyList<HistorialCostoServicio> ObtenerHistorial();
}