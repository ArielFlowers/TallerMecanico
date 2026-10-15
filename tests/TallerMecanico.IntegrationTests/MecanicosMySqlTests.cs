using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;

namespace TallerMecanico.IntegrationTests;

public sealed class MecanicosMySqlTests
{
    private static MecanicoService CrearServicio(MySqlAisladoFixture aislada) =>
        new(new CreadorMecanico(aislada.Factory), new ValidacionMecanicos());

    [MySqlAisladoFact]
    public void Crear_SinSegundoApellidoPersisteVacioYConservaLaEntrada()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        string?[] entradas = [null, "", "   "];

        for (int indice = 0; indice < entradas.Length; indice++)
        {
            var formulario = MecanicosTests.FormularioValido($"12345{indice}");
            formulario.SegundoApellido = entradas[indice];

            Assert.Empty(servicio.Crear(formulario));
            Assert.Equal(string.Empty, Assert.Single(servicio.Obtener(formulario.Ci)).SegundoApellido);
            Assert.Equal(entradas[indice], formulario.SegundoApellido);
        }

        Assert.Equal(3, servicio.Obtener().Count);
        Assert.Equal(0L, Convert.ToInt64(aislada.Escalar(
            "SELECT COUNT(*) FROM Mecanicos WHERE SegundoApellido IS NULL;")));
    }

    [MySqlAisladoFact]
    public void Actualizar_PermiteQuitarSegundoApellidoSinCambiarAuditoriaDeCreacion()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        var formulario = MecanicosTests.FormularioValido();
        Assert.Empty(servicio.Crear(formulario));
        var original = Assert.Single(servicio.Obtener());
        formulario.SegundoApellido = null;

        var resultado = servicio.Actualizar(original.Id, formulario);

        Assert.True(resultado.Actualizado);
        Assert.Empty(resultado.Errores);
        var actualizado = Assert.Single(servicio.Obtener());
        Assert.Equal(string.Empty, actualizado.SegundoApellido);
        Assert.Equal(original.PrimerApellido, actualizado.PrimerApellido);
        Assert.Equal(original.CreadoPor, actualizado.CreadoPor);
        Assert.Equal(original.FechaCreacion, actualizado.FechaCreacion);
    }

    [MySqlAisladoFact]
    public void Crear_ConSegundoApellidoNormalizaEspaciosCapitalizacionYLongitud()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        var formulario = MecanicosTests.FormularioValido();
        formulario.SegundoApellido = "   mUÑOZ    de LA   crUZ   ";
        Assert.Empty(servicio.Crear(formulario));
        Assert.Equal("Muñoz De La Cruz", Assert.Single(servicio.Obtener()).SegundoApellido);
        Assert.Equal("   mUÑOZ    de LA   crUZ   ", formulario.SegundoApellido);

        formulario = MecanicosTests.FormularioValido("7654321");
        formulario.SegundoApellido = "   " + new string('a', 100) + "   ";
        Assert.Empty(servicio.Crear(formulario));
        Assert.Equal(100, Assert.Single(servicio.Obtener(formulario.Ci)).SegundoApellido.Length);
    }

    [MySqlAisladoFact]
    public void CrearYActualizar_RechazanSegundoApellidoInvalidoSinCambiarRegistros()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        var formulario = MecanicosTests.FormularioValido();
        Assert.Empty(servicio.Crear(formulario));
        int id = Assert.Single(servicio.Obtener()).Id;

        foreach (string invalido in new[] { "Pérez2", "Pérez-", new string('a', 101) })
        {
            var editar = MecanicosTests.FormularioValido();
            editar.SegundoApellido = invalido;
            var resultado = servicio.Actualizar(id, editar);
            Assert.False(resultado.Actualizado);
            Assert.Equal(nameof(MecanicoInputModel.SegundoApellido), Assert.Single(resultado.Errores).Key);

            var crear = MecanicosTests.FormularioValido("7654321");
            crear.SegundoApellido = invalido;
            Assert.Equal(nameof(MecanicoInputModel.SegundoApellido), Assert.Single(servicio.Crear(crear)).Key);
            Assert.Equal("Mercado", Assert.Single(servicio.Obtener()).SegundoApellido);
        }
    }

    [MySqlAisladoFact]
    public void Persistencia_ConservaNotNullYDatosExistentesSinNecesitarMigracion()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Assert.Empty(servicio.Crear(MecanicosTests.FormularioValido()));

        new DatabaseInitializer(aislada.Factory).Initialize();
        new DatabaseInitializer(aislada.Factory).Initialize();

        Assert.Equal("NO", aislada.Escalar("""
            SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Mecanicos'
              AND COLUMN_NAME = 'SegundoApellido';
            """));
        Assert.Equal("Mercado", Assert.Single(servicio.Obtener()).SegundoApellido);
        var nuevo = MecanicosTests.FormularioValido("7654321");
        nuevo.SegundoApellido = "";
        Assert.Empty(servicio.Crear(nuevo));
    }

    [MySqlAisladoFact]
    public void Repositorio_NormalizaNullAVacioEnInsertYUpdate()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Assert.Empty(servicio.Crear(MecanicosTests.FormularioValido()));
        var repositorio = new MecanicoRepository(aislada.Factory);
        var mecanico = Assert.Single(repositorio.GetAll());
        mecanico.SegundoApellido = null!;
        repositorio.Update(mecanico);
        Assert.Equal("", repositorio.GetById(mecanico.Id)!.SegundoApellido);

        mecanico.Ci = "7654321";
        repositorio.Add(mecanico);
        Assert.Equal("", Assert.Single(repositorio.Search(mecanico.Ci)).SegundoApellido);
        Assert.Equal(2, repositorio.Count());
    }

    [MySqlAisladoFact]
    public void ListadoYBusqueda_ToleranVacioYNullEnEsquemaPreexistente()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        var formulario = MecanicosTests.FormularioValido("2345678");
        formulario.SegundoApellido = null;
        Assert.Empty(servicio.Crear(formulario));
        Assert.Empty(servicio.Crear(MecanicosTests.FormularioValido("7654321")));

        aislada.Ejecutar("ALTER TABLE Mecanicos MODIFY COLUMN SegundoApellido VARCHAR(100) NULL;");
        aislada.Ejecutar("UPDATE Mecanicos SET SegundoApellido = NULL WHERE Ci = @Ci;", ("@Ci", formulario.Ci));

        Assert.Equal(2, servicio.Obtener().Count);
        Assert.Equal("", Assert.Single(servicio.Obtener(formulario.Ci)).SegundoApellido);
        Assert.Equal(2, servicio.Obtener("Pérez").Count);
        Assert.Equal(2, servicio.Obtener("Juan").Count);
        Assert.Single(servicio.Obtener("Mercado"));
        Assert.Empty(servicio.Obtener("Inexistente"));
        int id = Assert.Single(servicio.Obtener(formulario.Ci)).Id;
        Assert.Equal("", new MecanicoRepository(aislada.Factory).GetById(id)!.SegundoApellido);
    }
}
