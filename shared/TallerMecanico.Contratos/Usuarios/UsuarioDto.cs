namespace TallerMecanico.Contratos.Usuarios;

public sealed record UsuarioDto(int Id, string Username, string? Email, string Rol, bool Activo);

public sealed record EstadoUsuarioDto(bool Activo);

public sealed record ResumenUsuarios(int Total, int Activos, int AdministradoresActivos);

public sealed record CrearInvitacionSolicitud(string Email, string Rol, string Password);

public sealed record ActualizarUsuarioSolicitud(string Email, string Rol, bool Activo);

public sealed record CambiarContrasenaSolicitud(string Actual, string Nueva, string Confirmacion);

public sealed record EmailSolicitud(string Email);

public sealed record TokenSolicitud(string Token);
