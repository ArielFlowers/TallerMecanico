using System.Data.Common;

namespace TallerMecanico.Data.Factories;

public abstract class DatabaseConnectionFactory
{
    protected readonly string ConnectionString;

    protected DatabaseConnectionFactory(IConfiguration configuration)
    {
        ConnectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "La cadena de conexión no está configurada.");
    }

    public abstract DbConnection CreateConnection();
}