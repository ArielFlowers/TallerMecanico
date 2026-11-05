using System.Data.Common;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

public sealed class CreadorDetalle(DbConnection conexion, DbTransaction transaccion) : CreadorPuerto<IDetalleOrdenRepository>
{
    public override IDetalleOrdenRepository CrearRepositorio() => new DetalleOrdenRepository(conexion, transaccion);
}
