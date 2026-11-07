using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
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

    private sealed class TempDataPrueba : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
