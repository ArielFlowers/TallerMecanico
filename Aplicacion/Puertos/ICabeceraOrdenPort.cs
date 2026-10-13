namespace TallerMecanico.Application.Ports;

public interface ICabeceraOrdenPort
{
    bool ExisteVehiculo(int vehiculoId, out int? clienteId);
    bool ExisteMecanico(int mecanicoId);
}
