using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

public abstract class CreadorRepositorio<T>
{
    public abstract IRepository<T> CrearRepositorio();
}
