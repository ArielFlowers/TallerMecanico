using System.Data.Common;
using MySqlConnector;
using TallerMecanico.Application.Ordenes;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public abstract class RepositorioOrdenSql(DbConnection conexion, DbTransaction transaccion)
{
    protected DbCommand CrearComando(string sql, params (string Nombre, object? Valor)[] parametros)
        => CrearComandoCompartido(conexion, transaccion, sql, parametros);

    internal static DbCommand CrearComandoCompartido(DbConnection conexion, DbTransaction transaccion,
        string sql, params (string Nombre, object? Valor)[] parametros)
    {
        if (!ReferenceEquals(transaccion.Connection, conexion))
            throw new InvalidOperationException("La conexión y la transacción deben pertenecer a la misma unidad de trabajo.");
        DbCommand comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            DbParameter parametro = comando.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor ?? DBNull.Value;
            comando.Parameters.Add(parametro);
        }
        return comando;
    }

    internal static T Ejecutar<T>(Func<T> operacion, bool insertandoToken = false)
    {
        try { return operacion(); }
        catch (MySqlException error) when (insertandoToken && error.Number == 1062)
        { throw new TokenOrdenDuplicadoException(error); }
        catch (DbException error)
        { throw new PersistenciaOrdenException("Falló una operación de persistencia de la orden.", error); }
    }
}
