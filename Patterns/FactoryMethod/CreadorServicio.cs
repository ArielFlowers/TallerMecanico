using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Patterns.FactoryMethod;

public class CreadorServicio : CreadorRepositorio<Servicio>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public CreadorServicio(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public override IRepository<Servicio> CrearRepositorio()
    {
        return new ServicioRepository(_connectionFactory);
    }
}
