using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface ISolicitudRegistroPort
{
    bool ExisteUsername(string username);

    bool ExisteEmail(string email);

    void Agregar(SolicitudRegistro solicitud);

    SolicitudRegistro? ObtenerPorId(long id);

    SolicitudRegistro? ObtenerPorTokenHash(string tokenHash);

    SolicitudRegistro? ObtenerPendientePorEmail(string email);

    bool IntentarRenovarTokenConLimite(
        long solicitudId,
        string nuevoTokenHash,
        DateTime nuevaExpiracion);
    bool ConfirmarEmail(string tokenHash);


    void Actualizar(SolicitudRegistro solicitud);
}