# Arquitectura Hexagonal – TallerMecanico (rama `refactor/hexagonal`)

> Monolito por Responsabilidad Técnica + Hexagonal (Puertos y Adaptadores).
> Stack: Razor Pages .NET 10 + ADO.NET (`MySqlConnector`) + MySQL 8. Sin EF Core.
> `Pages/` no se mueve físicamente (rompería rutas Razor): se declara como
> **Presentación**. Sin Docker de app en este sprint (solo compose de BD).

## Estructura

```text
Dominio/
  Entidades/      Cliente, Vehiculo, Producto, Servicio, Mecanico, Orden,
                  DetalleOrden, Usuario, HistorialCostoServicio, ...
  Servicios/      ImporteEnPalabras (lógica de dominio pura)
Aplicacion/
  Puertos/        21 contratos I*Port (IClientePort, IOrdenPort, ...,
                  ICurrentUser, IPasswordHasher, IComprobanteOrdenPdf)
  CasosUso/       DTOs de orden (CrearOrdenSolicitud, ResultadoOrden, ...)
  Fachadas/       OrdenServicioFacade, ConsultaOrdenesFacade, RegistroFacade
  Servicios/      casos de uso por entidad + DashboardService
  Validaciones/   Validacion* por entidad
  Dto/            ViewModels de binding Razor
Infraestructura/
  Persistencia/       DatabaseInitializer, DatabaseConnectionFactory,
                      IRepository<T>, repos legados (Mecanico, Servicio)
  Persistencia/MySql/ adaptadores MySQL (Cliente, Vehiculo, Producto,
                      Orden, Detalle, Usuario, Auditoria, ...) +
                      MySqlUnidadTrabajoOrden (misma conexión/transacción)
  Persistencia/Fabricas/ Creador* (Factory Method)
  Persistencia/Scripts/  DDL MySQL de referencia
  Sesion/         borradores + token (puertos de sesión)
  Seguridad/      CurrentUser, Argon2idPasswordHasher
  Documentos/     ComprobanteOrdenPdf (QuestPDF)
  Arranque/       inicialización de usuarios / restablecimiento admin
  Correo/         GmailEmailService
  Configuracion/  ConfiguracionTaller (Singleton vía DI)
Presentacion/     Pages/**/*.cshtml(.cs) + wwwroot (no movida, ver nota)
```

Regla de dependencias: `Presentacion -> Aplicacion -> Dominio`;
`Infraestructura` implementa `Aplicacion/Puertos`. DI solo contra
puertos/fachadas; `new Repo(` solo en Fábricas; `new MySqlConnection`
solo en Persistencia.

## Patrones (3, sin extras)

| Patrón | Dónde |
|---|---|
| Facade (obligatorio) | `Aplicacion/Fachadas/OrdenServicioFacade` (crear/anular), `ConsultaOrdenesFacade`, `RegistroFacade`. La Page solo llama a la fachada |
| Factory Method | `Infraestructura/Persistencia/Fabricas/Creador*` retornan `IRepository<T>` / puertos; registrados en `Program.cs` |
| Singleton | `Infraestructura/Configuracion/ConfiguracionTaller` (`Lazy<T>` thread-safe) registrado `AddSingleton(Instancia)` e inyectado en Pages y `ComprobanteOrdenPdf` |

## Auditoría (quién hizo qué)

- Creador: `CreadoPor` + `FechaCreacion` en Clientes, Vehículos,
  Productos, Servicios, Mecánicos y Órdenes (desde sesión, nunca del form).
- Modificación: `ModificadoPor` + `FechaModificacion` en las 5 entidades
  editables (UPDATE las fija, INSERT las deja NULL).
- Anulación: `AnuladoPor` + `FechaAnulacion` en Órdenes + restitución 1:1.
- Eventos: tabla `Auditoria` (login/logout) + trigger
  `TRG_Servicios_HistorialCosto` intacto (costo sí registra, descripción no).

## Dashboard por rol

`DashboardService` con conteos reales (mecánicos ya no hardcodeado).
`Pages/Index`: subtítulo Vista Administrador/Recepcionista; banner de
auditoría solo visible para Administrador (el menú lateral ya era por rol).

## Verificación

- `dotnet build`: 0 warnings, 0 errors (SDK 10).
- `node --check wwwroot/js/ordenes.js`: OK (conflicto fusionado).
- SQL parametrizado 100% (`@Param` + `ESCAPE '!'` en LIKE); sin SQL en Pages.
- Pendiente próximo sprint: Dockerfile app, Strategy de cálculo (4.º patrón,
  fuera de rúbrica), reutilizar parciales buscadores en `Crear`.
