using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.IntegrationTests;

public sealed class VehiculosAutocompletadoTests
{
    [Trait("Area", "Backend")]
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a")]
    public async Task TerminosCortosYLargos_NoConsultanElPuerto(string? termino)
    {
        var puerto = new VehiculosPrueba();
        var servicio = new VehiculoService(puerto, new ClientesPrueba(), new ValidacionVehiculos());
        Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Empty(await servicio.SugerirAsync(new string('a', 101)));
        Assert.Null(puerto.UltimoTerminoSugerencias);
    }

    [Trait("Area", "Backend")]
    [Fact]
    public async Task Servicio_NormalizaEspaciosSinCambiarLaConsultaManual()
    {
        var puerto = new VehiculosPrueba();
        var servicio = new VehiculoService(puerto, new ClientesPrueba(), new ValidacionVehiculos());
        await servicio.SugerirAsync("   tOYota   cor   ");
        Assert.Equal("tOYota cor", puerto.UltimoTerminoSugerencias);
    }

    [Trait("Area", "Datos")]
    [MySqlAisladoFact]
    public async Task MySql_PrefijoPlacasNacionalesYExtranjerasMarcaModeloYTildes()
    {
        using var aislada = new MySqlAisladoFixture();
        var repositorio = new VehiculoRepository(aislada.Factory);
        repositorio.Add(new Vehiculo { Placa = "123ABC", Marca = "Toyota", Modelo = "Corolla" });
        repositorio.Add(new Vehiculo { Placa = "1234ABC", Marca = "Toyota", Modelo = "Land Cruiser" });
        repositorio.Add(new Vehiculo { Placa = "AB123CD", EsPlacaExtranjera = true, Marca = "Citroën", Modelo = "Eléctrico Familiar" });
        var servicio = CrearServicio(aislada);
        Assert.Equal(2, (await servicio.SugerirAsync("123")).Count);
        Assert.Equal(2, (await servicio.SugerirAsync("TOY")).Count);
        Assert.Single(await servicio.SugerirAsync("cor"));
        Assert.Single(await servicio.SugerirAsync("toy cru"));
        Assert.Single(await servicio.SugerirAsync("land cru"));
        Assert.Empty(await servicio.SugerirAsync("oyota"));
        Assert.Empty(await servicio.SugerirAsync("234"));
        var extranjera = Assert.Single(await servicio.SugerirAsync("ab12"));
        Assert.Equal("AB123CD", extranjera.TextoPrincipal);
        Assert.Equal("Citroën · Eléctrico Familiar", extranjera.TextoSecundario);
        Assert.Equal(extranjera.Id, Assert.Single(await servicio.SugerirAsync("citroen elec")).Id);
        Assert.Equal(3, servicio.Search("123").Count);
        Assert.Equal(2, servicio.Search("oyota").Count);
        Assert.Single(servicio.Search("Cruiser"));
    }

    [Trait("Area", "Datos")]
    [MySqlAisladoFact]
    public async Task MySql_LimiteDiezEscapeYConsultasSinEscribirRelaciones()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        int cliente = CrearCliente(aislada);
        for (int indice = 0; indice < 13; indice++) Crear(servicio, $"{800 + indice}ABC", cliente);
        string antes = JsonSerializer.Serialize(servicio.GetAll());
        string clienteAntes = JsonSerializer.Serialize(new ClienteRepository(aislada.Factory).GetById(cliente));
        var repositorio = new VehiculoRepository(aislada.Factory);
        var sugerencias = await repositorio.SugerirAsync("Toyota");
        Assert.Equal(10, sugerencias.Count);
        Assert.Equal(sugerencias.ToArray(), (await repositorio.SugerirAsync("Toyota")).ToArray());
        foreach (string termino in new[] { "%%", "__", "80%", "80_", "' OR 1=1 --", "<script>" })
            Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Equal(antes, JsonSerializer.Serialize(servicio.GetAll()));
        Assert.Equal(clienteAntes, JsonSerializer.Serialize(new ClienteRepository(aislada.Factory).GetById(cliente)));
        Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
    }

    internal static VehiculoService CrearServicio(MySqlAisladoFixture aislada) => new(
        (IVehiculoPort)new CreadorVehiculo(aislada.Factory).CrearRepositorio(),
        new ClienteRepository(aislada.Factory), new ValidacionVehiculos(), new UsuarioPrueba());

    internal static VehiculoFormViewModel Formulario(int cliente, string placa = "123ABC") => new()
    { Placa = placa, Marca = "Toyota", Modelo = "Corolla", Kilometraje = 1200, ClienteId = cliente };

    internal static int CrearCliente(MySqlAisladoFixture aislada)
    {
        var cliente = new Cliente
        {
            Ci = "1234567", Nombres = "Ana", PrimerApellido = "Arce", SegundoApellido = "Pérez", Celular = "71234567",
            CreadoPor = "pruebas", FechaCreacion = DateTime.Now
        };
        new ClienteRepository(aislada.Factory).Add(cliente);
        return cliente.Id;
    }

    internal static int Crear(VehiculoService servicio, string placa, int cliente, bool extranjera = false)
    {
        var formulario = Formulario(cliente, placa);
        formulario.EsPlacaExtranjera = extranjera;
        Assert.Empty(servicio.Create(formulario).Errores);
        return servicio.Search(placa).Single(vehiculo => vehiculo.Placa == placa).Id;
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
