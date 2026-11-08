# Arquitectura Hexagonal: Servicios 2 y 3 (Usuarios y Reportes)

> Órdenes (Servicio 1) usa Clean Architecture: ver [ARQUITECTURA_CLEAN.md](ARQUITECTURA_CLEAN.md).
> Stack: Razor Pages .NET 10 + ADO.NET (`MySqlConnector`) + MySQL 8. Sin EF Core.
> `frontend-app/Pages/` conserva sus rutas y consume las APIs por HTTP.
> Usuarios y Reportes son procesos independientes: ver [SBA](SBA.md).

## Puertos y adaptadores

| Lado | Puerto (`Aplicacion/Puertos`) | Quién lo usa o implementa |
|---|---|---|
| Entrada | `IGestionUsuariosPort` | Lo consume el controlador de Usuarios y lo implementa `UsuarioAdministracionService` |
| Entrada | `ICambioContrasenaPort` | Lo consume el controlador de autenticación y lo implementa `CambioContrasenaService` |
| Entrada | `IReporteOrdenesPort`, `IReporteMecanicosPort` | Los consume `ReportesController` del Servicio 3 y los implementan `ReporteOrdenesService` y `ReporteMecanicosService` |
| Salida | `IUsuarioAdministracionPort`, `IActivacionInvitacionPort`, `ISolicitudRegistroPort`, `IAuditoriaPort` | Adaptadores MySQL de `Infraestructura/Persistencia/MySql` |
| Salida | `IConsultaReportesPort` | `ConsultaReportesMySqlAdapter` del Servicio 3 |
| Salida | `IGeneradorReporte*PdfPort`, `IGeneradorReporte*ExcelPort` | Adaptadores QuestPDF y ClosedXML del Servicio 3 |
| Salida | `IEmailService`, `IPasswordHasher` | `GmailEmailService`, `Argon2idPasswordHasher` |

`IGeneradorNombreUsuarioPort` es la excepción: `GeneradorNombreUsuarioService` lo implementa dentro de `Aplicacion`, porque el protocolo es una regla de negocio y no depende de tecnología externa.

## Estructura

```text
backend-services/servicio-usuarios/
  Aplicacion/       Puertos, servicios y fachadas de cuentas
  Dominio/          Usuario y SolicitudRegistro
  Infraestructura/  MySQL, auditoría, correo y Argon2id
  Controllers/      Adaptadores de entrada HTTP: autenticación y usuarios
backend-services/servicio-reportes/
  Aplicacion/       Puertos de entrada/salida y servicios de reportes
  Infraestructura/  Consultas MySQL, PDF, Excel y gráficos
  Controllers/      Adaptador HTTP ReportesController
frontend-app/
  Pages/            Presentación Razor y formularios
  Api/              UsuariosApi, ReportesApi y propagación del usuario
  Seguridad/        Usuario actual y validación de la cuenta activa
shared/TallerMecanico.Contratos/
  Usuarios/         DTO de cuenta y sesión, y ReglasCuenta (compartidas con el Frontend)
  Reportes/         Filtros y documentos de reporte
```

Regla de dependencias dentro de cada API: `Controllers -> Aplicacion -> Dominio`
(Reportes no necesita entidades propias). `Infraestructura` implementa
`Aplicacion/Puertos`; el Frontend se comunica por HTTP y solo comparte DTO.

## Patrones de diseño

| Patrón | Ejemplo del código | Propósito |
|---|---|---|
| Repository | `servicio-usuarios/Infraestructura/Persistencia/MySql/UsuarioRepository.cs` | Aísla el SQL detrás de los puertos de usuarios |
| Adapter | `servicio-reportes/Infraestructura/Documentos/QuestPdfReporteOrdenesAdapter.cs` y adaptadores ClosedXML | Implementa los puertos de generación con bibliotecas concretas |
| Facade | `servicio-usuarios/Aplicacion/Fachadas/RegistroFacade.cs` | Coordina el flujo de solicitud de registro con una entrada común |
| Singleton | `ConfiguracionTaller` (`Lazy<T>`) | Una sola configuración del taller dentro de cada proceso que la usa |

## Principios SOLID

| Principio | Evidencia | Justificación |
|---|---|---|
| Responsabilidad única | Servicios de Reportes y sus adaptadores | Consultar SQL y dibujar un PDF son responsabilidades distintas |
| Abierto/cerrado | `IGeneradorReporte*PdfPort` e `IGeneradorReporte*ExcelPort` | Una implementación nueva se registra sin cambiar la orquestación del reporte |
| Sustitución de Liskov | Puertos de usuarios y dobles de `servicio-usuarios.Tests` | Los servicios funcionan igual con adaptadores reales o dobles |
| Segregación de interfaces | `IUsuarioConsultaPort` e `IUsuarioAdministracionPort` | Consultar y administrar tienen contratos separados |
| Inversión de dependencias | Constructor de `UsuarioAdministracionService` | La aplicación depende de puertos; MySQL, correo y hash quedan en Infraestructura |

## Diagramas

En [`docs/diagramas/`](diagramas/): `Clases_Usuarios_Hexagonal.puml`,
`Clases_Reportes_Hexagonal.puml` y `Secuencia_Login.puml`.

## Auditoría (quién hizo qué)

