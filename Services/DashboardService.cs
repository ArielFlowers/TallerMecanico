using TallerMecanico.Data;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class DashboardService
{
    private readonly ServicioRepository _servicioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;

    public DashboardService(
        ServicioRepository servicioRepository,
        IVehiculoRepository vehiculoRepository)
    {
        _servicioRepository = servicioRepository;
        _vehiculoRepository = vehiculoRepository;
    }

    public DashboardViewModel GetDashboardData()
    {
        return new DashboardViewModel
        {
            MecanicosDisponibles = 0,
            VehiculosRegistrados = _vehiculoRepository.Count(),
            ServiciosRegistrados = _servicioRepository.Count()
        };
    }
}
