# Tarjeta 4 del Sprint 3 Creación y anulación de órdenes

Esta entrega implementa la transacción principal y la anulación lógica de la tarjeta 4 de `Sprint3_Plan_EC2_Corregido.docx`. Una orden relaciona un vehículo con cliente asignado, un mecánico y productos con cantidades. La operación guarda cabecera y detalles y descuenta stock de forma atómica. La anulación conserva el historial y devuelve las cantidades una sola vez.

La interfaz completa corresponde a la tarjeta 3 y el listado, detalle visual y comprobante corresponden a la tarjeta 5. Esta entrega publica sus contratos y deja registrado en DI el punto de entrada que deben utilizar esas páginas.

## Arquitectura y patrones

El flujo es `Razor Page → OrdenServicioFacade → OrdenServicioService → puertos → adaptadores MySQL`. La página no debe insertar órdenes ni modificar stock directamente.

- `OrdenServicioFacade` aplica Facade: entrega token, creación y anulación desde un único punto de entrada.
- `OrdenServicioService` coordina reglas, permisos y transacciones; depende de puertos, sin referencias a Razor, sesión, `MySqlConnector`, `DbConnection` ni `DbTransaction`.
- `ValidacionOrdenes` comprueba entrada y agrupa productos repetidos con sumas verificadas para evitar desbordamientos.
- `IUnidadTrabajoOrdenPort` inicia una unidad de trabajo. `ITransaccionOrden` entrega repositorios que participan en esa transacción, confirma o revierte y libera recursos.
- Los repositorios implementan puertos específicos de cabecera, órdenes, detalles y stock. `CreadorOrden` y `CreadorDetalle` heredan de `CreadorPuerto<TPuerto>` y aplican Factory Method sin casts a implementaciones concretas. Los creadores existentes de CRUD conservan su funcionamiento.

Las órdenes no ofrecen edición ni borrado mediante su puerto. Sus operaciones son crear, consultar para idempotencia y anular. Esto evita forzar métodos CRUD que no representan los casos de uso de la tarjeta.

## Contrato para la tarjeta 3

Inyectar `OrdenServicioFacade` en el PageModel. La solicitud recibe solamente IDs, fecha, token y cantidades; no incluye precio ni total.

```csharp
// GET de un formulario nuevo, después de cargar sus opciones.
Token = _facade.GenerarTokenFormulario();

// POST: Formulario.Lineas contiene ProductoId y Cantidad.
var solicitud = new CrearOrdenSolicitud(
    Formulario.VehiculoId,
    Formulario.MecanicoId,
    Formulario.Fecha,
    Formulario.Token,
    Formulario.Lineas.Select(linea =>
        new LineaOrdenSolicitud(linea.ProductoId, linea.Cantidad)).ToArray());

ResultadoOrden resultado = _facade.CrearOrden(solicitud);
// Conservar el token y el formulario si hay errores o falla la respuesta.
// Usar resultado.OrdenId únicamente cuando resultado.Exito sea true.

ResultadoOrden anulacion = _facade.AnularOrden(ordenId);
```

`ResultadoOrden` devuelve `Exito`, `OrdenId`, `Total`, `Estado`, `EsReenvio` y `Errores`. Las claves de errores incluyen `VehiculoId`, `MecanicoId`, `Fecha`, `Token`, `Lineas`, `Lineas[i].Cantidad` y `Productos[id]`; la clave vacía representa un error general. La página debe trasladar esos errores a su formulario y mantener el borrador temporal.

Un Administrador o Recepcionista puede crear; únicamente un Administrador puede anular. El servicio vuelve a comprobar los permisos, aunque el consumidor omita la autorización de la página. `CreadoPor` y `AnuladoPor` proceden de `ICurrentUser`, implementado sobre la identidad autenticada por cookies; nunca se aceptan desde el formulario.

## Transacción de creación

1. Validar entrada y agrupar cantidades por producto. Comprobar vehículo existente con cliente y mecánico existente.
2. Abrir una `DbConnection` y una `DbTransaction` con aislamiento `ReadCommitted`. Las referencias de cabecera se consultan con `FOR SHARE` y los productos con `FOR UPDATE`, siempre por ID ascendente.
3. Recuperar una orden previa con el token, si existe. Comprobar nuevamente después de esperar los bloqueos de productos, antes de evaluar el stock restante.
4. Leer precios desde `Productos`, comprobar todas las cantidades y calcular subtotales y total con `decimal`. Las cantidades positivas y las claves foráneas también se respaldan en el esquema.
5. Insertar una cabecera con total cero. Por cada producto agrupado, insertar un detalle y descontar stock. Actualizar el total y confirmar.
6. Si una validación o una operación falla, liberar la unidad de trabajo revierte sus cambios. Las sobrecargas transaccionales de `ProductoRepository` utilizan la conexión recibida y no abren otra.

