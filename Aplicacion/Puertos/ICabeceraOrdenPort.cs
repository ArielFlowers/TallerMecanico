namespace TallerMecanico.Aplicacion.Puertos;

using TallerMecanico.Dominio.Entidades;

public interface ICabeceraOrdenPort
{
    bool ExisteVehiculo(int vehiculoId, out int? clienteId);
    bool ExisteMecanico(int mecanicoId);
    bool ExisteServicio(int servicioId, out Servicio? servicio);
}
