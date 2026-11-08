namespace TallerMecanico.Sesion;

public interface ITokenOrden
{
    Guid Generar();
    bool EsValido(Guid token);
}