La segunda comprobación del token permite que un doble envío devuelva la misma orden incluso cuando el primero agotó el stock. Una restricción única en `Ordenes.Token` resuelve también la carrera entre solicitudes con el mismo token y productos distintos. Después de un conflicto de unicidad se revierte la transacción y se recupera la orden ya confirmada.

## Token y reintentos

`TokenOrdenSesion` genera un GUID asociado al usuario y a su sesión durante 30 minutos. Se conserva después de confirmar, para reconocer un doble clic o reintento. Otro usuario, un token desconocido o un formulario vencido se rechazan.

La clave única persistida evita depender exclusivamente de la memoria de la sesión. Un reenvío devuelve ID, total y estado de la orden original; si ya está anulada no vuelve a descontar stock. El consumidor debe presentar ese estado y no anunciar una creación nueva cuando `EsReenvio` sea true.

Si se pierde la respuesta durante `COMMIT`, el cliente puede desconocer si la base confirmó la operación. Debe reintentar con el mismo token o consultar la orden; generar inmediatamente otro token podría producir una operación nueva. El mensaje de error evita afirmar un rollback cuando no fue posible confirmar el resultado de la conexión.

## Anulación y conservación del historial

La anulación bloquea la orden mediante `SELECT … FOR UPDATE`. Si ya está anulada, devuelve ese error sin modificar stock. Bloquea sus productos por ID, registra estado, usuario y fecha de anulación y restituye las cantidades dentro de la misma transacción. Si falla una restitución, revierte tanto el stock como el estado y la auditoría.

Se conservan creador, fecha de creación, precios unitarios, subtotales y total. Las marcas temporales de creación y anulación se guardan en UTC; `Fecha` es la fecha de trabajo enviada por el formulario.

Las claves foráneas impiden eliminar vehículos, mecánicos o productos referenciados, incluso después de anular. Sus servicios y páginas muestran un mensaje comprensible. La eliminación de mecánicos devuelve ahora un mensaje nullable, siguiendo el mismo criterio del CRUD de clientes y productos. El trigger `TRG_Servicios_HistorialCosto` no se modifica.

## Esquema y verificación

`DatabaseInitializer` crea `Ordenes` y `DetalleOrdenes` de forma idempotente. `docs/migraciones/004_ordenes_transaccionales.sql` permite crear el mismo esquema manualmente después de las tablas relacionadas. Ambas tablas usan InnoDB. La actualización no elimina registros existentes.

Las pruebas comprueban:

- Dos unidades de A y tres de B: stock exacto, detalles, total y creador.
- Falta de stock o un fallo después de modificar existencias: cero nuevas órdenes/detalles y stock intacto, verificado con `SELECT`.
- Productos repetidos, cantidades inválidas, cliente pendiente y referencias inexistentes.
- Anulación, reanulación y rollback después de restituciones reales.
- Dos envíos simultáneos del mismo token, incluso con productos distintos, y dos órdenes que compiten por stock.
- Dos anulaciones simultáneas: una única restitución.
- Precios manipulados en JSON: el importe guardado procede de la base.
- Conservación de auditoría, restricciones de borrado, migración repetible y trigger de historial de servicios.

```powershell
# Configurar TALLER_TEST_MYSQL con un servidor MySQL de pruebas.
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~Ordenes'
```

Las pruebas MySQL crean y eliminan bases temporales `autotaller_test_<guid>`. Los fallos y la concurrencia se fuerzan mediante decoradores sobre repositorios reales; no se simula el comportamiento transaccional de MySQL.

## Resultado de la verificación del 10 de octubre de 2026

La compilación en .NET 10 terminó con cero advertencias y errores. La regresión seleccionada aprobó 97 pruebas: 44 de lógica existentes, 24 de vehículos y 29 de órdenes. Incluyó MySQL 8.4 y el recorrido HTTPS en Edge, con cero fallos y cero pruebas omitidas dentro de esa selección.

Se excluyeron expresamente las pruebas antiguas de MySQL que dependen de User Secrets y usuarios de una base específica, y el envío real de Gmail. Para repetir la misma selección, configurar `TALLER_TEST_MYSQL` y `PLAYWRIGHT_MODULE` y ejecutar:

```powershell
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter '(FullyQualifiedName!~MySqlTests&FullyQualifiedName!~EnvioGmailRealTests)|FullyQualifiedName~VehiculosMySqlTests|FullyQualifiedName~OrdenesMySqlTests'
```
