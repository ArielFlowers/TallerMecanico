using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos.Dashboard;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Administrador,Recepcionista")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public DashboardViewModel Obtener() => dashboard.GetDashboardData();
}
