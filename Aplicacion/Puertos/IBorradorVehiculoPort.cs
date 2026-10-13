using TallerMecanico.ViewModels;

namespace TallerMecanico.Application.Ports;

public interface IBorradorVehiculoPort
{
    Guid Guardar(VehiculoFormViewModel formulario, string modal, string? buscar, Guid? ordenBorradorId = null);
    BorradorVehiculo? Obtener(Guid id);
    bool AsignarCliente(Guid id, int clienteId);
    void Eliminar(Guid id);
}
