using System.Data.Common;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

// Lee la duración en cada llamada: un UPDATE en la base se aplica
// al siguiente token sin reiniciar el servicio.
public sealed class ConfiguracionTokensMySqlAdapter : IConfiguracionTokensPort
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public ConfiguracionTokensMySqlAdapter(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public TimeSpan ObtenerDuracionVerificacion() => ObtenerDuracion("VerificacionEmail");

    public TimeSpan ObtenerDuracionInvitacion() => ObtenerDuracion("InvitacionUsuario");

    public TimeSpan ObtenerDuracionSesion() => ObtenerDuracion("SesionUsuario");

    private TimeSpan ObtenerDuracion(string clave)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT DuracionMinutos
            FROM ConfiguracionTokens
            WHERE Clave = @Clave;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "@Clave";
        parameter.Value = clave;
        command.Parameters.Add(parameter);

        object? resultado = command.ExecuteScalar();

        int minutos = resultado is null || resultado is DBNull
            ? 0
            : Convert.ToInt32(resultado);

        if (minutos <= 0)
        {
            throw new InvalidOperationException(
                $"Falta la duración '{clave}' en la tabla ConfiguracionTokens.");
        }

        return TimeSpan.FromMinutes(minutos);
    }
}
