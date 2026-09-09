using TallerMecanico.Models;

namespace TallerMecanico.Services;

public interface IVehiculoService
{
    List<Vehiculo> ObtenerTodos();

    Vehiculo? ObtenerPorId(int id);

    void Crear(Vehiculo vehiculo);

    void Actualizar(Vehiculo vehiculo);

    void Eliminar(int id);

    bool PlacaRegistrada(string placa, int idExcluido);
}
