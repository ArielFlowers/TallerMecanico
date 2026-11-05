using TallerMecanico.Aplicacion.DTOs;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IActivacionInvitacionPort
{
    ResultadoActivacionInvitacion ActivarPorTokenHash(
        string tokenHash);
}