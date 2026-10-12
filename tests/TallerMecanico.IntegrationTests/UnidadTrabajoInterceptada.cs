using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

namespace TallerMecanico.IntegrationTests;

// Intercepta operaciones reales para forzar fallos o concurrencia, sin simular MySQL.
internal sealed class UnidadTrabajoInterceptada(
    IUnidadTrabajoOrdenPort real,
    Action<string, int>? antes = null,
    Action<string, int>? despues = null) : IUnidadTrabajoOrdenPort
{
    public ITransaccionOrden Iniciar() => new Transaccion(real.Iniciar(), antes, despues);

    private sealed class Transaccion : ITransaccionOrden
    {
        private readonly ITransaccionOrden _real;
        private readonly Action<string, int>? _antes;
        private readonly Action<string, int>? _despues;
        private readonly Dictionary<string, int> _cuentas = [];
        public IOrdenPort Ordenes { get; }
        public IDetalleOrdenPort Detalles { get; }
        public IProductoStockOrdenPort Productos { get; }
        public ICabeceraOrdenPort Cabecera => _real.Cabecera;

        public Transaccion(ITransaccionOrden real, Action<string, int>? antes, Action<string, int>? despues)
        {
            _real = real; _antes = antes; _despues = despues;
            Ordenes = new OrdenInterceptada(real.Ordenes, this);
            Detalles = new DetalleInterceptado(real.Detalles, this);
            Productos = new ProductoInterceptado(real.Productos, this);
        }

        private T Ejecutar<T>(string operacion, Func<T> accion)
        {
            int cuenta = _cuentas[operacion] = _cuentas.GetValueOrDefault(operacion) + 1;
            _antes?.Invoke(operacion, cuenta);
            T resultado = accion();
            _despues?.Invoke(operacion, cuenta);
            return resultado;
        }

        public void Confirmar() => _real.Confirmar();
        public void Revertir() => _real.Revertir();
        public void Dispose() => _real.Dispose();

        private sealed class OrdenInterceptada(IOrdenPort real, Transaccion contexto) : IOrdenPort
        {
            public Orden? ObtenerPorToken(Guid token) => contexto.Ejecutar("ObtenerToken", () => real.ObtenerPorToken(token));
            public Orden? ObtenerParaAnular(int id) => contexto.Ejecutar("ObtenerAnular", () => real.ObtenerParaAnular(id));
            public void Insertar(Orden orden) => real.Insertar(orden);
            public void ActualizarTotal(int ordenId, decimal total) => real.ActualizarTotal(ordenId, total);
            public bool MarcarAnulada(int ordenId, string usuario, DateTime fechaUtc) => real.MarcarAnulada(ordenId, usuario, fechaUtc);
        }

        private sealed class DetalleInterceptado(IDetalleOrdenPort real, Transaccion contexto) : IDetalleOrdenPort
        {
            public IReadOnlyList<DetalleOrden> ObtenerPorOrden(int ordenId) => real.ObtenerPorOrden(ordenId);
            public void Insertar(DetalleOrden detalle) => contexto.Ejecutar("InsertarDetalle", () => { real.Insertar(detalle); return true; });
        }

        private sealed class ProductoInterceptado(IProductoStockOrdenPort real, Transaccion contexto) : IProductoStockOrdenPort
        {
            public IReadOnlyList<Producto> BloquearPorId(IEnumerable<int> ids) => real.BloquearPorId(ids);
            public bool Descontar(int id, int cantidad) => contexto.Ejecutar("Descontar", () => real.Descontar(id, cantidad));
            public bool Restituir(int id, int cantidad) => contexto.Ejecutar("Restituir", () => real.Restituir(id, cantidad));
        }
    }
}
