using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;

public class CambioContrasenaServiceTests
{
    private sealed class UsuarioFalso : IUsuarioPort
    {
        public Usuario Usuario { get; set; } = new()
        {
            Id = 1,
            Username = "usuario1",
            PasswordHash = "HASH_actual",
            Email = "usuario1@test.com",
            Activo = true,
            Rol = "Recepcionista"
        };
        public Usuario? ObtenerPorUsername(string username) => Usuario;
        public Usuario? ObtenerPorId(int id) => Usuario;
        public bool ExisteUsername(string username) => false;
        public bool ExisteEmail(string email) => false;
        public void Agregar(Usuario usuario) { }
        public void Actualizar(Usuario usuario) => Usuario = usuario;
    }

    private sealed class HasherFalso : IPasswordHasher
    {
        public string GenerarHash(string password) => "HASH_" + password;
        public bool VerificarPassword(string password, string hash) => hash == "HASH_" + password;
    }

    private sealed class CurrentUserFalso : ICurrentUser
    {
        public bool EstaAutenticado { get; set; } = true;
        public int? UsuarioId { get; set; } = 1;
        public string? Username { get; set; } = "usuario1";
        public string? Rol { get; set; } = "Recepcionista";
    }

    private sealed class AuditoriaFalsa : IAuditoriaPort
    {
        public RegistroAuditoria? Ultimo { get; private set; }
        public void Registrar(RegistroAuditoria registro) => Ultimo = registro;
        public IReadOnlyList<RegistroAuditoria> ObtenerRecientes(int cantidad) => new List<RegistroAuditoria>();
    }

    [Fact]
    public void Cambiar_Exitoso_ActualizaHashYAudita()
    {
        var usuarios = new UsuarioFalso();
        var hasher = new HasherFalso();
        var currentUser = new CurrentUserFalso();
        var auditoria = new AuditoriaFalsa();
        var servicio = new CambioContrasenaService(usuarios, hasher, currentUser, auditoria);

        bool resultado = servicio.Cambiar("actual", "NuevaClave123!", "NuevaClave123!", out string error);

        Assert.True(resultado);
        Assert.Equal(string.Empty, error);
        Assert.Equal("HASH_NuevaClave123!", usuarios.Usuario.PasswordHash);
        Assert.NotNull(auditoria.Ultimo);
        Assert.Equal("CONTRASENA_CAMBIADA", auditoria.Ultimo.Accion);
        Assert.Equal("usuario1", auditoria.Ultimo.Username);
    }

    [Fact]
    public void Cambiar_RechazaContrasenaActualIncorrecta()
    {
        var servicio = CrearServicio(out _);
        bool resultado = servicio.Cambiar("incorrecta", "NuevaClave123!", "NuevaClave123!", out string error);
        Assert.False(resultado);
        Assert.Equal("La contraseña actual es incorrecta.", error);
    }

    [Fact]
    public void Cambiar_RechazaNuevaIgualAActual()
    {
        var usuarios = new UsuarioFalso { Usuario = { PasswordHash = "HASH_NuevaClave123!" } };
        var servicio = new CambioContrasenaService(usuarios, new HasherFalso(), new CurrentUserFalso(), new AuditoriaFalsa());
        bool resultado = servicio.Cambiar("NuevaClave123!", "NuevaClave123!", "NuevaClave123!", out string error);
        Assert.False(resultado);
        Assert.Equal("La contraseña nueva debe ser diferente de la actual.", error);
    }

    [Fact]
    public void Cambiar_RechazaConfirmacionNoCoincide()
    {
        var servicio = CrearServicio(out _);
        bool resultado = servicio.Cambiar("actual", "NuevaClave123!", "OtraClave123!", out string error);
        Assert.False(resultado);
        Assert.Equal("Las contraseñas nuevas no coinciden.", error);
    }

    [Theory]
    [InlineData("", "NuevaClave123!", "NuevaClave123!")]
    [InlineData("actual", "", "NuevaClave123!")]
    [InlineData("actual", "NuevaClave123!", "")]
    public void Cambiar_RechazaCamposVacios(string actual, string nueva, string confirmacion)
    {
        var servicio = CrearServicio(out _);
        bool resultado = servicio.Cambiar(actual, nueva, confirmacion, out string error);
        Assert.False(resultado);
        Assert.Equal("Todos los campos son obligatorios.", error);
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("sinmayus123!")]
    [InlineData("SINMINUS123!")]
    [InlineData("SinNumero!")]
    [InlineData("SinEspecial123")]
    public void Cambiar_RechazaPoliticaInvalida(string nueva)
    {
        var servicio = CrearServicio(out _);
        bool resultado = servicio.Cambiar("actual", nueva, nueva, out string error);
        Assert.False(resultado);
        Assert.Contains("carácter especial", error);
    }

    [Fact]
    public void Cambiar_RechazaUsuarioNoAutenticado()
    {
        var currentUser = new CurrentUserFalso { EstaAutenticado = false };
        var servicio = new CambioContrasenaService(
            new UsuarioFalso(), new HasherFalso(), currentUser, new AuditoriaFalsa());
        bool resultado = servicio.Cambiar("actual", "NuevaClave123!", "NuevaClave123!", out string error);
        Assert.False(resultado);
        Assert.Equal("Debe iniciar sesión.", error);
    }

    private static CambioContrasenaService CrearServicio(out UsuarioFalso usuarios)
    {
        usuarios = new UsuarioFalso();
        var hasher = new HasherFalso();
        var currentUser = new CurrentUserFalso();
        var auditoria = new AuditoriaFalsa();
        return new CambioContrasenaService(usuarios, hasher, currentUser, auditoria);
    }
}
