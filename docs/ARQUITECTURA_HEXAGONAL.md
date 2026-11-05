# Arquitectura Hexagonal: Servicios 2 y 3 (Usuarios y Reportes)

> Órdenes (Servicio 1) usa Clean Architecture: ver [ARQUITECTURA_CLEAN.md](ARQUITECTURA_CLEAN.md).
> Stack: Razor Pages .NET 10 + ADO.NET (`MySqlConnector`) + MySQL 8. Sin EF Core.
> `Pages/` no se mueve físicamente (rompería rutas Razor): se declara como **Presentación**.

## Estado actual

- **Servicio 2 (Usuarios):** ya está en `main` con puertos y adaptadores. `UsuarioAdministracionService` todavía accede a la base directamente, y esa deuda figura en `ArquitecturaTests`.
- **Servicio 3 (Reportes):** todavía no está en `main`; está en integración en una rama aparte.

## Estructura

```text
Aplicacion/
  Puertos/        Contratos I*Port de lo que no es Órdenes: IUsuarioPort,
                  IUsuarioAdministracionPort, IAuditoriaPort, IEmailService,
                  IPasswordHasher, IClientePort, IProductoPort, IVehiculoPort...
  Fachadas/       RegistroFacade
  Servicios/      Lógica por entidad y DashboardService
  Validaciones/   Validacion* por entidad
  Dto/            ViewModels de binding Razor
Infraestructura/
  Persistencia/MySql/    Adaptadores MySQL (Usuario, Auditoria, Cliente, Producto...)
  Persistencia/Fabricas/ Creador* (Factory Method)
  Seguridad/             CurrentUser, Argon2idPasswordHasher
  Correo/                GmailEmailService
  Cache/                 RedisCacheAdapter
Presentacion/     Pages/**/*.cshtml(.cs) + wwwroot (no movida, ver nota)
```

Regla de dependencias: `Presentacion -> Aplicacion -> Dominio`; `Infraestructura` implementa `Aplicacion/Puertos`.

## Patrones

| Patrón | Dónde |
|---|---|
| Facade | `Aplicacion/Fachadas/RegistroFacade` |
| Factory Method | `Infraestructura/Persistencia/Fabricas/Creador*`, registrados en `Program.cs` |
| Singleton | `Aplicacion/Configuracion/ConfiguracionTaller` (`Lazy<T>`), compartido con Órdenes |

## Auditoría (quién hizo qué)

- Creador: `CreadoPor` + `FechaCreacion` en Clientes, Vehículos, Productos, Servicios, Mecánicos y Órdenes (desde sesión, nunca del formulario).
- Modificación: `ModificadoPor` + `FechaModificacion` en las entidades editables.
- Anulación: `AnuladoPor` + `FechaAnulacion` en Órdenes.
- Eventos: tabla `Auditoria` y trigger `TRG_Servicios_HistorialCosto`.

## Dashboard por rol

`DashboardService` entrega conteos reales. `Pages/Index` muestra la vista de Administrador o Recepcionista, y el banner de auditoría solo es visible para Administrador.
