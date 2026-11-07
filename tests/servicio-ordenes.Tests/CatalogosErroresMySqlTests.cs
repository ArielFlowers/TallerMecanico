using MySqlConnector;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Datos")]
public sealed class CatalogosErroresMySqlTests
{
    [MySqlAisladoTheory]
    [InlineData("Cliente", false)]
    [InlineData("Cliente", true)]
    [InlineData("Producto", false)]
    [InlineData("Producto", true)]
    [InlineData("Vehiculo", false)]
    [InlineData("Vehiculo", true)]
    public void Duplicados_Traduce1062SinModificarRegistros(string catalogo, bool actualizar)
    {
        using var aislada = new MySqlAisladoFixture();
        var clientes = new ClienteRepository(aislada.Factory);
        var primero = ClientePrueba("1234567"); clientes.Add(primero);
        RegistroDuplicadoException error;
        switch (catalogo)
        {
            case "Cliente":
                var segundo = ClientePrueba("7654321");
                if (actualizar) clientes.Add(segundo);
                segundo.Ci = primero.Ci;
                error = Assert.Throws<RegistroDuplicadoException>(() =>
                { if (actualizar) clientes.Update(segundo); else clientes.Add(segundo); });
                Assert.Equal(actualizar ? 2 : 1, clientes.GetAll().Count);
                if (actualizar) Assert.Equal("7654321", clientes.GetById(segundo.Id)!.Ci);
                break;
            case "Producto":
                var productos = new ProductoRepository(aislada.Factory);
                productos.Add(ProductoPrueba("FIL-001"));
                var producto = ProductoPrueba("FIL-002");
                if (actualizar) { productos.Add(producto); producto = productos.Search("FIL-002").Single(); }
                producto.Codigo = "FIL-001";
                error = Assert.Throws<RegistroDuplicadoException>(() =>
                { if (actualizar) productos.Update(producto); else productos.Add(producto); });
                Assert.Equal(actualizar ? 2 : 1, productos.GetAll().Count);
                if (actualizar) Assert.Equal("FIL-002", productos.GetById(producto.Id)!.Codigo);
                Assert.All(productos.GetAll(), registro => Assert.Equal(10, registro.Stock));
                break;
            default:
                var vehiculos = new VehiculoRepository(aislada.Factory);
                vehiculos.Add(VehiculoPrueba("123ABC", primero.Id));
                var vehiculo = VehiculoPrueba("456DEF", primero.Id);
                if (actualizar) { vehiculos.Add(vehiculo); vehiculo = vehiculos.Search("456DEF").Single(); }
                vehiculo.Placa = "123ABC";
                error = Assert.Throws<RegistroDuplicadoException>(() =>
                { if (actualizar) vehiculos.Update(vehiculo); else vehiculos.Add(vehiculo); });
                Assert.Equal(actualizar ? 2 : 1, vehiculos.GetAll().Count);
                if (actualizar) Assert.Equal("456DEF", vehiculos.GetById(vehiculo.Id)!.Placa);
                break;
        }
        Assert.Equal(1062, Assert.IsType<MySqlException>(error.InnerException).Number);
    }

    [MySqlAisladoTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Vehiculo_ClienteInexistenteTraduce1452SinGuardarCambios(bool actualizar)
    {
        using var aislada = new MySqlAisladoFixture();
        var clientes = new ClienteRepository(aislada.Factory);
        var cliente = ClientePrueba("1234567"); clientes.Add(cliente);
        var vehiculos = new VehiculoRepository(aislada.Factory);
        var vehiculo = VehiculoPrueba("123ABC", cliente.Id);
        if (actualizar) { vehiculos.Add(vehiculo); vehiculo = vehiculos.GetAll().Single(); }
        vehiculo.ClienteId = int.MaxValue;

        var error = Assert.Throws<ReferenciaInexistenteException>(() =>
        { if (actualizar) vehiculos.Update(vehiculo); else vehiculos.Add(vehiculo); });

        Assert.Equal(1452, Assert.IsType<MySqlException>(error.InnerException).Number);
        if (actualizar) Assert.Equal(cliente.Id, vehiculos.GetById(vehiculo.Id)!.ClienteId);
        else Assert.Empty(vehiculos.GetAll());
    }

    [MySqlAisladoTheory]
    [InlineData("Cliente")]
    [InlineData("Producto")]
    [InlineData("Vehiculo")]
    public void Relaciones_Traduce1451YConservaDatosHistoricos(string catalogo)
    {
        using var aislada = new MySqlAisladoFixture();
        ConsultaOrdenesMySqlTests.CrearOrden(aislada, new DateTime(2026, 11, 4));
        string tabla = catalogo switch { "Cliente" => "Clientes", "Producto" => "Productos", _ => "Vehiculos" };
        int id = Convert.ToInt32(aislada.Escalar($"SELECT MIN(Id) FROM {tabla};"));

        var error = Assert.Throws<RegistroRelacionadoException>(() =>
        {
            switch (catalogo)
            {
                case "Cliente": new ClienteRepository(aislada.Factory).Delete(id); break;
                case "Producto": new ProductoRepository(aislada.Factory).Delete(id); break;
                default: new VehiculoRepository(aislada.Factory).Delete(id); break;
            }
        });

        Assert.Equal(1451, Assert.IsType<MySqlException>(error.InnerException).Number);
        Assert.Equal(1L, Convert.ToInt64(aislada.Escalar($"SELECT COUNT(*) FROM {tabla} WHERE Id=@Id;", ("@Id", id))));
        Assert.Equal(1L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        Assert.Equal(2L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;")));
        Assert.Equal(200L, Convert.ToInt64(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
    }

    [MySqlAisladoTheory]
    [InlineData("Cliente")]
    [InlineData("Producto")]
    [InlineData("Vehiculo")]
    public void OtrosErroresMySql_NoSeTraducenComoDuplicadosORelaciones(string catalogo)
    {
        using var aislada = new MySqlAisladoFixture();
        var error = Assert.Throws<MySqlException>(() =>
        {
            switch (catalogo)
            {
                case "Cliente": new ClienteRepository(aislada.Factory).Add(ClientePrueba("ABC")); break;
                case "Producto":
                    var producto = ProductoPrueba("FIL-001"); producto.Precio = -1;
                    new ProductoRepository(aislada.Factory).Add(producto); break;
                default:
                    var vehiculo = VehiculoPrueba("123ABC", null); vehiculo.Kilometraje = -1;
                    new VehiculoRepository(aislada.Factory).Add(vehiculo); break;
            }
        });
        Assert.Equal(3819, error.Number);
    }

    private static Cliente ClientePrueba(string ci) => new()
    {
        Ci = ci, Nombres = "Ana", PrimerApellido = "Arce", Celular = "71234567",
        CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
    };

    private static Producto ProductoPrueba(string codigo) => new()
    {
        Codigo = codigo, Nombre = "Filtro", Precio = 25, Stock = 10,
        CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
    };

    private static Vehiculo VehiculoPrueba(string placa, int? clienteId) => new()
    {
        Placa = placa, Marca = "Toyota", Modelo = "Corolla", ClienteId = clienteId,
        CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow
    };
}
