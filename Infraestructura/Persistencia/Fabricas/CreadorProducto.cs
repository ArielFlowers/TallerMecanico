using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

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