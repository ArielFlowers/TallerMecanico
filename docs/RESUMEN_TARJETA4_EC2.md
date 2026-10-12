# Resumen de la tarjeta 4 del Sprint 3

Este resumen explica lo implementado para estudiar el código y compartir el funcionamiento con el equipo. La tarjeta 4 implementa la creación y la anulación de órdenes de trabajo, asegurando que la orden, sus detalles y el stock se actualicen juntos. También incorpora control de permisos, auditoría y protección contra envíos duplicados. Todo está integrado en `main` mediante el PR #23.

Una orden relaciona un vehículo, su cliente, un mecánico y los productos utilizados durante el trabajo.

## Modelos y datos

Se añadieron dos modelos y sus tablas:

- **`Orden` / `Ordenes`:** guarda vehículo, mecánico, fecha del trabajo, estado `Activa` o `Anulada`, total, token, usuario creador y datos de anulación.
- **`DetalleOrden` / `DetalleOrdenes`:** guarda cada producto utilizado, cantidad, precio unitario y subtotal. Una orden puede tener varios detalles.

Los precios se guardan en los detalles para conservar el importe original aunque posteriormente cambie el precio del producto.

Las tablas usan InnoDB, claves foráneas y restricciones para cantidades positivas, precios válidos, subtotales coherentes y tokens únicos. `DatabaseInitializer` crea el esquema de forma idempotente. La migración `004_ordenes_transaccionales.sql` permite crear ese mismo esquema manualmente sin eliminar datos existentes.

## Organización del código

El funcionamiento está organizado en este flujo:

**Página → `OrdenServicioFacade` → `OrdenServicioService` → puertos → adaptadores MySQL.**

Las responsabilidades principales son:

- **`OrdenServicioFacade`:** es el punto de entrada para las páginas. Permite generar el token del formulario, crear una orden y anularla. Comprueba el token y delega la operación al servicio.
- **`OrdenServicioService`:** coordina permisos, comprobación de existencias, cálculo de importes y ejecución de la transacción.
- **`ValidacionOrdenes`:** valida los datos recibidos y agrupa las cantidades cuando un producto aparece varias veces.
- **`MySqlUnidadTrabajoOrden`:** proporciona los repositorios asociados a una misma conexión y una misma transacción. Confirma los cambios o los revierte.

## Creación de una orden

Al crear una orden, se sigue este proceso:

1. Se comprueba que el usuario pueda crear órdenes.
2. Se valida que existan vehículo, mecánico, fecha, token y al menos un producto con cantidad positiva.
3. Se agrupan los productos repetidos. Por ejemplo, dos líneas con 2 y 3 unidades del mismo producto se convierten en una línea de 5 unidades.
4. Se abre la transacción y se comprueba si ese token ya corresponde a una orden registrada.
5. Se verifica que el vehículo exista y tenga un cliente asignado, y que el mecánico exista.
6. Se bloquean los productos, se comprueba el stock y se leen sus precios desde la base de datos.
7. Se calcula cada subtotal y el total utilizando `decimal`.
8. Se inserta la cabecera inicialmente con total cero; después se insertan los detalles, se descuentan las cantidades y se actualiza el total.
9. Se ejecuta `COMMIT` cuando todas las operaciones terminan correctamente.

## Transacción y cálculo de precios

La transacción es la parte central de la tarjeta. Todos los repositorios participantes utilizan la misma `DbConnection` y la misma `DbTransaction`. Así, si falla una operación antes de confirmar, se ejecuta `ROLLBACK` y se revierten los cambios realizados.

Por ejemplo, si una orden necesita dos productos y falla el descuento del segundo, tampoco queda guardada la cabecera, el primer detalle ni el descuento del primer producto.

El formulario envía únicamente IDs, fecha, token y cantidades mediante `CrearOrdenSolicitud`. No envía precios ni total como datos confiables: el servidor los obtiene y calcula desde `Productos`. Esto evita que alguien altere el importe manipulando el formulario.

Para controlar solicitudes simultáneas se utiliza `SELECT … FOR UPDATE`. Este bloqueo hace que otra operación que necesite modificar el mismo producto espere hasta que termine la primera. Los productos se bloquean en orden ascendente de ID para reducir el riesgo de bloqueos cruzados.

## Protección contra envíos duplicados

Existe protección contra doble clic o reintentos, llamada **idempotencia**:

- `TokenOrdenSesion` genera un GUID asociado al usuario y a su sesión, válido durante 30 minutos.
- Ese token se guarda en la orden y tiene una restricción única en la base.
- Si se vuelve a enviar el mismo formulario, se devuelve la orden existente sin insertar otra ni descontar stock nuevamente.
- El token se comprueba otra vez después de esperar los bloqueos. Esto permite reconocer un reenvío incluso cuando la primera solicitud ya agotó el stock.
- Si dos solicitudes intentan insertar el mismo token simultáneamente, la restricción única impide el duplicado.

