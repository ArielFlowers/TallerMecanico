using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Models;
using TallerMecanico.Data;

namespace TallerMecanico.IntegrationTests;

[Collection("MySQL aislado")]
public sealed class VehiculosMySqlTests(MySqlAisladoFixture basePrueba)
{
    [MySqlAisladoFact]
    public void Vehiculo_PersistenciaToggleClienteYOrdenAlfabetico()
    {
        var clientes = new ClienteRepository(basePrueba.Factory);
        var cliente = new Cliente { Ci = "1234567", Nombres = "Ana", PrimerApellido = "Arce", SegundoApellido = "Perez", Celular = "71234567", CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow };
        clientes.Add(cliente);
        Assert.True(cliente.Id > 0);
        var repositorio = new VehiculoRepository(basePrueba.Factory);
        repositorio.Add(new Vehiculo { Placa = "ZY123AB", EsPlacaExtranjera = true, Marca = "Toyota", Modelo = "Corolla", ClienteId = cliente.Id });
        repositorio.Add(new Vehiculo { Placa = "123ABC", Marca = "Toyota", Modelo = "Yaris", ClienteId = cliente.Id });
        var guardado = repositorio.Search("ZY123AB").Single();
        Assert.True(guardado.EsPlacaExtranjera);
        Assert.Equal(cliente.Id, guardado.ClienteId);
        guardado.Placa = "1234ABC";
        guardado.EsPlacaExtranjera = false;
        repositorio.Update(guardado);
        Assert.False(repositorio.GetById(guardado.Id)!.EsPlacaExtranjera);
        var placas = repositorio.GetAll().Select(vehiculo => vehiculo.Placa).ToArray();
        Assert.Equal(placas.OrderBy(placa => placa, StringComparer.Ordinal), placas);
        new DatabaseInitializer(basePrueba.Factory).Initialize();
        Assert.Equal(cliente.Id, repositorio.GetById(guardado.Id)!.ClienteId);
    }

    [MySqlAisladoFact]
    public void MigracionVehiculos_ConservaRegistrosAntiguosSinCliente()
    {
        using var antigua = new MySqlAisladoFixture();
        antigua.Ejecutar("ALTER TABLE Vehiculos DROP COLUMN EsPlacaExtranjera, MODIFY COLUMN Placa VARCHAR(10) NOT NULL;");
        antigua.Ejecutar("INSERT INTO Vehiculos (Placa, Marca, Modelo, Kilometraje) VALUES ('987ABC', 'Toyota', 'Hilux', 10);");
        new DatabaseInitializer(antigua.Factory).Initialize();
        var anterior = new VehiculoRepository(antigua.Factory).Search("987ABC").Single();
        Assert.Null(anterior.ClienteId);
        Assert.False(anterior.EsPlacaExtranjera);
        string migracion = File.ReadAllText(Path.Combine(MySqlAisladoFixture.RaizProyecto, "docs/migraciones/003_vehiculos_placas_extranjeras.sql"));
        antigua.Ejecutar(migracion);
        antigua.Ejecutar(migracion);
        Assert.Equal(1, Convert.ToInt32(antigua.Escalar("SELECT COUNT(*) FROM Vehiculos;")));
    }
}
