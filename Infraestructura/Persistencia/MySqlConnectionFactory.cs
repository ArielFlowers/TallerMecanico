using System.Data.Common;
using MySqlConnector;

namespace TallerMecanico.Data.Factories;

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