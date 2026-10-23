using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.IntegrationTests;

public sealed class CantidadesServiciosTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CantidadesMalformadas_DevuelvenErrorSinAbrirTransaccion(bool entradaNula)
    {
        IReadOnlyList<LineaServicioOrdenSolicitud> cantidades = entradaNula
            ? [null!]
            : [new(1, 1), new(1, 3)];
        var solicitud = new CrearOrdenSolicitud(1, 1, DateTime.Today, Guid.NewGuid(), [],
            ServicioIds: [1], CantidadesServicios: cantidades);
        var servicio = new OrdenServicioService(new UnidadNoUtilizable(), new UsuarioPrueba(),
            new ValidacionOrdenes(), TimeProvider.System, NullLogger<OrdenServicioService>.Instance);

        var resultado = servicio.CrearOrden(solicitud);

        Assert.False(resultado.Exito);
        Assert.Contains(nameof(CrearOrdenSolicitud.CantidadesServicios), resultado.Errores);
    }

    [Fact]
    public void ServiciosDuplicados_SeRechazanAunqueSusCantidadesSeanValidas()
    {
        var solicitud = new CrearOrdenSolicitud(1, 1, DateTime.Today, Guid.NewGuid(), [],
            ServicioIds: [1, 1], CantidadesServicios: [new(1, 1)]);

        Assert.Contains(nameof(CrearOrdenSolicitud.ServicioIds), new ValidacionOrdenes().Validar(solicitud));
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1,5")]
    public async Task Formulario_CantidadesFraccionariasProducenErrorDeModelBinding(string cantidad)
    {
        var (_, errores) = await EnlazarCantidadAsync(cantidad);

        Assert.False(errores.IsValid);
        Assert.NotEmpty(errores["Formulario.CantidadesServicios[1]"]!.Errors);
    }

    internal static async Task<(OrdenFormViewModel Formulario, ModelStateDictionary Errores)>
        EnlazarCantidadAsync(string cantidad)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddMvcCore();
        using var proveedor = servicios.BuildServiceProvider();
        var metadatos = proveedor.GetRequiredService<IModelMetadataProvider>()
            .GetMetadataForType(typeof(OrdenFormViewModel));
        var enlace = new BindingInfo { BindingSource = BindingSource.Form };
        var vinculador = proveedor.GetRequiredService<IModelBinderFactory>().CreateBinder(new()
        {
            Metadata = metadatos, BindingInfo = enlace, CacheToken = typeof(OrdenFormViewModel)
        });
        var contexto = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var campos = new FormCollection(new Dictionary<string, StringValues>
        {
            ["Formulario.VehiculoId"] = "1",
            ["Formulario.MecanicoId"] = "1",
            ["Formulario.ServicioIds"] = "1",
            ["Formulario.CantidadesServicios[1]"] = cantidad
        });
        var valores = new FormValueProvider(BindingSource.Form, campos, CultureInfo.InvariantCulture);
        var contextoEnlace = DefaultModelBindingContext.CreateBindingContext(
            contexto, valores, metadatos, enlace, "Formulario");

        await vinculador.BindModelAsync(contextoEnlace);

        return (Assert.IsType<OrdenFormViewModel>(contextoEnlace.Result.Model), contexto.ModelState);
    }

    private sealed class UnidadNoUtilizable : IUnidadTrabajoOrdenPort
    {
        public ITransaccionOrden Iniciar() => throw new InvalidOperationException("No debe abrirse una transacción.");
    }
}
