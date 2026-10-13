using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

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
