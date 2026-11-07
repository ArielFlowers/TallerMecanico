using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using TallerMecanico.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using Microsoft.Extensions.DependencyInjection;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class MecanicoServiceTests
{
    [Fact]
    public async Task AltaDesdeOrden_AbreElFormularioYVuelveConElBorrador()
    {
        var api = new ApiFalsa();
        api.Json("GET api/mecanicos", Array.Empty<MecanicoDto>());
        api.Json("POST api/mecanicos", new Dictionary<string, string>());
        var borradores = new BorradoresPrueba();
        Guid orden = borradores.Guardar(new OrdenFormViewModel());
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Administrador")], "Prueba"))
        };
        var pagina = new TallerMecanico.Pages.Mecanicos.IndexModel(new MecanicosApi(api.Cliente()), borradores)
        {
            OrdenBorradorId = orden,
            MecanicoInput = FormularioValido(),
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TemporalesPrueba())
        };

        await pagina.OnGetAsync();
        Assert.Equal("crear", pagina.FormularioActivo);

        var redireccion = Assert.IsType<RedirectToPageResult>(await pagina.OnPostCrearAsync());
        Assert.Equal("/Ordenes/Crear", redireccion.PageName);
        Assert.Equal(orden, redireccion.RouteValues!["BorradorId"]);
        Assert.Single(api.Llamadas, llamada => llamada == "POST api/mecanicos");
        Assert.NotNull(borradores.Obtener(orden));
    }

    private sealed class BorradoresPrueba : IBorradorOrden
    {
        private readonly Dictionary<Guid, OrdenFormViewModel> _borradores = [];

        public Guid Guardar(OrdenFormViewModel formulario)
        {
            Guid id = Guid.NewGuid();
            _borradores[id] = formulario;
            return id;
        }

        public OrdenFormViewModel? Obtener(Guid id) => _borradores.GetValueOrDefault(id);

        public void Eliminar(Guid id) => _borradores.Remove(id);
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static MecanicoInputModel FormularioValido() => new()
    {
        Ci = "1234567", Nombres = "Juan Carlos", PrimerApellido = "Pérez", SegundoApellido = "Mercado",
        Genero = "Masculino", Especialidad = "Motores", Celular = "71234567"
    };
}
