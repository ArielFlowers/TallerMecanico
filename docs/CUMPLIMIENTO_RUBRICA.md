# Cumplimiento de la rúbrica: 3er Entregable

Estado al 04/11/2026, según la rama `feature/adrian-3erEntregable` (basada en `main` `6a3ce87`). El trabajo de ramas sin fusionar no se cuenta como hecho. Actualizar este documento cuando se integre cada rama.

**Estados:** ✅ comprobado | 🔶 implementado, falta comprobarlo o completarlo | ❌ falta

## Resumen

| Bloque | Estado | Pendiente principal |
|---|---|---|
| Arquitecturas | 🔶 | Servicio 1 en Clean; falta completar los Servicios 2 y 3 en Hexagonal |
| Servicio 1 (60) | 🔶 | Llamar a otra UI si falta un registro, refresco del listado, comprobante en otra pestaña y logo |
| Servicios 2 y 3 (60) | ❌ | Generador de usuario, cambio de contraseña y los 2 reportes |
| Documento, Anexo 1 (15) | ❌ | Sin redactar; solo existen las guías de arquitectura |
| Clean Code y SOLID (10) | 🔶 | Ejemplos en las guías; falta el enlace al repositorio en el documento y sacar SQL de `UsuarioAdministracionService` |
| Defensa (15) | ❌ | La guía se hará cuando estén todos los puntos |
| Ortografía de la UI (−1 por error) | ❌ | Revisión completa sin hacer |

## Arquitectura y patrones

| Exigencia | Estado | Evidencia o pendiente |
|---|---|---|
| Servicio 1 en Clean Architecture | ✅ | `Aplicacion/CasosUso/Ordenes/` y [ARQUITECTURA_CLEAN.md](ARQUITECTURA_CLEAN.md) |
| Servicios 2 y 3 en Hexagonal | 🔶 | Usuarios usa `Aplicacion/Puertos`, con deuda en `UsuarioAdministracionService`; Reportes no está en `main` |
| SBA | 🔶 | Según lo informado por el equipo, se acepta un solo proyecto con servicios lógicos; falta documentarlo en el Anexo 1 |
| Al menos 3 patrones de diseño | ✅ | Facade, Factory Method y Singleton, además de Repository y Unit of Work |
| Regla de dependencias | 🔶 | `ArquitecturaTests` en verde, con 6 servicios en su lista de pendientes |

## Servicio 1: Órdenes (60)

| Requisito | Pts | Estado | Evidencia o pendiente |
|---|---:|---|---|
| Al menos 2 buscadores | 8 | ✅ | 4 buscadores; usados en navegador |
| Llamar a otra UI si falta un registro | 5 | ❌ | Existen `OnPostNuevoVehiculo`, `OnPostNuevoCliente` y `OnPostNuevoProducto`, pero **ninguna pantalla los llama**: al no encontrar un registro solo aparece "No se encontraron coincidencias" (Santiago) |
| Agregar, editar y eliminar registros temporales | 9 | ✅ | Probado en navegador; el total se recalculó correctamente |
| Datos sumariados | 2 | ✅ | Líneas, unidades y total |
| Confirmación al guardar | 1 | ✅ | Diálogo "Confirmar orden" |
| Transacción con commit y rollback, y usuario creador | 10 | ✅ | `OrdenesMySqlTests`; orden de prueba con total, stock y `CreadoPor` correctos |
| Anulación, restitución y usuario que anula | 10 | ✅ | Probada en navegador: estado `Anulada`, `AnuladoPor` y `FechaAnulacion` registrados, y stock de 7 a 10 |
| Select con refresco | 1 | 🔶 | Una fila por orden; **no hay botón Refrescar en `main`** (Santiago) |
| Modal de detalle de solo lectura | 5 | ✅ | Comprobado: sin campos ni formularios |
| Comprobante con línea gráfica y logo | 1 | 🔶 | Colores propios y monograma "AP"; falta el logo oficial (Gabo) |
| Comprobante con datos completos | 7 | ✅ | `ComprobanteOrdenPdf` y pruebas de PDF |
| Comprobante automático | 1 | 🔶 | Se abre solo, pero en un modal; el Anexo 2 indica otra pestaña (Santiago) |

## Servicios 2 y 3: Usuarios y Reportes (60)

| Requisito | Pts | Estado | Evidencia o pendiente |
|---|---:|---|---|
| Login, sesiones y menú por rol | 10 | 🔶 | `_Layout.cshtml` usa `IsInRole`; falta revisar la UI |
| Bitácora en atributos de auditoría | 10 | 🔶 | Las entidades tienen `ModificadoPor`; falta verificar cada CRUD |
| Select de usuarios sin contraseña | 2 | 🔶 | Falta comprobarlo |
| Insert con nombre de usuario por protocolo y activación por correo | 7 | ❌ | Hay invitación por correo; **falta el generador de nombre de usuario** |
| Dar de baja a un usuario | 1 | 🔶 | `DesactivarUsuario`; falta comprobarlo |
| Cambio de contraseña | 10 | ❌ | No está en `main` |
| Reporte 1: lista, 2 tablas, 2 filtros, PDF y Excel | 10 | ❌ | No está en `main`; el menú no lleva a ninguna página |
| Reporte 2: sumariado con gráfico, 2 tablas, 2 filtros, PDF y Excel | 10 | ❌ | No está en `main` |

Hay trabajo sin fusionar en `origin/Ariel/HU3`. Las correcciones que necesita están en el plan del entregable.

## Documento: Anexo 1 (15)

| Sección | Estado |
|---|---|
| Carátula, introducción y objetivo | ❌ |
| Contexto y requisitos funcionales | ❌ |
| C4 niveles 1, 2, 3 y combinado | ❌ |
| Diagramas de clases y capturas de patrones | ❌ |
| Capturas de los estilos arquitectónicos | ❌ |
| Ejemplos de cada principio SOLID | 🔶 Órdenes en [ARQUITECTURA_CLEAN.md](ARQUITECTURA_CLEAN.md); faltan capturas y pasarlos al documento |
| URL y organización del repositorio | ❌ |
| Conclusiones y bibliografía | ❌ |
| Subida individual al LMS | ❌ |

## Cómo se verificó

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter "Area=Backend"
```

- Las áreas Datos, API y Frontend requieren `TALLER_TEST_MYSQL`; ver el README.
- Última ejecución completa, el 04/11/2026 sobre `a0f2909`: 526 pruebas sin fallos (Backend 390, Datos 122, API 10 y Frontend 4).
- Prueba manual en Chrome sobre una base temporal aislada: crear orden, editar líneas, guardar, comprobante, listado, detalle y anular.

## Observaciones de la prueba manual

- Al guardar una orden aparecen dos mensajes de éxito en `Pages/Ordenes/Index.cshtml`, y uno se muestra en rojo. El mensaje de anulación también sale en rojo.
