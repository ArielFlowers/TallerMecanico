using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class CatalogosServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cliente_DuplicadoDuranteEscrituraConservaElErrorDeCi(bool actualizar)
    {
        var puertos = new PuertosConFallo(new RegistroDuplicadoException(new InvalidOperationException()));
        var servicio = new ClienteService(puertos, puertos, new ValidacionClientes(), new UsuarioPrueba());
        var formulario = ClientesAutocompletadoTests.Formulario();
        formulario.Id = 1;

        var resultado = actualizar ? servicio.Actualizar(formulario) : servicio.Crear(formulario);

        Assert.Equal("Ya existe un cliente registrado con este CI y complemento.",
            Assert.Single(resultado.Errores).Value);
        Assert.Contains(nameof(ClienteFormViewModel.Ci), resultado.Errores.Keys);
        Assert.Equal(1, puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Producto_DuplicadoDuranteEscrituraConservaElErrorDeCodigo(bool actualizar)
    {
        var puertos = new PuertosConFallo(new RegistroDuplicadoException(new InvalidOperationException()));
        var servicio = new ProductoService(puertos, new ValidacionProductos(), new UsuarioPrueba());
        var formulario = ProductosAutocompletadoTests.Formulario();
        formulario.Id = 1;

        var resultado = actualizar ? servicio.Actualizar(formulario) : servicio.Crear(formulario);

        Assert.Equal("Ya existe un producto registrado con este código.", Assert.Single(resultado.Errores).Value);
        Assert.Contains(nameof(ProductoFormViewModel.Codigo), resultado.Errores.Keys);
        Assert.Equal(1, puertos.Escrituras);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Vehiculo_ConflictoDuranteEscrituraConservaElCampoYNoInvalidaCache(bool actualizar, bool referencia)
    {
        Exception error = referencia
            ? new ReferenciaInexistenteException(new InvalidOperationException())
            : new RegistroDuplicadoException(new InvalidOperationException());
        var puertos = new PuertosConFallo(error);
        var cache = new CachePrueba();
        var servicio = new VehiculoService(puertos, puertos, new ValidacionVehiculos(), new UsuarioPrueba(), cache);
        var formulario = VehiculosAutocompletadoTests.Formulario(1);
        formulario.Id = 1;

        var resultado = actualizar ? servicio.Update(formulario) : servicio.Create(formulario);

        Assert.Equal(referencia ? "El cliente seleccionado no existe." : "Esta placa ya está registrada.",
            Assert.Single(resultado.Errores).Value);
        Assert.Contains(referencia ? nameof(VehiculoFormViewModel.ClienteId) : nameof(VehiculoFormViewModel.Placa),
            resultado.Errores.Keys);
        Assert.Equal(1, puertos.Escrituras);
        Assert.Equal(0, cache.Invalidaciones);
    }

    [Theory]
    [InlineData("Cliente", "No se puede eliminar el cliente porque tiene vehículos asociados.")]
    [InlineData("Producto", "No se puede eliminar el producto porque tiene órdenes asociadas.")]
    [InlineData("Vehiculo", "No se puede eliminar el vehículo porque tiene órdenes asociadas.")]
    public void Eliminar_RelacionCreadaTrasLaConsultaConservaMensaje(string catalogo, string mensaje)
    {
        var puertos = new PuertosConFallo(new RegistroRelacionadoException(new InvalidOperationException()));
        var cache = new CachePrueba();

        Assert.Equal(mensaje, Eliminar(catalogo, puertos, cache));
        Assert.Equal(1, puertos.Escrituras);
        Assert.Equal(0, cache.Invalidaciones);
    }

    [Theory]
    [InlineData("Cliente", "Crear")]
    [InlineData("Cliente", "Actualizar")]
    [InlineData("Cliente", "Eliminar")]
    [InlineData("Producto", "Crear")]
    [InlineData("Producto", "Actualizar")]
    [InlineData("Producto", "Eliminar")]
    [InlineData("Vehiculo", "Crear")]
    [InlineData("Vehiculo", "Actualizar")]
    [InlineData("Vehiculo", "Eliminar")]
    public void OtrosErrores_NoSeOcultanComoDuplicadosORelaciones(string catalogo, string operacion)
    {
        var error = new InvalidOperationException("Fallo de persistencia no relacionado.");
        var puertos = new PuertosConFallo(error);
        var cache = new CachePrueba();
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
        {
            if (operacion == "Eliminar") { Eliminar(catalogo, puertos, cache); return; }
            bool actualizar = operacion == "Actualizar";
            switch (catalogo)
            {
                case "Cliente":
                    var clientes = new ClienteService(puertos, puertos, new ValidacionClientes(), new UsuarioPrueba());
                    var cliente = ClientesAutocompletadoTests.Formulario(); cliente.Id = 1;
                    if (actualizar) clientes.Actualizar(cliente); else clientes.Crear(cliente);
                    break;
                case "Producto":
                    var productos = new ProductoService(puertos, new ValidacionProductos(), new UsuarioPrueba());
                    var producto = ProductosAutocompletadoTests.Formulario(); producto.Id = 1;
                    if (actualizar) productos.Actualizar(producto); else productos.Crear(producto);
                    break;
                default:
                    var vehiculos = new VehiculoService(puertos, puertos, new ValidacionVehiculos(), cache: cache);
                    var vehiculo = VehiculosAutocompletadoTests.Formulario(1); vehiculo.Id = 1;
                    if (actualizar) vehiculos.Update(vehiculo); else vehiculos.Create(vehiculo);
                    break;
            }
        }));
        Assert.Equal(1, puertos.Escrituras);
        Assert.Equal(0, cache.Invalidaciones);
    }

    private static string? Eliminar(string catalogo, PuertosConFallo puertos, CachePrueba cache) => catalogo switch
    {
        "Cliente" => new ClienteService(puertos, puertos, new ValidacionClientes(), new UsuarioPrueba()).Eliminar(1),
        "Producto" => new ProductoService(puertos, new ValidacionProductos(), new UsuarioPrueba()).Eliminar(1),
        _ => new VehiculoService(puertos, puertos, new ValidacionVehiculos(), cache: cache).Delete(1)
    };

    private sealed class PuertosConFallo(Exception error) : IClientePort, IProductoPort, IVehiculoPort
    {
        public int Escrituras { get; private set; }
        private void FallarEscritura() { Escrituras++; throw error; }
        IReadOnlyList<Cliente> IClientePort.GetAll() => [];
        IReadOnlyList<Producto> IProductoPort.GetAll() => [];
        IReadOnlyList<Vehiculo> IVehiculoPort.GetAll() => [];
        Cliente? IClientePort.GetById(int id) => new() { Id = id };
        Producto? IProductoPort.GetById(int id) => new() { Id = id };
        Vehiculo? IVehiculoPort.GetById(int id) => new() { Id = id };
        IReadOnlyList<Cliente> IClientePort.Search(string termino) => [];
        IReadOnlyList<Producto> IProductoPort.Search(string termino) => [];
        IReadOnlyList<Vehiculo> IVehiculoPort.Search(string termino) => [];
        Task<IReadOnlyList<SugerenciaCliente>> IClientePort.SugerirAsync(string termino, CancellationToken token) => Task.FromResult<IReadOnlyList<SugerenciaCliente>>([]);
        Task<IReadOnlyList<SugerenciaProducto>> IProductoPort.SugerirAsync(string termino, CancellationToken token) => Task.FromResult<IReadOnlyList<SugerenciaProducto>>([]);
        Task<IReadOnlyList<SugerenciaVehiculo>> IVehiculoPort.SugerirAsync(string termino, CancellationToken token) => Task.FromResult<IReadOnlyList<SugerenciaVehiculo>>([]);
        public bool ExistsByCi(string ci, string complemento, int idExcluido) => false;
        public bool ExistsByCodigo(string codigo, int idExcluido) => false;
        public bool ExistsByPlaca(string placa, int idExcluido) => false;
        public bool ExistsPorCliente(int clienteId) => false;
        public void Add(Cliente cliente) => FallarEscritura();
        public void Add(Producto producto) => FallarEscritura();
        public void Add(Vehiculo vehiculo) => FallarEscritura();
        public void Update(Cliente cliente) => FallarEscritura();
        public void Update(Producto producto) => FallarEscritura();
        public void Update(Vehiculo vehiculo) => FallarEscritura();
        public void Delete(int id) => FallarEscritura();
        public bool Descontar(int productoId, int cantidad) => throw new NotSupportedException();
        public void Restituir(int productoId, int cantidad) => throw new NotSupportedException();
    }

    private sealed class CachePrueba : ICacheResumenDashboardPort
    {
        public int Invalidaciones { get; private set; }
        public ResultadoEliminacionCache InvalidarResumen() { Invalidaciones++; return ResultadoEliminacionCache.Eliminada; }
        public LecturaResumenDashboard ObtenerResumen() => throw new NotSupportedException();
        public EstadoPublicacionResumen PublicarResumen(LecturaResumenDashboard lectura, string valor, TimeSpan expiracion) => throw new NotSupportedException();
        public TimeSpan? TiempoRestanteResumen() => null;
    }
}
