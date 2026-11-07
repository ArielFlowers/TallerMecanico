using System.Data.Common;
using MySqlConnector;

namespace TallerMecanico.Infraestructura.Persistencia;

public sealed class MySqlConnectionFactory : DatabaseConnectionFactory
{
    private const string ConnectionStringName = "MySqlConnection";

    public MySqlConnectionFactory(IConfiguration configuration)
        : base(configuration, ConnectionStringName)
    {
    }

    public override DbConnection CreateConnection()
    {
        return new MySqlConnection(ConnectionString);
    }
}