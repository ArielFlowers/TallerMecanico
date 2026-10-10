namespace TallerMecanico.Application.Ports;

public interface ITokenOrdenPort
{
    Guid Generar();
    bool EsValido(Guid token);
}
