# Incorporación controlada del catálogo de Nueva Orden

`CatalogoServicios` define los diez códigos estables, nombres, precios iniciales
y unidades. Cambio de aceite es único. Solo filtros, pastillas, bujías y suspensión
permiten cantidades. Los IDs numéricos no tienen significado de negocio.

## Esquema e incorporación son pasos diferentes

`DatabaseInitializer.Initialize()` aplica la migración aditiva 008: columna
`CodigoCatalogo` opcional e índice único. No carga servicios ni modifica precios.
El script 008 es la alternativa manual idempotente para ese mismo esquema.
Los registros antiguos conservan su Id y reciben un código nulo.

La carga es explícita mediante `InicializadorCatalogoServicios`, con la misma
`DatabaseConnectionFactory` de la base que se vaya a incorporar. **No ejecutar
sobre la base habitual sin revisar previamente el destino y los datos.**

1. Ejecutar `Analizar()`, que solo consulta. El informe muestra Id, código, nombre,
   descripción, precio, tiempo, unidad y número de órdenes relacionadas. También
   informa coincidencias ambiguas, nombres similares y datos faltantes.
2. Revisar los diez servicios. Se reutiliza una coincidencia exacta de nombre
   ignorando mayúsculas, pero no se fusionan registros parecidos ni duplicados.
3. Proporcionar a `Incorporar(datosConfirmados)` una lista de `Servicio` con
   `CodigoCatalogo`, `Descripcion` y `TiempoEstimadoHoras` aprobados para cada
   registro nuevo. Su `Id` debe ser cero. No se usan precios ni unidades enviados
   en esa lista: la definición fija sus valores iniciales.
4. Para resolver explícitamente una ambigüedad, proporcionar el código y el Id
   del registro existente elegido. Se conservan su descripción y tiempo válidos.
5. La lista de mensajes devuelta debe estar vacía. Cualquier pendiente impide
   **toda** la carga; no queda un catálogo parcialmente incorporado por ese intento.

No se inventan descripciones ni tiempos. Sin esos datos puede prepararse y
auditarse el mecanismo, pero una base vacía no tendrá todavía diez opciones.
La selección de Nueva Orden queda limitada a los códigos efectivamente
incorporados y a su unidad correcta; nunca vuelve al catálogo antiguo como fallback.

## Conservación y repetición

La carga adopta los IDs inequívocos, asigna el nombre canónico, precio inicial y
unidad, o inserta solo los servicios que faltan. Es transaccional y debe ejecutarse
como operación administrativa única, no desde cada réplica al arrancar.
El índice único impide asignar el mismo código a dos registros.

Repetir la carga no reinicia precios de códigos ya incorporados ni genera
historial de costos adicional si no cambia el costo. El trigger existente registra
los cambios iniciales de precios al adoptar un servicio antiguo.

No se eliminan servicios fuera del catálogo ni se actualizan órdenes o detalles.
Las consultas históricas siguen usando sus snapshots. Los precios autorizados
pueden editarse desde Gestión de Servicios; el código se conserva y la unidad
del catálogo no puede cambiarse. Tampoco puede eliminarse un servicio del catálogo.
La administración de los demás servicios mantiene su comportamiento.

## Verificación aislada

Las pruebas `CatalogoServiciosMySqlTests` y `CatalogoServiciosHttpTests` usan
`MySqlAisladoFixture` y bases descartables `autotaller_test_*`. Sus descripciones
y tiempos son exclusivamente datos ficticios de prueba, no propuestas para producción.
Se mantienen las políticas existentes: `SoloAdministrador` en Servicios y
`PersonalAutorizado` en Nueva Orden.
