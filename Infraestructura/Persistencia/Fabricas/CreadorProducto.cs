using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Models;

namespace TallerMecanico.Patterns.FactoryMethod;

public class CreadorProducto :
    CreadorRepositorio<Producto>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public CreadorProducto(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory;
    }

    public override IRepository<Producto> CrearRepositorio()
    {
        return new ProductoRepository(
            _connectionFactory);
    }
}