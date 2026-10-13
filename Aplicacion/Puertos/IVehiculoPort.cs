using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IVehiculoPort
{
    IReadOnlyList<Vehiculo> GetAll();

    Vehiculo? GetById(int id);

    IReadOnlyList<Vehiculo> Search(
        string filtro);

    bool ExistsByPlaca(
        string placa,
        int idExcluido = 0);

    bool ExistsPorCliente(
        int clienteId);

    void Add(
        Vehiculo vehiculo);

    void Update(
        Vehiculo vehiculo);

    void Delete(
        int id);
}