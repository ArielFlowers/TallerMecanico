using TallerMecanico.Api;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Sesion;
using PaginaProductos = TallerMecanico.Pages.Productos.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class ProductosAutocompletadoTests
{
    [Trait("Area", "Backend")]
    [Fact]
    public async Task CrearDesdeOrden_ConservaBorradorYPermisosDeRecepcionista()
    {
        var (pagina, puerto, borradores) = CrearPagina("Recepcionista");
        Guid orden = borradores.Guardar(new OrdenFormViewModel { MecanicoId = 12 });
        pagina.OrdenBorradorId = orden;
        pagina.ProductoInput = Formulario();
        var resultado = Assert.IsType<RedirectToPageResult>(await pagina.OnPostCrearAsync());
        Assert.Equal("/Ordenes/Crear", resultado.PageName);
        Assert.Equal(orden, resultado.RouteValues!["BorradorId"]);
        Assert.Equal(7, resultado.RouteValues["ProductoNuevoId"]);
        Assert.Equal(12, borradores.Obtener(orden)!.MecanicoId);

        pagina.OrdenBorradorId = Guid.NewGuid();
        Assert.IsType<ForbidResult>(await pagina.OnPostCrearAsync());
        Assert.IsType<ForbidResult>(await pagina.OnPostActualizarAsync());
        Assert.IsType<ForbidResult>(await pagina.OnPostEliminarAsync());
        Assert.Single(puerto.Llamadas, llamada => llamada.StartsWith("POST "));
    }

    [Trait("Area", "Backend")]
    [Fact]
    public async Task Administrador_CrudConservaFiltrosYContextoSinCambiarElRetornoEspecial()
    {
        var (pagina, puerto, _) = CrearPagina("Administrador");
        pagina.ProductoInput = Formulario();
        pagina.TerminoBusqueda = "Filtro";
        Assert.IsType<RedirectToPageResult>(await pagina.OnPostCrearAsync());
        int id = 7;
        pagina.ProductoId = id;
        pagina.ProductoSeleccionadoId = id;
        pagina.OrdenBorradorId = Guid.NewGuid();
        pagina.ProductoInput.Nombre = "Filtro Actualizado";
        foreach (var resultado in new[] { await pagina.OnPostActualizarAsync(), await pagina.OnPostEliminarAsync() })
        {
            var redireccion = Assert.IsType<RedirectToPageResult>(resultado);
            Assert.Equal(id, redireccion.RouteValues![nameof(pagina.ProductoSeleccionadoId)]);
            Assert.Equal("Filtro", redireccion.RouteValues[nameof(pagina.TerminoBusqueda)]);
            Assert.Equal(pagina.OrdenBorradorId, redireccion.RouteValues[nameof(pagina.OrdenBorradorId)]);
        }
    }

    internal static ProductoFormViewModel Formulario(string codigo = "FIL-001", string nombre = "Filtro De Aceite", int stock = 10) => new()
    {
        Codigo = codigo, Nombre = nombre, Precio = 25, Stock = stock, StockMinimo = 2
    };

    private static (PaginaProductos Pagina, ApiFalsa Api, BorradorOrdenSesion Borradores) CrearPagina(string rol)
    {
        var contexto = new DefaultHttpContext
        {
            Session = new SesionPrueba(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, rol)], "prueba"))
        };
        var usuario = new UsuarioPrueba { Rol = rol };
        var borradores = new BorradorOrdenSesion(new HttpContextAccessor { HttpContext = contexto }, usuario);
        var puerto = new ApiFalsa();
        var formulario = Formulario(); formulario.Id = 7;
        puerto.Json("GET api/productos", new[] { new ProductoDto { Id = 7, Codigo = "FIL-001", Nombre = "Filtro De Aceite" } });
        puerto.Json("GET api/productos/7", new ProductoDto { Id = 7, Codigo = "FIL-001", Nombre = "Filtro De Aceite" });
        puerto.Json("POST api/productos", new ResultadoFormulario<ProductoFormViewModel>(formulario, new Dictionary<string, string>()));
        puerto.Json("PUT api/productos/7", new ResultadoFormulario<ProductoFormViewModel>(formulario, new Dictionary<string, string>()));
        puerto.Json("DELETE api/productos/7", new { });
        return (new PaginaProductos(new ProductosApi(puerto.Cliente()), borradores)
        {
            PageContext = new PageContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TemporalesPrueba())
        }, puerto, borradores);
    }

    private sealed class TemporalesPrueba : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
