# Refactor de vehículos para EC2

El registro de vehículos distingue placas nacionales y extranjeras, exige un cliente registrado y presenta sus opciones en orden alfabético. Se conserva la arquitectura existente: Razor Pages → servicios → puertos → adaptadores MySQL, con validaciones en `Validators` y creación de repositorios mediante Factory Method.

## Placas y validación

`EsPlacaExtranjera` se guarda junto con el vehículo. La placa nacional admite 3 o 4 números seguidos de 3 letras (`123ABC`, `1234ABC`). Con el toggle activo, admite de 2 a 15 letras ASCII o números en cualquier orden (`AB123CD`, `123AB45`). No se solicita país y la placa continúa siendo única en todo el sistema.

`ValidacionVehiculos.Normalizar` elimina espacios y convierte la placa a mayúsculas; para placas extranjeras también elimina guiones. Después, `Validar` comprueba el formato correspondiente, marca y modelo, kilometraje, observaciones y cliente. El servicio verifica que el cliente exista y que la placa no esté duplicada antes de guardar. Una clave única y una clave foránea respaldan esas comprobaciones en la base.

El JavaScript adapta patrón, longitud y mensaje al cambiar el toggle. La validación del servidor sigue siendo obligatoria aunque se omita o manipule la del navegador. Crear y Editar reutilizan `_CamposVehiculo.cshtml`.

El layout carga jQuery antes de los scripts de validación: el recorrido de clientes reveló que faltaba esa dependencia y se corrigió para evitar errores en el navegador.

## Orden de los datos

El catálogo expone marcas y modelos ordenados con comparación cultural `es-BO`, sin distinguir mayúsculas. El catálogo JSON utiliza el mismo orden para los modelos cargados dinámicamente. Los vehículos se ordenan por placa; los clientes por primer apellido, segundo apellido, nombres y CI. El CI permite distinguir personas con nombres iguales.

## Cliente y borrador

La relación `Vehiculos.ClienteId` ya existía en `main`. Ahora es obligatoria al guardar desde el CRUD. Los registros históricos con `NULL` se conservan y se muestran sin cliente; al editarlos se debe asignar uno. La tarjeta 4 deberá rechazar órdenes para esos registros mientras no tengan cliente.

“Registrar cliente” envía los campos del vehículo a un borrador en sesión y abre el CRUD existente de clientes. No exige completar todavía el resto del vehículo. Al registrar un cliente, el repositorio recupera su ID y el CRUD vuelve al formulario con ese cliente seleccionado. “Cancelar y volver al vehículo” recupera el mismo borrador sin crear un cliente.

Cada borrador tiene un GUID, pertenece al usuario autenticado y vence a los 30 minutos. Conserva los campos, el modo Crear/Editar y la búsqueda. Se elimina después de guardar el vehículo. No se aceptan destinos de retorno externos. La sesión debe seguir activa durante el recorrido.

## Esquema y pruebas

`DatabaseInitializer` amplía `Placa` a 15 caracteres y añade `EsPlacaExtranjera` con valor inicial falso, sin eliminar registros ni asignar clientes ficticios. `docs/migraciones/003_vehiculos_placas_extranjeras.sql` permite aplicar los mismos cambios manualmente y es idempotente.

Las pruebas xUnit cubren formatos, normalización, unicidad entre ambos tipos de placa, cliente obligatorio, cliente inexistente, orden del catálogo, borradores y retorno con el ID generado. Las pruebas MySQL crean y eliminan exclusivamente una base temporal con nombre `autotaller_test_<guid>`.

```powershell
dotnet build TallerMecanico.csproj
# Configurar TALLER_TEST_MYSQL de forma local con un servidor de pruebas
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~Vehiculos'
```

Sin `TALLER_TEST_MYSQL`, las pruebas MySQL se informan como omitidas. Los scripts históricos de SQLite no validan la aplicación MySQL actual y no deben ejecutarse sobre la base del taller.
