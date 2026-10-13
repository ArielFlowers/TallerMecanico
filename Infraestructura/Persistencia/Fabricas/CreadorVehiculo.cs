using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

public class CreadorVehiculo :
    CreadorRepositorio<Vehiculo>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public CreadorVehiculo(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory;
    }

    public override IRepository<Vehiculo> CrearRepositorio()
    {
        return new VehiculoRepository(
            _connectionFactory);
    }
}