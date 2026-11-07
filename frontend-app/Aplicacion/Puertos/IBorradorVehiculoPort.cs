using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IBorradorVehiculoPort
{
    Guid Guardar(VehiculoFormViewModel formulario, string modal, string? buscar,
        Guid? ordenBorradorId = null, int? vehiculoSeleccionadoId = null);
    BorradorVehiculo? Obtener(Guid id);
    bool AsignarCliente(Guid id, int clienteId);
    void Eliminar(Guid id);
}
