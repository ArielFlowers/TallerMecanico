using System.Data;
using System.Data.Common;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data.Factories;
using TallerMecanico.Patterns.FactoryMethod;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class MySqlUnidadTrabajoOrden(
    DatabaseConnectionFactory connectionFactory, ILogger<MySqlUnidadTrabajoOrden> logger) : IUnidadTrabajoOrdenPort
{
    public ITransaccionOrden Iniciar()
    {
        DbConnection conexion = connectionFactory.CreateConnection();
        try
        {
            conexion.Open();
            return new Transaccion(conexion, connectionFactory, logger);
        }
        catch (DbException error)
        {
            conexion.Dispose();
            throw new PersistenciaOrdenException("No se pudo iniciar la transacción.", error);
        }
    }

    private sealed class Transaccion : ITransaccionOrden
    {
        private readonly DbConnection _conexion;
        private readonly DbTransaction _transaccion;
        private readonly ILogger _logger;
        private bool _terminada;
        public IOrdenPort Ordenes { get; }
        public IDetalleOrdenPort Detalles { get; }
        public IProductoStockOrdenPort Productos { get; }
        public ICabeceraOrdenPort Cabecera { get; }

        public Transaccion(DbConnection conexion, DatabaseConnectionFactory factory, ILogger logger)
        {
            _conexion = conexion;
            _logger = logger;
            _transaccion = conexion.BeginTransaction(IsolationLevel.ReadCommitted);
            Ordenes = new CreadorOrden(conexion, _transaccion).CrearRepositorio();
            Detalles = new CreadorDetalle(conexion, _transaccion).CrearRepositorio();
            Productos = new ProductoStockOrdenRepository(new ProductoRepository(factory), conexion, _transaccion);
            Cabecera = new CabeceraOrdenRepository(conexion, _transaccion);
        }

        public void Confirmar()
        {
            RepositorioOrdenSql.Ejecutar(() => { _transaccion.Commit(); return true; });
            _terminada = true;
        }

        public void Revertir()
        {
            if (_terminada) return;
            RepositorioOrdenSql.Ejecutar(() => { _transaccion.Rollback(); return true; });
            _terminada = true;
        }

        public void Dispose()
        {
            if (!_terminada)
            {
                try { Revertir(); }
                catch (PersistenciaOrdenException error) { _logger.LogError(error, "No se pudo confirmar el rollback de la orden."); }
            }
            _transaccion.Dispose();
            _conexion.Dispose();
        }
    }
}
