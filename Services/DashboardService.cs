using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class DashboardService
{
    public DashboardViewModel GetDashboardData()
    {
        return new DashboardViewModel
        {
            MecanicosDisponibles = 0,
            VehiculosRegistrados = 0,
            ServiciosRegistrados = 0
        };
    }
}