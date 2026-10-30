using System.Security.Cryptography;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public sealed class RepositoriosMySqlTests : IDisposable
{
    private readonly MySqlAisladoFixture _aislada = new();
    public void Dispose() => _aislada.Dispose();
    private DatabaseConnectionFactory CrearFactory() => _aislada.Factory;

    [MySqlAisladoFact]
    public void UsuarioRepository_DebeLeerUsuariosExistentes()
    {
        var repositorio = new UsuarioRepository(CrearFactory());

        repositorio.Agregar(new Usuario { Username = "admin", PasswordHash = "HASH_SOLO_PRUEBA", Rol = "Administrador", CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow });
        repositorio.Agregar(new Usuario { Username = "andy", PasswordHash = "HASH_SOLO_PRUEBA", Rol = "Recepcionista", CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow });
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

    [MySqlAisladoFact]
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