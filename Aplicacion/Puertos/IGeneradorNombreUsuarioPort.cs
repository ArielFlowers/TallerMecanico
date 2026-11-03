namespace TallerMecanico.Aplicacion.Puertos;

public interface IGeneradorNombreUsuarioPort
{
    string GenerarUsuarioUnico(
        string correoElectronico,
        System.Threading.CancellationToken cancellationToken = default);
}
