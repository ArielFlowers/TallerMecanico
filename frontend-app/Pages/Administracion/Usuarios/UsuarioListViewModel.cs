using System.ComponentModel.DataAnnotations;

namespace TallerMecanico.Pages.Administracion.Usuarios;

public sealed class UsuarioListViewModel
{
    public int Id { get; set; }

    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Rol")]
    public string Rol { get; set; } = string.Empty;

    [Display(Name = "Activo")]
    public bool Activo { get; set; }

    [Display(Name = "Fecha de creación")]
    [DataType(DataType.DateTime)]
    public DateTime FechaCreacion { get; set; }
}