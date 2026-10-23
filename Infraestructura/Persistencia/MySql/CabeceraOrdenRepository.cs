using System.Data.Common;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

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

    public bool ExisteServicio(int servicioId, out Servicio? servicio)
    {
        Servicio? encontrado = Ejecutar(() =>
        {
            using DbCommand comando = CrearComando(
                "SELECT Id, Nombre, Descripcion, Costo, TiempoEstimadoHoras, UnidadCantidad, CodigoCatalogo FROM Servicios WHERE Id = @Id FOR SHARE;",
                ("@Id", servicioId));
            using DbDataReader lector = comando.ExecuteReader();
            if (!lector.Read()) return (Servicio?)null;
            return new Servicio
            {
                Id = lector.GetInt32(0),
                Nombre = lector.GetString(1),
                Descripcion = lector.GetString(2),
                Costo = lector.GetDecimal(3),
                TiempoEstimadoHoras = lector.GetDecimal(4),
                UnidadCantidad = lector.IsDBNull(5) ? null : lector.GetString(5),
                CodigoCatalogo = lector.IsDBNull(6) ? null : lector.GetString(6)
            };
        });
        servicio = encontrado;
        return encontrado is not null;
    }
}
