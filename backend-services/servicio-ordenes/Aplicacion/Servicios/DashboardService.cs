using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Contratos.Dashboard;

namespace TallerMecanico.Aplicacion.Servicios;

public class DashboardService
{
    private readonly IRepository<Servicio> _servicioRepository;
    private readonly IRepository<Vehiculo> _vehiculoRepository;
    private readonly IRepository<Mecanico> _mecanicoRepository;

    public DashboardService(
        IRepository<Servicio> servicioRepository,
        IRepository<Vehiculo> vehiculoRepository,
        IRepository<Mecanico> mecanicoRepository)
    {
        _servicioRepository = servicioRepository;
        _vehiculoRepository = vehiculoRepository;
        _mecanicoRepository = mecanicoRepository;
    }

    public DashboardViewModel GetDashboardData()
    {
        return new DashboardViewModel
        {
            MecanicosDisponibles = _mecanicoRepository.Count(),
            VehiculosRegistrados = _vehiculoRepository.Count(),
            ServiciosRegistrados = _servicioRepository.Count()
        };
    }
}
