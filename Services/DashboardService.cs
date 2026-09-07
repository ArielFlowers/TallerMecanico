using TallerMecanico.Data;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class DashboardService
{
    private readonly ServicioRepository _servicioRepository;

    public DashboardService(ServicioRepository servicioRepository)
    {
        _servicioRepository = servicioRepository;
    }

    public DashboardViewModel GetDashboardData()
    {
        return new DashboardViewModel
        {
            MecanicosDisponibles = 0,
            VehiculosRegistrados = 0,
            ServiciosRegistrados = _servicioRepository.Count()
        };
    }
}
