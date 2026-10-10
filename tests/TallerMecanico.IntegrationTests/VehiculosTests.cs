using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Infraestructura.Session;
using TallerMecanico.Models;
using TallerMecanico.Services;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.IntegrationTests;

public class VehiculosTests
{
    private readonly ValidacionVehiculos _validador = new();

    private static VehiculoFormViewModel Formulario(string placa, bool extranjera = false) => new()
    {
        Placa = placa, EsPlacaExtranjera = extranjera, Marca = "  To Yo Ta ",
        Modelo = " coROLLA ", Kilometraje = 100, ClienteId = 1
    };

    [Theory]
    [InlineData("123abc", "123ABC")]
    [InlineData(" 1234 AbC ", "1234ABC")]
    public void PlacaNacional_NormalizaYAceptaTresOCuatroNumeros(string entrada, string esperada)
    {
        var formulario = _validador.Normalizar(Formulario(entrada));
        Assert.Equal(esperada, formulario.Placa);
        Assert.Equal("Toyota", formulario.Marca);
        Assert.Equal("Corolla", formulario.Modelo);
        Assert.Empty(_validador.Validar(formulario));
    }

    [Theory]
    [InlineData("AB123CD", false)]
    [InlineData("12ABC", false)]
    [InlineData("1234ABCD", false)]
    [InlineData("A", true)]
    [InlineData("ABCDEFGHIJKLMNOP", true)]
    [InlineData("AB/123", true)]
    public void Placa_RechazaFormatosInvalidos(string placa, bool extranjera)
    {
        var formulario = _validador.Normalizar(Formulario(placa, extranjera));
        Assert.Contains(nameof(formulario.Placa), _validador.Validar(formulario));
    }

