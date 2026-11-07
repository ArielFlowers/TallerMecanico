using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class CantidadesServiciosTests
{
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
}
