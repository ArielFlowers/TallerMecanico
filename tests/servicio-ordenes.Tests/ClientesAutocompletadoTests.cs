using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.IntegrationTests;

public sealed class ClientesAutocompletadoTests
{
    [Trait("Area", "Backend")]
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a")]
    public async Task TerminosCortosYLargos_NoConsultanElPuerto(string? termino)
    {
        var puerto = new ClientesPrueba();
        var servicio = new ClienteService(puerto, new VehiculosPrueba(), new ValidacionClientes(), new UsuarioPrueba());
        Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Empty(await servicio.SugerirAsync(new string('a', 101)));
        Assert.Null(puerto.UltimoTerminoSugerencias);
    }

    [Trait("Area", "Backend")]
    [Fact]
    public async Task Servicio_NormalizaSolamenteLosEspaciosDelTermino()
    {
        var puerto = new ClientesPrueba();
        var servicio = new ClienteService(puerto, new VehiculosPrueba(), new ValidacionClientes(), new UsuarioPrueba());
        await servicio.SugerirAsync("  mARía   péREZ  ");
        Assert.Equal("mARía péREZ", puerto.UltimoTerminoSugerencias);
    }

    [Trait("Area", "Datos")]
    [MySqlAisladoFact]
    public async Task MySql_PrefijoPalabrasCompuestasTildesYComplementos()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Crear(servicio, Formulario("7812345", "1A"));
        Crear(servicio, Formulario("7812345", "2B"));
        var otro = Formulario("1278345");
        otro.Nombres = "Juan Carlos";
        otro.PrimerApellido = "Muñoz";
        otro.SegundoApellido = "Mercado";
        Crear(servicio, otro);

        Assert.Equal(2, (await servicio.SugerirAsync("78")).Count);
        foreach (string termino in new[] { "MARIA", "fer", "perez", "maria per", "de la cru", "maria cruz" })
            Assert.Equal(2, (await servicio.SugerirAsync(termino)).Count);
        Assert.Empty(await servicio.SugerirAsync("aria"));
        Assert.Empty(await servicio.SugerirAsync("2345"));
        Assert.Single(await servicio.SugerirAsync("carlos munoz"));
        var porComplemento = Assert.Single(await servicio.SugerirAsync("7812345-1a"));
        Assert.Equal("CI: 7812345-1A", porComplemento.TextoSecundario);
        Assert.NotEqual(porComplemento.Id, Assert.Single(await servicio.SugerirAsync("7812345-2B")).Id);
        Assert.Equal(porComplemento.Id, servicio.ObtenerPorId(porComplemento.Id)!.Id);
    }

    [Trait("Area", "Datos")]
    [MySqlAisladoFact]
    public async Task MySql_LimitaDiezEscapaEntradasYNoEscribe()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        for (int indice = 0; indice < 13; indice++) Crear(servicio, Formulario($"78{indice:00000}"));
        string antes = JsonSerializer.Serialize(servicio.Obtener());
        var repositorio = new ClienteRepository(aislada.Factory);
        var resultados = await repositorio.SugerirAsync("78");
        Assert.Equal(10, resultados.Count);
        Assert.Equal(10, resultados.Select(sugerencia => sugerencia.Id).Distinct().Count());
        Assert.Equal(resultados.ToArray(), (await repositorio.SugerirAsync("78")).ToArray());
        foreach (string termino in new[] { "%%", "__", "!!", "78%", "78_", "' OR 1=1 --", "<script>" })
            Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Equal(antes, JsonSerializer.Serialize(servicio.Obtener()));
    }

    [Trait("Area", "Datos")]
    [MySqlAisladoFact]
    public async Task MySql_BusquedaManualYApellidoVacioMantienenComportamientoAnterior()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Crear(servicio, Formulario("7812345", "1A"));
        Crear(servicio, Formulario("1278345", "2B"));
        Assert.Equal(2, servicio.Buscar("78").Count);
        Assert.Equal(2, servicio.Buscar("71234567").Count);
        Assert.Equal(2, servicio.Buscar("Fernanda Pérez").Count);
        Assert.Single(servicio.Buscar("2B"));
        var repositorio = new ClienteRepository(aislada.Factory);
        repositorio.Add(new TallerMecanico.Dominio.Entidades.Cliente
        {
            Ci = "5566778", Nombres = "Juan Carlos", PrimerApellido = "Muñoz", SegundoApellido = "",
            Celular = "71234567", CreadoPor = "pruebas", FechaCreacion = DateTime.Now
        });
        Assert.Equal("Juan Carlos Muñoz", Assert.Single(await servicio.SugerirAsync("juan munoz")).TextoPrincipal);
        Assert.Equal(3, servicio.Obtener().Count);
        Assert.Null(servicio.ObtenerPorId(int.MaxValue));
    }

    internal static ClienteService CrearServicio(MySqlAisladoFixture aislada) => new(
        (TallerMecanico.Aplicacion.Puertos.IClientePort)new CreadorCliente(aislada.Factory).CrearRepositorio(),
        new VehiculoRepository(aislada.Factory), new ValidacionClientes(), new UsuarioPrueba());

    internal static ClienteFormViewModel Formulario(string ci = "7812345", string complemento = "") => new()
    {
        Ci = ci, ComplementoCi = complemento, Nombres = "María Fernanda", PrimerApellido = "Pérez",
        SegundoApellido = "De La Cruz", Celular = "71234567"
    };

    internal static int Crear(ClienteService servicio, ClienteFormViewModel formulario)
    {
        var resultado = servicio.Crear(formulario);
        Assert.Empty(resultado.Errores);
        return resultado.Formulario.Id;
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
