using TallerMecanico.ViewModels;

namespace TallerMecanico.Application.Ports;

public interface IBorradorOrdenPort
{
    Guid Guardar(OrdenFormViewModel formulario);
    OrdenFormViewModel? Obtener(Guid id);
    void Eliminar(Guid id);
}
