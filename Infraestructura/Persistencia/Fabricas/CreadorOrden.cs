using System.Data.Common;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

public sealed class CreadorOrden(DbConnection conexion, DbTransaction transaccion) : CreadorPuerto<IOrdenPort>
{
    public override IOrdenPort CrearRepositorio() => new OrdenRepository(conexion, transaccion);
}
