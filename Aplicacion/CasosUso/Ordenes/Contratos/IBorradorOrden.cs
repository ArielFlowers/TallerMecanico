using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

public interface IBorradorOrden
{
    Guid Guardar(OrdenFormViewModel formulario);
    OrdenFormViewModel? Obtener(Guid id);
    void Eliminar(Guid id);
}
