using System.Data.Common;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

public sealed class CreadorOrden(DbConnection conexion, DbTransaction transaccion) : CreadorPuerto<IOrdenRepository>
{
    public override IOrdenRepository CrearRepositorio() => new OrdenRepository(conexion, transaccion);
}
