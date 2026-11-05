using Microsoft.Extensions.Configuration;
using TallerMecanico.Infraestructura.Persistencia;

namespace TallerMecanico.IntegrationTests;

// Fábrica de conexiones del proyecto bajo prueba. Cada proyecto de pruebas que use
// repositorios copia este archivo cuando su servicio ya tiene MySqlConnectionFactory.
public sealed partial class MySqlAisladoFixture
{
    private DatabaseConnectionFactory? _factory;

    public DatabaseConnectionFactory Factory => _factory ??= new MySqlConnectionFactory(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MySqlConnection"] = CadenaConexion
        }).Build());
}
