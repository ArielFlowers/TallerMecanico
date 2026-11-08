using TallerMecanico.Api;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Sesion;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public class VehiculosTests
{
    private static VehiculoFormViewModel Formulario(string placa, bool extranjera = false) => new()
    {
        Placa = placa, EsPlacaExtranjera = extranjera, Marca = "  To Yo Ta ",
        Modelo = " coROLLA ", Kilometraje = 100, ClienteId = 1
    };

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
    public async Task RegistrarCliente_RetornaAlBorradorConIdGenerado()
    {
        var sesion = new SesionPrueba();
        var usuario = new UsuarioPrueba();
        var contexto = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = sesion } };
        var borradores = new BorradorVehiculoSesion(contexto, usuario);
        var api = new ApiFalsa();
        api.Json("POST api/clientes", new ResultadoFormulario<ClienteFormViewModel>(
            new ClienteFormViewModel { Id = 1 }, new Dictionary<string, string>()));
        Guid id = borradores.Guardar(Formulario("123ABC"), "crear", null);
        var pagina = new TallerMecanico.Pages.Clientes.IndexModel(new ClientesApi(api.Cliente()), borradores, new BorradorOrdenSesion(contexto, usuario))
        {
            BorradorVehiculoId = id,
            ClienteInput = new ClienteFormViewModel
            {
                Ci = "1234567", Nombres = "Ana", PrimerApellido = "Arce", SegundoApellido = "Perez", Celular = "71234567"
            },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(contexto.HttpContext!, new TempDataPrueba())
        };
        var resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostCrearAsync());
        Assert.Equal("/Vehiculos/Index", resultado.PageName);
        Assert.Equal("Retomar", resultado.PageHandler);
        Assert.Equal(1, borradores.Obtener(id)!.Formulario.ClienteId);
    }

    private sealed class TempDataPrueba : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
