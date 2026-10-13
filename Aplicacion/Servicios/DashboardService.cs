using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Servicios;

public class DashboardService
{
    private readonly IRepository<Servicio> _servicioRepository;
    private readonly IRepository<Vehiculo> _vehiculoRepository;

    public DashboardService(
        IRepository<Servicio> servicioRepository,
        IRepository<Vehiculo> vehiculoRepository)
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
