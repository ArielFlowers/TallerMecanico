namespace TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

using TallerMecanico.Dominio.Entidades;

public interface ICabeceraOrdenRepository
{
    bool ExisteVehiculo(int vehiculoId, out int? clienteId);
    bool ExisteMecanico(int mecanicoId);
    bool ExisteServicio(int servicioId, out Servicio? servicio);
}
