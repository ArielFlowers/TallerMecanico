namespace TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

public interface ITokenOrden
{
    Guid Generar();
    bool EsValido(Guid token);
}
