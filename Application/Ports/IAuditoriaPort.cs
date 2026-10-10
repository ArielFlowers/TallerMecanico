
using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IAuditoriaPort
{
    void Registrar(RegistroAuditoria registro);

    IReadOnlyList<RegistroAuditoria> ObtenerRecientes(
        int cantidad);
}
