using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos.Dashboard;
using TallerMecanico.Contratos;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.Personal)]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public DashboardViewModel Obtener() => dashboard.GetDashboardData();
}
