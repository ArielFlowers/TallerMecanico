namespace TallerMecanico.Aplicacion.Puertos;

public interface ITokenOrdenPort
{
    Guid Generar();
    bool EsValido(Guid token);
}
