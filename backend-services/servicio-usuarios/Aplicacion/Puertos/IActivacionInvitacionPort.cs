using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IActivacionInvitacionPort
{
    ResultadoActivacionInvitacion ActivarPorTokenHash(
        string tokenHash);
}