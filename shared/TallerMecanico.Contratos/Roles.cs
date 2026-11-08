namespace TallerMecanico.Contratos;

// Roles del sistema. Personal es la lista para [Authorize], que los recibe separados por coma.
public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Recepcionista = "Recepcionista";
    public const string Personal = Administrador + "," + Recepcionista;
}
