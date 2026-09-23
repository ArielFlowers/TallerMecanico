using TallerMecanico.Data;

namespace TallerMecanico.Patterns.FactoryMethod;

public class CreadorMecanico : CreadorRepositorio
{
    private readonly DatabaseConnection _databaseConnection;

    public CreadorMecanico(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public override IRepository CrearRepositorio()
    {
        return new MecanicoRepository(_databaseConnection);
    }
}
