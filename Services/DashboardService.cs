using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class DashboardService
{
    public DashboardViewModel GetDashboardData()
    {
        return new DashboardViewModel
        {
            MecanicosDisponibles = 0,
            VehiculosEnTaller = 0,
            OrdenesActivas = 0,
            OrdenesPendientes = 0
        };
    }
}