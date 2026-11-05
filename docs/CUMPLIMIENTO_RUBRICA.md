# Cumplimiento de la rúbrica: 3er Entregable

Estado al 05/11/2026, en la rama `fix/grupo-funcionalidades-rubrica` (basada en `main` `778466f`). Cubre los 60 puntos de código y arquitectura de cada grupo interno. El documento y el repositorio se revisan aparte.

**Estados:** ✅ comprobado | 🔶 cumple, con una decisión pendiente

## Arquitectura y patrones

| Exigencia | Estado | Evidencia |
|---|---|---|
| Servicio 1 en Clean Architecture | ✅ | `Aplicacion/CasosUso/Ordenes/` y [ARQUITECTURA_CLEAN.md](ARQUITECTURA_CLEAN.md) |
| Servicios 2 y 3 en Hexagonal | ✅ | Puertos de entrada y de salida en `Aplicacion/Puertos`; ver [ARQUITECTURA_HEXAGONAL.md](ARQUITECTURA_HEXAGONAL.md) |
| Al menos 3 patrones de diseño | ✅ | Facade, Factory Method y Singleton, además de Repository y Unit of Work |
| Regla de dependencias | ✅ | `ArquitecturaTests` sin excepciones pendientes |
| 4 aplicaciones (SBA) | 🔶 | Un solo proyecto con servicios lógicos, según lo informado por el equipo; falta la confirmación del docente |

## Grupo 1: Servicio 1, Órdenes (60)

| Requisito | Pts | Estado | Evidencia |
|---|---:|---|---|
| Al menos 2 buscadores | 8 | ✅ | Vehículo, servicio, mecánico y producto |
| Llamar a otra UI si falta un registro | 5 | ✅ | Nuevo cliente, vehículo, producto y mecánico, conservando el borrador. El mecánico es solo para administradores, y los servicios vienen de un catálogo fijo |
| Agregar, modificar y eliminar registros temporales | 9 | ✅ | Agregar, actualizar cantidad y quitar |
| Datos sumariados | 2 | ✅ | Líneas, unidades y total |
| Confirmación al guardar | 1 | ✅ | Diálogo "Confirmar orden" |
| Transacción con commit y rollback, y usuario creador | 10 | ✅ | `CrearOrdenCasoUso`, `MySqlUnidadTrabajoOrden`, `OrdenesMySqlTests` |
| Anulación, restitución y usuario que anula | 10 | ✅ | `AnularOrdenCasoUso`, `OrdenesMySqlTests` |
| Select con refresco | 1 | ✅ | Una fila por orden y botón Refrescar |
| Modal de detalle de solo lectura | 5 | ✅ | `_DetalleOrden.cshtml` |
| Comprobante con línea gráfica y logo | 1 | 🔶 | Colores propios y monograma "AP" como logo; se puede reemplazar `logo-autotaller.png` si hay uno oficial |
| Comprobante con datos completos | 7 | ✅ | Datos generales, detalle, subtotales, total, fecha y hora de generación, y usuario |
| Comprobante automático | 1 | ✅ | Pestaña nueva, con enlace si el navegador la bloquea |

## Grupo 2: Servicios 2 y 3, Usuarios y Reportes (60)

| Requisito | Pts | Estado | Evidencia |
|---|---:|---|---|
| Login, sesiones y menú por rol | 10 | ✅ | Cada rol ve opciones distintas y ninguna queda sin destino |
| Bitácora en atributos de auditoría | 10 | 🔶 | `CreadoPor` y `ModificadoPor` en cada CRUD. Los borrados de catálogos son físicos, así que quedan en la tabla `Auditoria` y no en atributos de la fila |
| Select de usuarios sin contraseña | 2 | ✅ | `UsuarioListadoDto` |
| Insert con nombre por protocolo y activación por correo | 7 | ✅ | `GeneradorNombreUsuarioService`, token con SHA-256 y vencimiento de 1 hora |
| Dar de baja a un usuario | 1 | ✅ | `DesactivarUsuario` |
| Cambio de contraseña | 10 | ✅ | Actual y dos veces la nueva, política segura, y nuevo inicio de sesión |
| Reporte 1: lista, 2 tablas o más, 2 filtros o más, PDF y Excel | 10 | ✅ | Fecha desde, fecha hasta y estado |
| Reporte 2: sumariado con gráfico, 2 tablas o más, 2 filtros o más, PDF y Excel | 10 | ✅ | Nombre, fecha desde y fecha hasta; gráfico de barras en pantalla, PDF y Excel |

## Cómo se verificó

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj -- xUnit.ParallelizeTestCollections=false
```

- La suite completa requiere `TALLER_TEST_MYSQL`, y las pruebas de navegador y de PDF requieren además `PLAYWRIGHT_MODULE` y `TALLER_PDF_PYTHON`; ver el README.
- Recorrido con cada rol en Edge sobre una base temporal aislada: menú, filtros del Reporte 2, exportaciones de ambos reportes y alta de mecánico desde la orden.
- Flujo de cuenta completo con Mailpit: invitación, correo con nombre de usuario, activación, enlace de un solo uso, cambio de contraseña y baja.
- Envío por Gmail aceptado por SMTP; la llegada al buzón se confirma manualmente.
- Reportes abiertos en Excel sin reparación, con logo, datos sumariados y gráfico.
