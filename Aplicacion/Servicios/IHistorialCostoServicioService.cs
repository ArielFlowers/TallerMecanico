using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Servicios;

public interface IHistorialCostoServicioService
{
    IReadOnlyList<HistorialCostoServicio> ObtenerHistorial();
}