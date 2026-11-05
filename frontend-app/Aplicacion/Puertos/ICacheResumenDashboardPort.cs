using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Puertos;

public interface ICacheResumenDashboardPort
{
    LecturaResumenDashboard ObtenerResumen();
    EstadoPublicacionResumen PublicarResumen(
        LecturaResumenDashboard lectura, string valor, TimeSpan expiracion);
    ResultadoEliminacionCache InvalidarResumen();
    TimeSpan? TiempoRestanteResumen();
}
