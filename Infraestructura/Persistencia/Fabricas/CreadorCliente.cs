using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

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