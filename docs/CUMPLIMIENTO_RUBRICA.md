# Cumplimiento de la rúbrica: 3er Entregable

Estado documental al 08/11/2026, con las cuatro aplicaciones SBA integradas. Cubre los requisitos de código y arquitectura de cada grupo interno. La evidencia de implementación se distingue de la ejecución de pruebas y de la defensa; no representa un puntaje confirmado.

**Estados:** ✅ implementación localizada y comprobada | 🔶 decisión de presentación o revisión manual pendiente. Ver [resultados reales de la integración](VALIDACION_SBA.md).

## Arquitectura y patrones

| Exigencia | Estado | Evidencia |
|---|---|---|
| Servicio 1 en Clean Architecture | ✅ | `Aplicacion/CasosUso/Ordenes/` y [ARQUITECTURA_CLEAN.md](ARQUITECTURA_CLEAN.md) |
| Servicios 2 y 3 en Hexagonal | ✅ | Puertos de entrada y de salida en `Aplicacion/Puertos`; ver [ARQUITECTURA_HEXAGONAL.md](ARQUITECTURA_HEXAGONAL.md) |
| Al menos 3 patrones de diseño | ✅ | Facade, Factory Method y Singleton, además de Repository y Unit of Work |
| Regla de dependencias | ✅ | Pruebas de arquitectura aprobadas y Frontend sin referencias a persistencia ni implementaciones de los servicios |
| 4 aplicaciones (SBA) | ✅ | Cuatro imágenes construidas y cuatro procesos saludables con MySQL temporal; HTTPS, permisos y exportaciones comprobados. Ver [validación](VALIDACION_SBA.md) |

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
| Bitácora en atributos de auditoría | 10 | 🔶 | `CreadoPor` y `ModificadoPor` en cada CRUD. Los borrados de catálogos son físicos, así que quedan en la tabla `Auditoria` y no en atributos de la fila; la anulación de órdenes también se registra como `ORDEN_ANULADA` |
| Select de usuarios sin contraseña | 2 | ✅ | `UsuarioListadoDto` |
| Insert con nombre por protocolo y activación por correo | 7 | ✅ | `GeneradorNombreUsuarioService`, token con SHA-256 y vencimiento de 1 hora |
| Dar de baja a un usuario | 1 | ✅ | `DesactivarUsuario` |
| Cambio de contraseña | 10 | ✅ | Actual y dos veces la nueva, política segura, y nuevo inicio de sesión |
| Reporte 1: lista, 2 tablas o más, 2 filtros o más, PDF y Excel | 10 | ✅ | Fecha desde, fecha hasta y estado |
| Reporte 2: sumariado con gráfico, 2 tablas o más, 2 filtros o más, PDF y Excel | 10 | ✅ | Nombre, fecha desde y fecha hasta; gráfico de barras en pantalla, PDF y Excel |

## Verificación de la integración SBA

```powershell
dotnet build TallerMecanico.sln
dotnet test TallerMecanico.sln -m:1 -- xUnit.ParallelizeTestCollections=false
```

Resultado de la integración: **862 pruebas aprobadas, 0 fallidas y 9 omitidas** con MySQL temporal; las omitidas requieren navegador o lectura de PDF. Ver [Validación SBA](VALIDACION_SBA.md).

- La suite completa requiere `TALLER_TEST_MYSQL`, y las pruebas de navegador y de PDF requieren además `PLAYWRIGHT_MODULE` y `TALLER_PDF_PYTHON`; ver el README.
- Flujo de cuenta comprobado con SMTP simulado: invitación, activación de un solo uso, cambio de contraseña y baja.
- Correo real: confirmar manualmente la llegada al buzón del destinatario durante la prueba del equipo.
- Reportes: generación, estructura y contenido comprobados automáticamente; abrir también las salidas en los lectores que se usarán para la defensa.
- Despliegue: cuatro aplicaciones saludables, HTTPS directo en Kestrel, HTTPS interno hacia las APIs y APIs sin puertos públicos comprobados en un proyecto Docker temporal. `NubeUp` realiza la transición del entorno habitual con la configuración privada del equipo.
