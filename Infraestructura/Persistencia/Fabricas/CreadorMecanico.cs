using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

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
