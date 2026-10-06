using TallerMecanico.Data;

namespace TallerMecanico.Patterns.FactoryMethod;

public abstract class CreadorRepositorio<T>
{
    public abstract IRepository<T> CrearRepositorio();
}
