using TallerMecanico.Data;

namespace TallerMecanico.Patterns.FactoryMethod;

public abstract class CreadorRepositorio
{
    public abstract IRepository CrearRepositorio();
}
