using System.Data.Common;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

public sealed class CreadorDetalle(DbConnection conexion, DbTransaction transaccion) : CreadorPuerto<IDetalleOrdenPort>
{
    public override IDetalleOrdenPort CrearRepositorio() => new DetalleOrdenRepository(conexion, transaccion);
}
