using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using Xunit;

namespace TallerMecanico.IntegrationTests;

// RF-07: duraciones en ConfiguracionTokens y sesión en Usuarios.SesionVenceEn.
[Trait("Area", "Datos")]
public sealed class ConfiguracionTokensMySqlTests : IDisposable
{
    private readonly MySqlAisladoFixture _aislada = new();
    public void Dispose() => _aislada.Dispose();

    [MySqlAisladoFact]
    public void Configuracion_LeeValoresInicialesYCambiosSinReiniciar()
    {
        var configuracion = new ConfiguracionTokensMySqlAdapter(_aislada.Factory);

        Assert.Equal(TimeSpan.FromMinutes(60), configuracion.ObtenerDuracionVerificacion());
        Assert.Equal(TimeSpan.FromMinutes(60), configuracion.ObtenerDuracionInvitacion());
        Assert.Equal(TimeSpan.FromMinutes(30), configuracion.ObtenerDuracionSesion());

        _aislada.Ejecutar("UPDATE ConfiguracionTokens SET DuracionMinutos = 15 WHERE Clave = 'SesionUsuario';");
        Assert.Equal(TimeSpan.FromMinutes(15), configuracion.ObtenerDuracionSesion());

        _aislada.Ejecutar("DELETE FROM ConfiguracionTokens WHERE Clave = 'InvitacionUsuario';");
        Assert.Throws<InvalidOperationException>(() => configuracion.ObtenerDuracionInvitacion());
    }

    [MySqlAisladoFact]
    public void Sesion_SeIniciaRenuevaVenceYCierraEnLaBase()
    {
        var usuarios = new UsuarioRepository(_aislada.Factory);
        usuarios.Agregar(new Usuario { Username = "sesion_prueba", PasswordHash = "HASH_SOLO_PRUEBA", Rol = "Recepcionista", CreadoPor = "pruebas", FechaCreacion = DateTime.UtcNow });
        int id = usuarios.ObtenerPorUsername("sesion_prueba")!.Id;
        var sesiones = new SesionUsuarioMySqlAdapter(_aislada.Factory);

        Assert.Null(MinutosRestantes(usuarios));
        Assert.False(sesiones.Renovar(id, DateTime.UtcNow.AddMinutes(30)));

        sesiones.Iniciar(id, DateTime.UtcNow.AddMinutes(10));
        Assert.Equal(10, MinutosRestantes(usuarios));

        Assert.True(sesiones.Renovar(id, DateTime.UtcNow.AddMinutes(30)));
        Assert.Equal(30, MinutosRestantes(usuarios));

        // Una sesión vencida no se renueva y la tabla la muestra sin sesión.
        sesiones.Iniciar(id, DateTime.UtcNow.AddMinutes(-1));
        Assert.False(sesiones.Renovar(id, DateTime.UtcNow.AddMinutes(30)));
        Assert.Null(MinutosRestantes(usuarios));

        sesiones.Iniciar(id, DateTime.UtcNow.AddMinutes(10));
        sesiones.Cerrar(id);
        Assert.Null(MinutosRestantes(usuarios));
    }

    private static int? MinutosRestantes(UsuarioRepository usuarios)
        => usuarios.ListarFiltrados("sesion_prueba", null, null, 1, 10).Single().MinutosSesionRestantes;
}
