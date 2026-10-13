using System.Data.Common;

namespace TallerMecanico.Data.Factories;

public abstract class DatabaseConnectionFactory
{
    protected readonly string ConnectionString;

    protected DatabaseConnectionFactory(
        IConfiguration configuration,
        string connectionStringName)
    {
        ConnectionString =
            configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException(
                $"La cadena de conexión '{connectionStringName}' no está configurada.");
    }

    public abstract DbConnection CreateConnection();
}