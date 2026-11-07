extern alias Ordenes;

// La identidad de los dobles de negocio pertenece al backend. Los dobles de
// sesión implementan además la interfaz local del Frontend cuando corresponde.
global using ICurrentUser = Ordenes::TallerMecanico.Aplicacion.Puertos.ICurrentUser;
