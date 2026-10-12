# Instalación de autenticación - AutoTaller Pro

## Requisitos

- .NET 10 y MySQL 8.
- Una base de datos previamente creada. El nombre de referencia del proyecto es `taller_mecanico`.
- `ConnectionStrings:MySqlConnection`, suministrada mediante User Secrets o variables de entorno.
- Permisos para consultar el esquema y crear tablas, agregar columnas e índices. El arranque completo también conserva la creación de estructuras y triggers de los otros módulos.
- Para probar el registro y el correo: `App:PublicBaseUrl` y `Email:Smtp:Host`, `Port`, `Username`, `Password` y `From`. El adaptador actual exige Gmail en el puerto 465; usar HTTPS para la aplicación local por la configuración de sus cookies.

El nombre efectivo de la base siempre es el indicado en la cadena de conexión. El inicializador consulta `DATABASE()` y no contiene instrucciones `CREATE DATABASE` ni `USE` con un nombre fijo. Una instalación existente llamada `tallermecanico` puede conservar ese nombre si su conexión lo indica; no debe renombrarse por esta actualización.

## Base de datos nueva

1. Crear la base y configurar la conexión al destino correcto.
2. Iniciar una sola instancia de la aplicación para ejecutar `DatabaseInitializer.Initialize()`.
3. Comprobar que se crearon las tablas de autenticación:
   - `Usuarios`, con `Email`, `EmailVerificado` e índice único sobre `Email`.
   - `SolicitudesRegistro`, con los cuatro estados, índices y `UltimoIntentoVerificacionEn`.
   - `Auditoria`.
4. Preparar las cuentas iniciales mediante el comando administrativo del proyecto, después de que exista el esquema.

No ejecutar los scripts 001 y 002 para instalar la versión actual: el inicializador ya incorpora sus estructuras.

La migración `003_vehiculos_placas_extranjeras.sql` es idempotente: amplía
las placas y añade el toggle persistido. `DatabaseInitializer` también
aplica estos cambios automáticamente al iniciar la aplicación.

La migración `004_ordenes_transaccionales.sql` crea `Ordenes` y
`DetalleOrdenes` sin eliminar datos y se puede ejecutar nuevamente.
Requiere las tablas de clientes, vehículos, mecánicos y productos.
`DatabaseInitializer` también crea estas tablas automáticamente.

## Base de datos existente

El inicializador comprueba las columnas y el índice único antes de agregarlos. Conserva tablas, registros, contraseñas, correos, roles y marcas de verificación existentes.

| Estado anterior | Comportamiento de la versión actual |
|---|---|
| `Usuarios` básico, sin migraciones de autenticación | Agrega las dos columnas de correo y su índice único; crea `SolicitudesRegistro` completa. |
| Migración 001 aplicada | Conserva el esquema e incorpora `UltimoIntentoVerificacionEn` si falta. |
| Migraciones 001 y 002 aplicadas | Conserva las estructuras y los datos sin volver a ejecutar los scripts. |
| Columnas de correo agregadas parcialmente | Agrega únicamente las columnas que faltan y comprueba el índice único. |
| Índice único completo de `Email` con otro nombre | Reutiliza su garantía de unicidad sin agregar un índice equivalente. |

En cuentas antiguas, una columna `Email` nueva admite `NULL`; una columna `EmailVerificado` nueva tiene valor predeterminado `FALSE`. Esto no confirma correos automáticamente ni cambia una columna de verificación que ya exista. Las solicitudes antiguas mantienen `NULL` en la nueva columna de control del reenvío.

Si hay correos no nulos duplicados y falta el índice único, el inicializador se detiene con un mensaje explícito. No elimina, combina ni corrige cuentas. La decisión sobre esos datos requiere revisión manual.

Las comprobaciones cubren el esquema básico y las versiones definidas por los scripts 001 y 002. No corrigen automáticamente tipos de columna, índices incompatibles ni tablas modificadas manualmente fuera de esas versiones. Ejecutar la actualización con una sola instancia: las comprobaciones y las instrucciones DDL no coordinan inicializadores concurrentes. Una interrupción puede dejar algunos cambios de esquema completados; el siguiente arranque vuelve a comprobar qué falta.

## Scripts históricos

`001_registro_usuarios.sql` y `002_control_reenvio_verificacion.sql` se conservan como referencia del esquema y del procedimiento de versiones anteriores. Su SQL sigue siendo no idempotente. No ejecutarlos después de iniciar la versión actual ni repetirlos cuando sus estructuras ya existan.

## Cuentas iniciales

La creación de cuentas iniciales y el restablecimiento administrativo conservan los comandos existentes. No almacenar contraseñas reales en el repositorio.

## Verificación en una base aislada

La compilación no inicia la aplicación ni ejecuta su inicializador:

```powershell
dotnet build TallerMecanico.csproj --no-restore
```

Para validar posteriormente el comportamiento de MySQL:

1. Usar una base descartable y confirmar la conexión efectiva antes de iniciar la aplicación. Un clon no aísla MySQL ni cambia el `UserSecretsId` del proyecto.
2. Preparar, por separado, los casos de base nueva, esquema básico, migración 001 y migraciones 001 + 002. Los scripts históricos solo se utilizan para construir esas fixtures antiguas, antes de iniciar la versión actual.
3. Conservar una copia de la estructura y de los registros de cada fixture antes del arranque.
4. Iniciar una sola instancia, verificar columnas e índices y comparar los registros existentes.
5. Reiniciar y comprobar que no se duplican estructuras ni cambian los datos.
6. Probar una actualización parcial, un índice equivalente con otro nombre y el rechazo seguro de correos duplicados.
7. En esa misma base aislada, comprobar creación administrativa, registro, confirmación de correo, aprobación y login con SMTP preparado.

No ejecutar la suite MySQL existente sin revisar su conexión: algunas pruebas usan los User Secrets de la aplicación y escriben o eliminan sus registros temporales.
