# Instalacion de autenticacion - AutoTaller Pro

## Requisitos

- .NET 10
- MySQL 8
- Base de datos `tallermecanico`
- Cadena de conexion MySQL configurada de forma segura mediante User Secrets o variables de entorno

## Base de datos nueva

1. Crear la base de datos `tallermecanico`.
2. Configurar la cadena de conexion MySQL.
3. Iniciar la aplicacion para ejecutar `DatabaseInitializer.Initialize()`.
4. Detener la aplicacion.
5. Ejecutar, en este orden:
   - `001_registro_usuarios.sql`
   - `002_control_reenvio_verificacion.sql`
6. Reiniciar la aplicacion y comprobar el inicio de sesion y el registro.

IMPORTANTE: El inicializador crea la estructura basica de `Usuarios`.
Las migraciones agregan los campos de correo, las solicitudes de
registro y el control del reenvio.

Los scripts actuales no son idempotentes: deben aplicarse una sola vez.

La migración `003_vehiculos_placas_extranjeras.sql` es idempotente: amplía
las placas y añade el toggle persistido. `DatabaseInitializer` también
aplica estos cambios automáticamente al iniciar la aplicación.

La migración `004_ordenes_transaccionales.sql` crea `Ordenes` y
`DetalleOrdenes` sin eliminar datos y se puede ejecutar nuevamente.
Requiere las tablas de clientes, vehículos, mecánicos y productos.
`DatabaseInitializer` también crea estas tablas automáticamente.

## Base de datos existente

Si ya se aplicaron las migraciones 001 y 002, no ejecutarlas nuevamente.

No eliminar ni recrear las tablas existentes para actualizar
el sistema.

## Cuentas iniciales

La creacion de cuentas iniciales y el restablecimiento administrativo
se realizan mediante los comandos administrativos del proyecto.
No almacenar contrasenas en el repositorio.

## Pruebas

Ejecutar:

dotnet build TallerMecanico.csproj

dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj

Algunas pruebas de integracion pueden requerir una base de datos
de pruebas configurada y servicios externos disponibles.
