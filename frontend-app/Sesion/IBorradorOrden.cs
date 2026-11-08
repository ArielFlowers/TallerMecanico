using TallerMecanico.Pages.Ordenes;

namespace TallerMecanico.Sesion;

public interface IBorradorOrden
{
    Guid Guardar(OrdenFormViewModel formulario);
    OrdenFormViewModel? Obtener(Guid id);
    void Eliminar(Guid id);
}
