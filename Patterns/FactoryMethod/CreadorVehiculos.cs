using TallerMecanico.Data;

namespace TallerMecanico.Patterns.FactoryMethod;

public class CreadorVehiculos : CreadorRepositorio
{
    private readonly DatabaseConnection _databaseConnection;

    public CreadorVehiculos(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public override IRepository CrearRepositorio()
    {
        return new VehiculoRepository(_databaseConnection);
    }
}
