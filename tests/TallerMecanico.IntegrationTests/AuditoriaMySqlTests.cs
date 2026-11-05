using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Datos")]
public sealed class AuditoriaMySqlTests
{
    [MySqlAisladoFact]
    public void ObtenerRecientes_ConservaDetalleActorYFechaYOrdenaPorFechaEId()
    {
        using var aislada = new MySqlAisladoFixture();
        var repositorio = new AuditoriaRepository(aislada.Factory);
        var fecha = new DateTime(2026, 5, 20, 12, 30, 45, DateTimeKind.Utc).AddTicks(1234560);
        repositorio.Registrar(new RegistroAuditoria
        {
            UsuarioId = 27, Username = "administrador_prueba", Accion = "USUARIO_ACTUALIZADO",
            Entidad = "Usuario", EntidadId = "42", Fecha = fecha,
            Detalle = "Cambio de rol: Recepcionista → Administrador."
        });
        repositorio.Registrar(new RegistroAuditoria
        {
            Accion = "SESION_EXPIRADA", Entidad = "Sesion", Fecha = fecha
        });
        repositorio.Registrar(new RegistroAuditoria
        {
            Username = "sistema", Accion = "REGISTRO_ANTERIOR", Entidad = "Usuario",
            Fecha = fecha.AddDays(-1), Detalle = "Este registro no debe entrar en el límite."
        });

        IReadOnlyList<RegistroAuditoria> recientes = repositorio.ObtenerRecientes(2);

        Assert.Equal(2, recientes.Count);
        RegistroAuditoria sinDetalle = recientes[0], conDetalle = recientes[1];
        Assert.True(sinDetalle.Id > conDetalle.Id);
        Assert.Equal("SESION_EXPIRADA", sinDetalle.Accion);
        Assert.Equal("Sesion", sinDetalle.Entidad);
        Assert.Null(sinDetalle.UsuarioId);
        Assert.Null(sinDetalle.Username);
        Assert.Null(sinDetalle.EntidadId);
        Assert.Null(sinDetalle.Detalle);
        Assert.Equal(27, conDetalle.UsuarioId);
        Assert.Equal("administrador_prueba", conDetalle.Username);
        Assert.Equal("USUARIO_ACTUALIZADO", conDetalle.Accion);
        Assert.Equal("Usuario", conDetalle.Entidad);
        Assert.Equal("42", conDetalle.EntidadId);
        Assert.Equal("Cambio de rol: Recepcionista → Administrador.", conDetalle.Detalle);
        Assert.All(recientes, registro =>
        {
            Assert.Equal(fecha, registro.Fecha);
            Assert.Equal(DateTimeKind.Utc, registro.Fecha.Kind);
        });
    }
}
