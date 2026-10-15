using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IBorradorOrdenPort
{
    Guid Guardar(OrdenFormViewModel formulario);
    OrdenFormViewModel? Obtener(Guid id);
    void Eliminar(Guid id);
}
