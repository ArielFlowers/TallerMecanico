
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IAuditoriaPort
{
    void Registrar(RegistroAuditoria registro);

    IReadOnlyList<RegistroAuditoria> ObtenerRecientes(
        int cantidad);
}
