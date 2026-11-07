using TallerMecanico.Aplicacion.DTOs;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IUsuarioAdministracionPort : IUsuarioPort, IUsuarioConsultaPort
{
    int ContarAdministradoresActivos();

    Usuario? ObtenerPorIdConCredenciales(int id);

    bool ActualizarUsuarioAtomico(int id, string email, string rol, bool activo, string modificadoPor, out string errorMessage);

    bool DesactivarUsuarioAtomico(int id, string modificadoPor, out string errorMessage);
}