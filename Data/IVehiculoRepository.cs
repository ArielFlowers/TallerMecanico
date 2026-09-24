using TallerMecanico.Models;

namespace TallerMecanico.Data;

public interface IVehiculoRepository : IRepository<Vehiculo>
{
    List<Vehiculo> Search(string filtro);

    bool ExistsByPlaca(string placa, int idExcluido);
}
