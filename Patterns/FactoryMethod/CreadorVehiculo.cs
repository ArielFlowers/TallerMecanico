using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Patterns.FactoryMethod;

public class CreadorVehiculo : CreadorRepositorio<Vehiculo>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public CreadorVehiculo(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public override IRepository<Vehiculo> CrearRepositorio()
    {
        return new VehiculoRepository(_connectionFactory);
    }
}
