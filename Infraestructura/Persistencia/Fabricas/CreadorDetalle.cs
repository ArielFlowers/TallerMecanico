using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Infraestructura.Adapters.MySql;

namespace TallerMecanico.Patterns.FactoryMethod;

public sealed class CreadorDetalle(DbConnection conexion, DbTransaction transaccion) : CreadorPuerto<IDetalleOrdenPort>
{
    public override IDetalleOrdenPort CrearRepositorio() => new DetalleOrdenRepository(conexion, transaccion);
}
