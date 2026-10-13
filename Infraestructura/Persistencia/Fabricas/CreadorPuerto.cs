namespace TallerMecanico.Patterns.FactoryMethod;

// Variante tipada del Factory Method para contratos que no son un CRUD genérico.
public abstract class CreadorPuerto<TPuerto>
{
    public abstract TPuerto CrearRepositorio();
}
