using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Servicios;

public interface IServicioService
{
    List<Servicio> ObtenerTodos();
    Servicio? ObtenerPorId(int id);
    void Crear(Servicio servicio);
    void Actualizar(Servicio servicio);
    void Eliminar(int id);
}