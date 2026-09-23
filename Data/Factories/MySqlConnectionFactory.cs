using System.Data.Common;
using MySqlConnector;

namespace TallerMecanico.Data.Factories;

public sealed class MySqlConnectionFactory : DatabaseConnectionFactory
{
    public MySqlConnectionFactory(IConfiguration configuration)
        : base(configuration)
    {
    }

    public override DbConnection CreateConnection()
    {
        return new MySqlConnection(ConnectionString);
    }
}