using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Infraestructura.Arranque;
using TallerMecanico.Infraestructura.Correo;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.Infraestructura;

public static class RegistroDependencias
{
    public static IServiceCollection AddInfraestructuraUsuarios(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddScoped<DatabaseConnectionFactory, MySqlConnectionFactory>();
        servicios.AddScoped<UsuarioRepository>();
        servicios.AddScoped<IUsuarioPort>(proveedor => proveedor.GetRequiredService<UsuarioRepository>());
        servicios.AddScoped<IUsuarioConsultaPort>(proveedor => proveedor.GetRequiredService<UsuarioRepository>());
        servicios.AddScoped<IUsuarioAdministracionPort>(proveedor => proveedor.GetRequiredService<UsuarioRepository>());
        servicios.AddScoped<ISolicitudRegistroPort, SolicitudRegistroRepository>();
        servicios.AddScoped<IActivacionInvitacionPort, ActivacionInvitacionMySqlAdapter>();
        servicios.AddScoped<IRevisionSolicitudesPort, RevisionSolicitudesRepository>();
        servicios.AddScoped<IAuditoriaPort, AuditoriaRepository>();
        servicios.AddScoped<IEmailService, GmailEmailService>();
        servicios.Configure<Argon2idOptions>(configuracion.GetSection(Argon2idOptions.SectionName));
        servicios.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        servicios.AddSingleton<IFallbackHash, AuthenticationFallbackHash>();
        servicios.AddScoped<IAuthenticationService, AuthenticationService>();
        servicios.AddScoped<IGeneradorNombreUsuarioPort, GeneradorNombreUsuarioService>();
        servicios.AddScoped<IGestionUsuariosPort, UsuarioAdministracionService>();
        servicios.AddScoped<ICambioContrasenaPort, CambioContrasenaService>();
        servicios.AddScoped<RegistroUsuarioService>();
        servicios.AddScoped<RegistroFacade>();
        servicios.AddScoped<VerificacionEmailService>();
        servicios.AddScoped<ReenvioVerificacionService>();
        servicios.AddScoped<RevisionSolicitudesService>();
        servicios.AddScoped<InicializadorUsuarios>();
        servicios.AddScoped<InicializacionUsuariosConsola>();
        servicios.AddScoped<RestablecimientoAdminConsola>();
        return servicios;
    }
}
