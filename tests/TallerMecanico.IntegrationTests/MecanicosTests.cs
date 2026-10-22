using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.IntegrationTests;

public sealed class MecanicosTests
{
    private readonly ValidacionMecanicos _validador = new();

    [Theory]
    [InlineData("51234567", null)]
    [InlineData("61234567", null)]
    [InlineData("71234567", null)]
    [InlineData("", "El celular es obligatorio.")]
    [InlineData("6123456", "El celular debe tener exactamente 8 dígitos.")]
    [InlineData("612345678", "El celular debe tener exactamente 8 dígitos.")]
    [InlineData("6A234567", "El celular debe contener únicamente números.")]
    [InlineData("6123 456", "El celular no debe contener espacios.")]
    [InlineData("41234567", "El celular debe comenzar en 5, 6 o 7.")]
    public void Celular_ConservaReglasYMensajesDelServidor(string celular, string? mensajeEsperado)
    {
        var formulario = FormularioValido();
        formulario.Celular = celular;
        var errores = _validador.Validar(formulario);
        if (mensajeEsperado is null) Assert.Empty(errores);
        else
        {
            var error = Assert.Single(errores);
            Assert.Equal(nameof(MecanicoInputModel.Celular), error.Key);
            Assert.Equal(mensajeEsperado, error.Value);
        }
    }

    internal static MecanicoInputModel FormularioValido(string ci = "1234567") => new()
    {
        Ci = ci,
        Nombres = "Juan Carlos",
        PrimerApellido = "Pérez",
        SegundoApellido = "Mercado",
        Genero = "Masculino",
        Especialidad = "Motores",
        Celular = "71234567"
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SegundoApellido_VacioNoEsObligatorio(string? segundoApellido)
    {
        var formulario = FormularioValido();
        formulario.SegundoApellido = segundoApellido;

        Assert.Empty(_validador.Validar(formulario));
    }

    [Theory]
    [InlineData("Muñoz")]
    [InlineData("Ávila Núñez")]
    [InlineData("De La Cruz")]
    public void SegundoApellido_AceptaLetrasUnicodeYEspacios(string segundoApellido)
    {
        var formulario = FormularioValido();
        formulario.SegundoApellido = segundoApellido;

        Assert.Empty(_validador.Validar(formulario));
    }

    [Theory]
    [InlineData("Pérez2")]
    [InlineData("Pérez-Mercado")]
    [InlineData("Pérez!")]
    public void SegundoApellido_RechazaCaracteresInvalidosConUnSoloError(string segundoApellido)
    {
        var formulario = FormularioValido();
        formulario.SegundoApellido = segundoApellido;

        var error = Assert.Single(_validador.Validar(formulario));
        Assert.Equal(nameof(MecanicoInputModel.SegundoApellido), error.Key);
        Assert.Equal("El segundo apellido solo puede contener letras y espacios.", error.Value);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void SegundoApellido_ConservaLongitudMaxima(int longitud, bool esValido)
    {
        var formulario = FormularioValido();
        formulario.SegundoApellido = new string('a', longitud);

        var errores = _validador.Validar(formulario);
        Assert.Equal(esValido, errores.Count == 0);
        if (!esValido)
            Assert.Equal("El segundo apellido no puede superar los 100 caracteres.",
                errores[nameof(MecanicoInputModel.SegundoApellido)]);
    }

    [Theory]
    [InlineData(nameof(MecanicoInputModel.Ci))]
    [InlineData(nameof(MecanicoInputModel.Nombres))]
    [InlineData(nameof(MecanicoInputModel.PrimerApellido))]
    [InlineData(nameof(MecanicoInputModel.Celular))]
    public void CamposObligatorios_ConservanSuValidacion(string campo)
    {
        var formulario = FormularioValido();
        formulario.SegundoApellido = null;
        typeof(MecanicoInputModel).GetProperty(campo)!.SetValue(formulario, "");

        Assert.Equal(campo, Assert.Single(_validador.Validar(formulario)).Key);
    }

    [Fact]
    public void Razor_NoAgregaRequiredImplicitoAlSegundoApellido()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddMvcCore().AddDataAnnotations();
        using var proveedor = servicios.BuildServiceProvider();
        var metadatos = proveedor.GetRequiredService<IModelMetadataProvider>();

        Assert.False(metadatos.GetMetadataForProperty(typeof(MecanicoInputModel),
            nameof(MecanicoInputModel.SegundoApellido)).IsRequired);
        Assert.True(metadatos.GetMetadataForProperty(typeof(MecanicoInputModel),
            nameof(MecanicoInputModel.PrimerApellido)).IsRequired);
        Assert.True(metadatos.GetMetadataForProperty(typeof(MecanicoInputModel),
            nameof(MecanicoInputModel.Nombres)).IsRequired);
    }
}
