using TallerMecanico.Aplicacion.Validaciones;
using Xunit;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public class ValidadorSolicitudRegistroTests
{
    [Theory]
    [InlineData("empleado123")]
    [InlineData("juan.perez")]
    [InlineData("maria_lopez")]
    [InlineData("usuario-01")]
    public void UsuarioValido_AceptaUsuariosCorrectos(string username)
    {
        Assert.True(ValidadorSolicitudRegistro.UsuarioValido(username));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("usuario con espacios")]
    [InlineData("usuario@correo")]
    public void UsuarioValido_RechazaUsuariosIncorrectos(string username)
    {
        Assert.False(ValidadorSolicitudRegistro.UsuarioValido(username));
    }

    [Theory]
    [InlineData("persona@gmail.com")]
    [InlineData("persona@outlook.com")]
    [InlineData("persona@hotmail.com")]
    [InlineData("empleado@empresa.com.bo")]
    public void EmailValido_AceptaDistintosProveedores(string email)
    {
        Assert.True(ValidadorSolicitudRegistro.EmailValido(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("correo-invalido")]
    [InlineData("persona@")]
    [InlineData(" persona@gmail.com")]
    public void EmailValido_RechazaCorreosIncorrectos(string email)
    {
        Assert.False(ValidadorSolicitudRegistro.EmailValido(email));
    }

    [Theory]
    [InlineData("Clave12!")]
    [InlineData("ClaveSegura12!")]
    public void PasswordValido_AceptaPasswordConComplejidad(string password)
    {
        Assert.True(ValidadorSolicitudRegistro.PasswordValido(password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Corta1!")]
    [InlineData("sinmayus1!")]
    [InlineData("SINMINUS1!")]
    [InlineData("SinNumero!")]
    [InlineData("SinEspecial1")]
    [InlineData(" ClaveSeg12!")]
    [InlineData("ClaveSeg12! ")]
    public void PasswordValido_RechazaPasswordsIncorrectas(string password)
    {
        Assert.False(ValidadorSolicitudRegistro.PasswordValido(password));
    }

    [Fact]
    public void PasswordValido_RechazaMasDe128Caracteres()
    {
        Assert.False(
            ValidadorSolicitudRegistro.PasswordValido(
                "Aa1!" + new string('a', 125)));
    }
}