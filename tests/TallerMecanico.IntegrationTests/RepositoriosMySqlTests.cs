using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public class RepositoriosMySqlTests
{
    private static DatabaseConnectionFactory CrearFactory()
    {
        IConfiguration configuracion = new ConfigurationBuilder()
            .AddUserSecrets(
                typeof(UsuarioRepository).Assembly,
                optional: false)
            .Build();

        return new MySqlConnectionFactory(configuracion);
    }

    [Fact]
    public void UsuarioRepository_DebeLeerUsuariosExistentes()
    {
        var repositorio = new UsuarioRepository(CrearFactory());

        var admin = repositorio.ObtenerPorUsername("admin");
        var andy = repositorio.ObtenerPorUsername("andy");

        Assert.NotNull(admin);
        Assert.NotNull(andy);

        Assert.Equal("Administrador", admin.Rol);
        Assert.Equal("Recepcionista", andy.Rol);

        Assert.True(repositorio.ExisteUsername("admin"));
        Assert.True(repositorio.ExisteUsername("andy"));

        Assert.NotNull(repositorio.ObtenerPorId(admin.Id));
    }

    [Fact]
    public void SolicitudRegistroRepository_DebeConsultarSinModificarDatos()
    {
        var repositorio =
            new SolicitudRegistroRepository(CrearFactory());

        string identificador = Guid.NewGuid().ToString("N");

        Assert.False(
            repositorio.ExisteUsername(identificador));

        Assert.False(
            repositorio.ExisteEmail(
                identificador + "@example.com"));

        Assert.Null(
            repositorio.ObtenerPorId(long.MaxValue));

        string tokenHash = Convert.ToHexString(
            SHA256.HashData(
                Guid.NewGuid().ToByteArray()));

        Assert.Null(
            repositorio.ObtenerPorTokenHash(tokenHash));
    }
}