using System.Data.Common;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class CabeceraOrdenRepository(DbConnection conexion, DbTransaction transaccion)
    : RepositorioOrdenSql(conexion, transaccion), ICabeceraOrdenPort
{
    public bool ExisteVehiculo(int vehiculoId, out int? clienteId)
    {
        var datos = Ejecutar(() =>
        {
            using DbCommand comando = CrearComando("SELECT ClienteId FROM Vehiculos WHERE Id = @Id FOR SHARE;", ("@Id", vehiculoId));
            using DbDataReader lector = comando.ExecuteReader();
            if (!lector.Read()) return (Existe: false, Cliente: (int?)null);
            return (Existe: true, Cliente: lector.IsDBNull(0) ? null : (int?)lector.GetInt32(0));
        });
        clienteId = datos.Cliente;
        return datos.Existe;
    }

    public bool ExisteMecanico(int mecanicoId) => Ejecutar(() =>
    {
        using DbCommand comando = CrearComando("SELECT Id FROM Mecanicos WHERE Id = @Id FOR SHARE;", ("@Id", mecanicoId));
        return comando.ExecuteScalar() is not null;
    });
}
