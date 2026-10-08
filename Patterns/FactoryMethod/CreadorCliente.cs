using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Models;

namespace TallerMecanico.Patterns.FactoryMethod;

public class CreadorCliente :
    CreadorRepositorio<Cliente>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public CreadorCliente(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public override IRepository<Cliente> CrearRepositorio()
    {
        return new ClienteRepository(
            _connectionFactory);
    }
}