- Creador: `CreadoPor` + `FechaCreacion` en Clientes, Vehículos, Productos, Servicios, Mecánicos y Órdenes (desde sesión, nunca del formulario). En Usuarios, `CreadoPor` es el administrador que envió la invitación.
- Modificación: `ModificadoPor` + `FechaModificacion` en las entidades editables.
- Anulación: `AnuladoPor` + `FechaAnulacion` en Órdenes.
- Eliminación en catálogos: como el borrado es físico, queda en la tabla `Auditoria` con usuario, fecha y datos del registro (`CLIENTE_ELIMINADO`, `VEHICULO_ELIMINADO`, `PRODUCTO_ELIMINADO`, `MECANICO_ELIMINADO` y `SERVICIO_ELIMINADO`).
- Baja lógica de una orden: además de sus columnas, queda como `ORDEN_ANULADA` en la tabla `Auditoria`.
- Eventos: tabla `Auditoria` y trigger `TRG_Servicios_HistorialCosto`.

## Gestión de usuarios (HU3)

- **Protocolo del nombre de usuario**: el administrador no lo escribe.
  `GeneradorNombreUsuarioService` toma la parte local del correo, elimina
  acentos, pasa a minúsculas, conserva letras/números/`.`/`_`/`-`,
  convierte el resto de separadores en `-`, recorta guiones y limita a 50
  caracteres. Si el resultado tiene menos de 3 caracteres usa `usuario`.
  Ante colisión agrega sufijos `-1`, `-2`, ... (máximo 100 intentos)
  comprobando `Usuarios` y `SolicitudesRegistro`.
- **Invitación**: normaliza el correo a minúsculas, valida la contraseña
  (8–128, mayúscula, minúscula, número y símbolo, sin espacios en los
  extremos), guarda el hash Argon2id en `SolicitudesRegistro`, genera un
  token aleatorio de 32 bytes (64 hex) y solo persiste su SHA-256, con
  expiración de 1 hora. El correo lleva el enlace de activación.
- **Activación**: `ActivacionInvitacionMySqlAdapter` busca y bloquea la
  solicitud por hash de token dentro de una transacción, crea el usuario
  y marca la solicitud como `Aprobada`; si falla, revierte ambas
  operaciones. La auditoría registra `USUARIO_INVITADO` y
  `USUARIO_ACTIVADO`.

## Reportes

El Servicio 3 es independiente en `backend-services/servicio-reportes` y aplica
arquitectura hexagonal. La presentación Razor (`frontend-app/Pages/Reportes`) ya no accede a
MySQL ni genera archivos: consume `ReportesApi` por HTTP.

| Reporte | Tablas | Filtros | Salidas |
|---|---|---|---|
| 1: lista ordenada de órdenes | Ordenes, Vehiculos, Clientes, Mecanicos, Servicios y DetalleServiciosOrdenes | Fecha desde, fecha hasta y estado | Pantalla, PDF y Excel |
| 2: información sumariada por mecánico, con gráfico de barras | Mecanicos y Ordenes | Nombre, fecha desde y fecha hasta | Pantalla, PDF y Excel |

Ambos usan el logo en el encabezado y los datos sumariados al pie.

### Puertos y adaptadores

- Entrada: `IReporteOrdenesPort` e `IReporteMecanicosPort`, implementados por
  `ReporteOrdenesService` y `ReporteMecanicosService`; los invoca `ReportesController`.
- Salida: `IConsultaReportesPort`, implementado por `ConsultaReportesMySqlAdapter`
  (consultas con filtros, agregados y escape de comodines `%`/`_`).
- Salida: `IGeneradorReporte*PdfPort`, implementados por `QuestPdfReporte*Adapter`
  con `GraficoEstadosOrdenes` y `GraficoMecanicos`.
- Salida: `IGeneradorReporte*ExcelPort`, implementados por `ClosedXmlReporte*Adapter`
  con `XlsxBuilder`.
- Composición: el `Program.cs` del servicio registra la fábrica de conexiones, los
  adaptadores y los servicios.

### Contrato HTTP

| Método y ruta | Filtros (`query`) | Respuesta |
|---|---|---|
| `GET /api/reportes/ordenes` | `fechaDesde`, `fechaHasta`, `estado` | JSON `DocumentoReporteOrdenes` |
| `GET /api/reportes/ordenes/pdf` | `fechaDesde`, `fechaHasta`, `estado` | `application/pdf` |
| `GET /api/reportes/ordenes/excel` | `fechaDesde`, `fechaHasta`, `estado` | XLSX |
| `GET /api/reportes/mecanicos` | `nombreMecanico`, `fechaDesde`, `fechaHasta`, `mecanicoId` | JSON `DocumentoReporteMecanicos` |
| `GET /api/reportes/mecanicos/pdf` | `nombreMecanico`, `fechaDesde`, `fechaHasta`, `mecanicoId` | `application/pdf` |
| `GET /api/reportes/mecanicos/excel` | `nombreMecanico`, `fechaDesde`, `fechaHasta`, `mecanicoId` | XLSX |

El frontend envía la clave interna; el controlador exige rol Administrador y toma
`GeneradoPor` de las cabeceras de usuario validadas (`sistema` si no viene). Los filtros inválidos
responden 400, la falta de sesión 401/403 y una base caída 503 mediante
`ErroresApiFilter`. Las páginas Razor conservan sus rutas y ahora son asíncronas.

Reportes lee las tablas de Órdenes y Mecánicos directamente porque la base es
compartida; la justificación está en [SBA](SBA.md#decisiones-de-la-arquitectura).

Pruebas: `tests/servicio-reportes.Tests` cubre servicios, adaptadores (QuestPDF y
ClosedXML), arquitectura e integración MySQL aislada; el contrato HTTP y el flujo
navegador permanecen en `tests/frontend-app.Tests`.

## Dashboard por rol

`DashboardService` pertenece al Servicio 1 y entrega conteos reales de MySQL.
`Pages/Index` consume su API y muestra la vista de Administrador o Recepcionista;
el banner de auditoría solo es visible para Administrador.