Ante una respuesta perdida durante la confirmación, se debe conservar el mismo token al reintentar. La respuesta informa si se está recuperando una orden existente y devuelve su estado; una orden ya anulada no vuelve a descontar stock por reenviar su formulario.

## Anulación lógica

La anulación es lógica: la orden y sus detalles permanecen guardados para conservar el historial.

Su funcionamiento es:

1. Comprobar que el usuario sea Administrador.
2. Bloquear la orden y consultar su estado.
3. Si ya está anulada, devolver ese mensaje sin modificar stock.
4. Consultar los detalles y bloquear sus productos por ID.
5. Marcar la orden como `Anulada`, registrar quién la anuló y cuándo, y restituir las cantidades.
6. Confirmar todo junto. Si falla una restitución antes de confirmar, revertir stock, estado y auditoría.

Por ejemplo, si la orden utilizó 2 unidades de A y 3 de B, anularla devuelve exactamente esas cantidades. Una segunda anulación no vuelve a sumarlas.

## Permisos, auditoría e historial

Los permisos implementados son:

- **Administrador:** puede crear y anular órdenes.
- **Recepcionista:** puede crear órdenes.
- **Usuario sin autorización:** no puede ejecutar estas operaciones.

Los permisos se comprueban también en el servicio. `CreadoPor` y `AnuladoPor` proceden de `ICurrentUser`, que representa la identidad autenticada; no se reciben del formulario. Las fechas de creación y anulación se guardan en UTC. La fecha del trabajo es la enviada en la solicitud.

Se protegió la conservación del historial mediante claves foráneas. Un vehículo, mecánico o producto asociado a órdenes no puede eliminarse físicamente, incluso si la orden está anulada. Sus pantallas muestran un mensaje comprensible. También se verificó que el trigger de historial de costos de servicios siga funcionando.

## Arquitectura y metodología

- **Facade:** ofrece a las páginas una entrada sencilla a las operaciones de órdenes.
- **Factory Method:** `CreadorOrden` y `CreadorDetalle` encapsulan la creación de sus repositorios y reciben la conexión y transacción compartidas. Heredan de `CreadorPuerto<TPuerto>`.
- **Ports and Adapters:** los contratos están en `Application/Ports` y las implementaciones MySQL en infraestructura.
- **Inyección de dependencias:** conecta servicios, puertos, validadores y fachada desde `Program.cs`.
- **SOLID:** se separan validación, coordinación y persistencia; el servicio depende de interfaces y los puertos ofrecen operaciones específicas para sus responsabilidades.
- **Clean Code:** se reutilizan reglas y operaciones, se devuelven errores identificables y se registran los fallos técnicos mediante el logger.

El servicio de órdenes no depende de Razor, de la sesión ni de clases de MySQL. Esos detalles se resuelven mediante los adaptadores de sus puertos. Las operaciones de órdenes se orientan a crear y anular; no se ofrece edición ni borrado mediante su puerto.

## Pruebas realizadas

Se incorporaron 29 pruebas específicas de órdenes, incluyendo creación, falta de stock, productos repetidos, rollback después de modificaciones reales, anulación, reanulación, envíos simultáneos, competencia por stock, precios manipulados y conservación del historial.

La regresión conjunta ejecutada el 12 de octubre de 2026 aprobó 129 pruebas sin fallos, incluyendo MySQL aislado y Edge. La selección excluyó el envío real de Gmail y las pruebas antiguas de MySQL que dependen de User Secrets y usuarios de una base específica.

## Integración para los compañeros

La tarjeta 3 debe consumir `OrdenServicioFacade`, generar el token al abrir un formulario nuevo y conservarlo cuando haya errores o reintentos. La página debe enviar vehículo, mecánico, fecha y una lista de productos con cantidades, y mostrar los errores devueltos.

Las operaciones devuelven `ResultadoOrden`, con éxito, ID, total, estado, indicador de reenvío y errores. La interfaz solo debe anunciar una operación confirmada cuando `Exito` sea verdadero.

La pantalla completa de registro pertenece a la tarjeta 3; el listado, detalle visual y comprobante pertenecen a la tarjeta 5. La tarjeta 4 deja preparados los casos de uso y sus contratos para conectar esas pantallas.

Para revisar los contratos y ejemplos de integración con más detalle, consultar [Tarjeta 4 de órdenes](TARJETA4_ORDENES_EC2.md).
