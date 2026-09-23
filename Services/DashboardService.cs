using TallerMecanico.Data;
using TallerMecanico.Models;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class DashboardService
{
    private readonly IRepository<Servicio> _servicioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;

    public DashboardService(
        IRepository<Servicio> servicioRepository,
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
