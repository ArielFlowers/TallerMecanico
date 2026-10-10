using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class OrdenRepository(DbConnection conexion, DbTransaction transaccion)
    : RepositorioOrdenSql(conexion, transaccion), IOrdenPort
{
    private const string Seleccionar = """
        SELECT Id, VehiculoId, MecanicoId, Fecha, Estado, Total, Token,
               CreadoPor, FechaCreacion, AnuladoPor, FechaAnulacion FROM Ordenes
        """;

    public Orden? ObtenerPorToken(Guid token) => Ejecutar(() =>
    {
        using DbCommand comando = CrearComando(Seleccionar + " WHERE Token = @Token;", ("@Token", token.ToString("D")));
        using DbDataReader lector = comando.ExecuteReader();
        return lector.Read() ? Mapear(lector) : null;
    });

    public Orden? ObtenerParaAnular(int id) => Ejecutar(() =>
    {
        using DbCommand comando = CrearComando(Seleccionar + " WHERE Id = @Id FOR UPDATE;", ("@Id", id));
        using DbDataReader lector = comando.ExecuteReader();
        return lector.Read() ? Mapear(lector) : null;
    });

    public void Insertar(Orden orden) => Ejecutar(() =>
    {
        using DbCommand comando = CrearComando("""
            INSERT INTO Ordenes (VehiculoId, MecanicoId, Fecha, Estado, Total, Token, CreadoPor, FechaCreacion)
            VALUES (@Vehiculo, @Mecanico, @Fecha, 'Activa', 0, @Token, @Usuario, @Creacion);
            """, ("@Vehiculo", orden.VehiculoId), ("@Mecanico", orden.MecanicoId),
            ("@Fecha", orden.Fecha), ("@Token", orden.Token.ToString("D")),
            ("@Usuario", orden.CreadoPor), ("@Creacion", orden.FechaCreacion));
        comando.ExecuteNonQuery();
        comando.Parameters.Clear();
        comando.CommandText = "SELECT LAST_INSERT_ID();";
        orden.Id = Convert.ToInt32(comando.ExecuteScalar());
        return orden.Id;
    }, insertandoToken: true);

    public void ActualizarTotal(int ordenId, decimal total) => Ejecutar(() =>
    {
        using DbCommand comando = CrearComando("UPDATE Ordenes SET Total = @Total WHERE Id = @Id;", ("@Total", total), ("@Id", ordenId));
        return comando.ExecuteNonQuery();
    });

    public bool MarcarAnulada(int ordenId, string usuario, DateTime fechaUtc) => Ejecutar(() =>
    {
        using DbCommand comando = CrearComando("""
            UPDATE Ordenes SET Estado = 'Anulada', AnuladoPor = @Usuario, FechaAnulacion = @Fecha
            WHERE Id = @Id AND Estado = 'Activa';
            """, ("@Usuario", usuario), ("@Fecha", fechaUtc), ("@Id", ordenId));
        return comando.ExecuteNonQuery() == 1;
    });

    private static Orden Mapear(DbDataReader lector) => new()
    {
        Id = lector.GetInt32(0), VehiculoId = lector.GetInt32(1), MecanicoId = lector.GetInt32(2),
        Fecha = lector.GetDateTime(3), Estado = Enum.Parse<EstadoOrden>(lector.GetString(4)),
        Total = lector.GetDecimal(5), Token = lector.GetGuid(6), CreadoPor = lector.GetString(7),
        FechaCreacion = lector.GetDateTime(8), AnuladoPor = lector.IsDBNull(9) ? null : lector.GetString(9),
        FechaAnulacion = lector.IsDBNull(10) ? null : lector.GetDateTime(10)
    };
}
