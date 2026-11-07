namespace TallerMecanico.Contratos.Usuarios;

public sealed record CredencialesLogin(string Username, string Password);

// Lo que el Frontend guarda en la cookie de sesión.
public sealed record UsuarioSesionDto(int Id, string Username, string Rol);
