using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Servicios;

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