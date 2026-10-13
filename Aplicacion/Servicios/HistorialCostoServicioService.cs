using TallerMecanico.Data;
using TallerMecanico.Models;

namespace TallerMecanico.Services;

public class HistorialCostoServicioService
    : IHistorialCostoServicioService
{
    private readonly IHistorialCostoServicioRepository _historialRepository;

    public HistorialCostoServicioService(
        IHistorialCostoServicioRepository historialRepository)
    {
        _historialRepository = historialRepository;
    }

    public IReadOnlyList<HistorialCostoServicio> ObtenerHistorial()
    {
        return _historialRepository.GetAll();
    }
}