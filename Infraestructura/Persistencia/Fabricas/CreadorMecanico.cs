using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Patterns.FactoryMethod;

public class CreadorMecanico : CreadorRepositorio<Mecanico>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public CreadorMecanico(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public override IRepository<Mecanico> CrearRepositorio()
    {
        return new MecanicoRepository(_connectionFactory);
    }
}