    [Theory]
    [InlineData(" ab-12 cd ", "AB12CD")]
    [InlineData("123AB45", "123AB45")]
    [InlineData("A1", "A1")]
    [InlineData("ABCDEFGHIJKLMNO", "ABCDEFGHIJKLMNO")]
    public void PlacaExtranjera_NormalizaSinExigirOrdenBoliviano(string entrada, string esperada)
    {
        var formulario = _validador.Normalizar(Formulario(entrada, true));
        Assert.Equal(esperada, formulario.Placa);
        Assert.True(formulario.EsPlacaExtranjera);
        Assert.Empty(_validador.Validar(formulario));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cliente_EsObligatorioAlGuardar(int? clienteId)
    {
        var formulario = _validador.Normalizar(Formulario("123ABC"));
        formulario.ClienteId = clienteId;
        Assert.Contains(nameof(formulario.ClienteId), _validador.Validar(formulario));
    }

    [Fact]
    public void Catalogo_MarcasYTodosLosModelosEstanOrdenados()
    {
        Assert.Equal(new[] { "Chevrolet", "Hyundai", "Kia", "Mitsubishi", "Nissan", "Suzuki", "Toyota" },
            CatalogoVehiculos.MarcasOrdenadas);
        foreach (var modelos in CatalogoVehiculos.CatalogoOrdenado.Values)
            Assert.Equal(modelos.OrderBy(nombre => nombre, CatalogoVehiculos.ComparadorAlfabetico), modelos);
    }

    [Fact]
    public void Vehiculo_NoDuplicaUnaPlacaAlCambiarElToggle()
    {
        var clientes = new ClientesPrueba();
        clientes.Add(new Cliente());
        var vehiculos = new VehiculosPrueba();
        var servicio = new VehiculoService(vehiculos, clientes, _validador);
        Assert.Empty(servicio.Create(Formulario("123ABC")).Errores);
        Assert.Contains(nameof(VehiculoFormViewModel.Placa), servicio.Create(Formulario("123-abc", true)).Errores);
        Assert.Single(vehiculos.Datos);
    }

    [Fact]
    public void Vehiculo_NoPermiteClienteInexistenteNiDesasignarUno()
    {
        var clientes = new ClientesPrueba();
        var vehiculos = new VehiculosPrueba();
        var servicio = new VehiculoService(vehiculos, clientes, _validador);
        Assert.Contains(nameof(VehiculoFormViewModel.ClienteId), servicio.Create(Formulario("123ABC")).Errores);
        clientes.Add(new Cliente());
        Assert.Empty(servicio.Create(Formulario("123ABC")).Errores);
        var editar = Formulario("123ABC");
        editar.Id = 1;
        editar.ClienteId = null;
        Assert.Contains(nameof(editar.ClienteId), servicio.Update(editar).Errores);
        Assert.Equal(1, vehiculos.Datos[0].ClienteId);
    }

    [Fact]
    public void Borrador_ConservaCamposBusquedaYClienteNuevo()
    {
        var sesion = new SesionPrueba();
        var usuario = new UsuarioPrueba();
        var contexto = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = sesion } };
        var borradores = new BorradorVehiculoSesion(contexto, usuario);
        var formulario = Formulario("AB-123", true);
        formulario.Id = 12;
        formulario.Observaciones = "Revisar motor";
        Guid id = borradores.Guardar(formulario, "editar", "AB");
        Assert.True(borradores.AsignarCliente(id, 42));
        var recuperado = Assert.IsType<BorradorVehiculo>(borradores.Obtener(id));
        Assert.Equal(12, recuperado.Formulario.Id);
        Assert.Equal("AB-123", recuperado.Formulario.Placa);
        Assert.True(recuperado.Formulario.EsPlacaExtranjera);
        Assert.Equal("Revisar motor", recuperado.Formulario.Observaciones);
        Assert.Equal(42, recuperado.Formulario.ClienteId);
        Assert.Equal("editar", recuperado.Modal);
        Assert.Equal("AB", recuperado.Buscar);
        borradores.Eliminar(id);
        Assert.Null(borradores.Obtener(id));
    }

    [Fact]
    public void Borrador_NoPermiteOtroUsuarioNiUnBorradorVencido()
    {
        var sesion = new SesionPrueba();
        var usuario = new UsuarioPrueba();
        var contexto = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = sesion } };
        var borradores = new BorradorVehiculoSesion(contexto, usuario);
        Guid id = borradores.Guardar(Formulario("123ABC"), "crear", null);
        usuario.Username = "otro";
        Assert.Null(borradores.Obtener(id));
        usuario.Username = "recepcion_prueba";
        id = borradores.Guardar(Formulario("123ABC"), "crear", null);
        var vencido = borradores.Obtener(id)!;
        vencido.VenceUtc = DateTime.UtcNow.AddSeconds(-1);
        sesion.SetString($"Vehiculos:Borrador:{id:N}", JsonSerializer.Serialize(vencido));
        Assert.Null(borradores.Obtener(id));
        Assert.False(borradores.AsignarCliente(id, 1));
    }

    [Fact]
    public void RegistrarCliente_RetornaAlBorradorConIdGenerado()
    {
        var sesion = new SesionPrueba();
        var usuario = new UsuarioPrueba();
        var contexto = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = sesion } };
        var borradores = new BorradorVehiculoSesion(contexto, usuario);
        var clientes = new ClientesPrueba();
        var vehiculos = new VehiculosPrueba();
        var servicio = new ClienteService(clientes, vehiculos, new ValidacionClientes(), usuario);
        Guid id = borradores.Guardar(Formulario("123ABC"), "crear", null);
        var pagina = new TallerMecanico.Pages.Clientes.IndexModel(servicio, borradores)
        {
            BorradorVehiculoId = id,
            ClienteInput = new ClienteFormViewModel
            {
                Ci = "1234567", Nombres = "Ana", PrimerApellido = "Arce", SegundoApellido = "Perez", Celular = "71234567"
            },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(contexto.HttpContext!, new TempDataPrueba())
        };
        var resultado = Assert.IsType<RedirectToPageResult>(pagina.OnPostCrear());
        Assert.Equal("/Vehiculos/Index", resultado.PageName);
        Assert.Equal("Retomar", resultado.PageHandler);
        Assert.Equal(clientes.Datos.Single().Id, borradores.Obtener(id)!.Formulario.ClienteId);
    }

    private sealed class TempDataPrueba : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
