using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Infraestructura.Adapters.MySql;

namespace TallerMecanico.Patterns.FactoryMethod;

public sealed class CreadorOrden(DbConnection conexion, DbTransaction transaccion) : CreadorPuerto<IOrdenPort>
{
    public override IOrdenPort CrearRepositorio() => new OrdenRepository(conexion, transaccion);
}
