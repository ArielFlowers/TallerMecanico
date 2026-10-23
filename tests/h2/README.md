# H2: entorno aislado de integración

Esta configuración prepara pruebas reales de la implementación existente.
No constituye evidencia de ejecución: PING, MISS, HIT y los conteos deben
comprobarse después de obtener autorización para iniciar el entorno.

## Aislamiento y límites

- Compose independiente: proyecto `autotaller-h2-integracion`, sin `container_name`.
- Reutiliza el Dockerfile de la raíz, con una etiqueta de imagen exclusiva de H2.
- MySQL solo utiliza `autotaller_h2_integracion`, usuario `h2_app` y almacenamiento
  en memoria (`tmpfs`). No monta volúmenes existentes ni publica su puerto.
- Redis es real, sin persistencia ni puerto publicado. Conserva la configuración
  de memoria del entorno habitual; no modifica la lógica de caché.
  La imagen oficial crea un volumen anónimo nuevo en `/data`, aunque RDB y AOF
  están deshabilitados. No se reutilizó ni eliminó ningún volumen existente.
- Las tres réplicas comparten `redis:6379`, TTL de 60 segundos y el volumen
  `autotaller-h2-integracion_h2-dpkeys`, exclusivo de estas pruebas.
- MySQL y Redis permanecen en una red interna exclusiva. Las apps y Nginx usan
  además una red bridge exclusiva para publicar HTTP: Docker no publicó puertos
  al conectarlos únicamente a la red interna. Ningún servicio se conecta a redes
  del entorno habitual. La red HTTP permite salida exterior; no probar correo.
- App1, app2, app3 y Nginx publican puertos dinámicos únicamente en localhost.
- Nginx reutiliza el esquema de balanceo existente, solo por HTTP en Development.
  No requiere certificados locales ni cambia las políticas de autenticación.
- `Program.cs` confía en una subred fija para Forwarded Headers. Esta red puede
  recibir otra subred: no se presupone confianza en sus cabeceras reenviadas ni
  se prueba HTTPS o IP original del cliente en esta iteración.
- MySQL registra consultas en `mysql.general_log`, solo dentro de este contenedor.
  Consultar únicamente los conteos filtrados: no publicar logs completos, variables
  de entorno, cadenas de conexión ni credenciales.
- El inicializador normal creará el esquema existente únicamente en la base
  aislada. No se han modificado scripts, migraciones ni código de inicialización.
- No se copian datos ni cuentas del entorno habitual. No hay usuarios precreados.

MySQL pierde sus datos al detener/recrear su contenedor; las claves de Data
Protection permanecen en el volumen exclusivo. No usar este entorno para datos
que se deban conservar. No ejecutar `down -v`, `FLUSHALL` ni `FLUSHDB`.

## 1. Requisitos y comprobaciones sin arrancar servicios

Abrir Docker Desktop desde Inicio, seleccionar contenedores Linux si corresponde
y esperar hasta que indique que el motor está ejecutándose. No cambiar contextos
automáticamente ni iniciar/detener infraestructura habitual.

Desde la raíz del repositorio:

```powershell
docker context show
docker version
docker compose version
docker ps --all
docker network ls
docker volume ls
docker ps --all --filter label=com.docker.compose.project=autotaller-h2-integracion
docker network ls --filter label=com.docker.compose.project=autotaller-h2-integracion
docker volume ls --filter label=com.docker.compose.project=autotaller-h2-integracion
```

Si aparece cualquier recurso de ese proyecto, detenerse y revisar su propietario
antes de reutilizarlo; no sobrescribirlo ni eliminarlo. Los puertos dinámicos
evitan reservar los puertos fijos del Compose habitual.

Definir contraseñas aleatorias solo para pruebas en la sesión actual, sin imprimirlas
ni sobrescribir variables ya definidas. Estos comandos requieren PowerShell con .NET
moderno (por ejemplo `pwsh`):

```powershell
if (Test-Path Env:H2_MYSQL_PASSWORD) { throw 'H2_MYSQL_PASSWORD ya existe; revisar antes de continuar.' }
if (Test-Path Env:H2_MYSQL_ROOT_PASSWORD) { throw 'H2_MYSQL_ROOT_PASSWORD ya existe; revisar antes de continuar.' }
$env:H2_MYSQL_PASSWORD = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:H2_MYSQL_ROOT_PASSWORD = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
docker compose -f tests/h2/docker-compose.yml --profile replicas config --quiet
```

No ejecutar `config` sin `--quiet`: mostraría las contraseñas interpoladas.
Alternativamente, usar el ejemplo `.env.h2.example` para un archivo privado fuera
del repositorio o `tests/h2/local.env` (ignorado por la regla existente `*.env`) y
pasarlo con `--env-file`; no crear ni sobrescribir el `.env` habitual.
Las credenciales seguirán siendo visibles para quien administre Docker; no son
credenciales de producción.

## 2. Inicio: requiere autorización expresa

No ejecutar esta sección hasta obtener autorización. Arrancar primero una sola
réplica evita que tres inicializadores compitan por el esquema nuevo:

```powershell
docker compose -f tests/h2/docker-compose.yml up -d --build mysql redis app1
docker compose -f tests/h2/docker-compose.yml ps
$direccionA = (docker compose -f tests/h2/docker-compose.yml port app1 8080).Trim()
Invoke-RestMethod "http://$direccionA/h2/redis"
```

Esperar a que app1 responda (puede tardar por la creación inicial del esquema).
Si falla, revisar el error y no continuar ni modificar código automáticamente.
Después:

```powershell
docker compose -f tests/h2/docker-compose.yml --profile replicas up -d --build app2 app3 nginx
$direccionB = (docker compose -f tests/h2/docker-compose.yml port app2 8080).Trim()
$direccionC = (docker compose -f tests/h2/docker-compose.yml port app3 8080).Trim()
$direccionNginx = (docker compose -f tests/h2/docker-compose.yml port nginx 80).Trim()
docker compose -f tests/h2/docker-compose.yml exec -T redis redis-cli PING
Invoke-RestMethod "http://$direccionA/h2/redis"
Invoke-RestMethod "http://$direccionB/h2/redis"
Invoke-RestMethod "http://$direccionC/h2/redis"
Invoke-RestMethod "http://$direccionNginx/h2/redis"
docker compose -f tests/h2/docker-compose.yml exec -T redis redis-cli --scan --pattern 'autotaller:dashboard:*'
```

Exigir `PONG` real y registrar los identificadores devueltos por las tres réplicas.
Los diagnósticos por sí solos no demuestran que compartan el resumen cacheado.

## 3. Cuenta aislada: requisito antes de MISS/HIT

Si no se autorizó crear cuentas de prueba, detener aquí las pruebas autenticadas.
No usar cuentas reales ni desactivar la autenticación. Con autorización específica,
la aplicación ofrece el comando interactivo existente:

```powershell
docker compose -f tests/h2/docker-compose.yml exec app1 dotnet TallerMecanico.dll --inicializar-usuarios
```

Confirmar únicamente después de comprobar el proyecto y la base aislados. El comando
solicita dos usuarios (Administrador y Recepcionista) y contraseñas sin mostrarlas;
usar nombres y contraseñas exclusivos de pruebas. No se ejecuta automáticamente.

## 4. Evidencia real MISS en A / HIT en B

1. Iniciar sesión en `http://<direccionA>/Auth/Login` con una cuenta aislada autorizada.
   El inicio de sesión puede redirigir al dashboard y crear la entrada. Esperar su
   expiración natural antes de medir; no limpiar toda la instancia Redis.
2. Confirmar `EXISTS` = 0 y `TTL` = -2 para la clave exacta:

   ```powershell
   docker compose -f tests/h2/docker-compose.yml exec -T redis redis-cli EXISTS autotaller:dashboard:resumen
   docker compose -f tests/h2/docker-compose.yml exec -T redis redis-cli TTL autotaller:dashboard:resumen
   ```

3. Obtener una línea base de los conteos MySQL con la consulta siguiente. No debe
   haber otros navegadores/pestañas haciendo solicitudes al dashboard.
4. Abrir `http://<direccionA>/` una sola vez. Debe mostrar MISS y réplica A como
   origen y actual. Consultar de nuevo los conteos: cada tabla debe aumentar en 1.
5. Consultar `GET` y `TTL` y guardar el JSON y la expiración observados:

   ```powershell
   docker compose -f tests/h2/docker-compose.yml exec -T redis redis-cli GET autotaller:dashboard:resumen
   docker compose -f tests/h2/docker-compose.yml exec -T redis redis-cli TTL autotaller:dashboard:resumen
   ```

6. Antes de 60 segundos, abrir `http://<direccionB>/` en el mismo navegador,
   usando exactamente `127.0.0.1` en ambas direcciones. Las cookies no dependen del
   puerto; las claves compartidas permiten autenticar en B. No es necesario otro login.
   Debe mostrar HIT, origen A y actual B; el JSON debe conservar origen y generación.
7. Repetir la consulta de conteos: no debe aumentar ninguno de los tres. Comprobar
   TTL decreciente y logs `MISS -> MySQL -> Redis` / `HIT -> Redis` de A y B.

Si la secuencia tarda más del TTL o aparece otra solicitud al dashboard, la medición
no es concluyente: repetir desde la expiración natural. Cero registros en la base
es válido para esta prueba: se verifica compartir la entrada y ejecutar/evitar SQL,
no datos ficticios ni resultados esperados sin observación.

### Conteos observados, no inferidos

Ejecutar este bloque en cada punto (línea base, después de MISS, después de HIT).
Solo devuelve tabla y número de ejecuciones; no muestra otras consultas o contraseñas:

```powershell
$consultaConteos = @'
SELECT
    CASE
        WHEN LOWER(CONVERT(argument USING utf8mb4)) LIKE '%mecanicos%' THEN 'Mecanicos'
        WHEN LOWER(CONVERT(argument USING utf8mb4)) LIKE '%vehiculos%' THEN 'Vehiculos'
        ELSE 'Servicios'
    END AS tabla,
    COUNT(*) AS ejecuciones
FROM mysql.general_log
WHERE user_host LIKE 'h2_app%'
  AND command_type IN ('Query', 'Execute')
  AND LOWER(TRIM(CONVERT(argument USING utf8mb4))) REGEXP
      '^select[[:space:]]+count[(][*][)][[:space:]]+from[[:space:]]+(mecanicos|vehiculos|servicios)[[:space:]]*;?$'
GROUP BY tabla
ORDER BY tabla;
'@
$consultaConteos | docker compose -f tests/h2/docker-compose.yml exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql -uroot --batch'
```

La ausencia de una fila significa cero ejecuciones observadas para esa tabla.
`mysql.general_log` incluye SQL de inicialización: por eso se toma una línea base
después del arranque. El filtro excluye los `SELECT` de `Usuarios` que realiza el
middleware de autenticación: esos pueden ocurrir incluso en HIT.

`argument` es binario en `mysql.general_log`; convertirlo a texto antes de aplicar
`LOWER` es necesario para que el filtro detecte también el SQL escrito en mayúsculas.

El contenedor MySQL registra consultas solo durante esta prueba; no activar este
registro en el MySQL habitual ni consultar/imprimir el log completo.

## 5. Criterio de cierre

Registrar por separado: diagnóstico real por réplica, valor/TTL de la clave, origen
y réplica actual del MISS/HIT, tres incrementos tras MISS y cero tras HIT. No dar
por aprobada esta integración solo porque pasen las pruebas con dobles en memoria.

No detener servicios existentes ni eliminar volúmenes. Cualquier detención del
entorno aislado debe autorizarse sabiendo que MySQL perderá sus datos temporales.

## 6. Prueba automatizada con infraestructura real

`redis_real.cjs` usa peticiones HTTP de Playwright (sin simular Redis o MySQL).
Requiere los seis servicios ya iniciados y una cuenta aislada válida. Usa siempre
`tests/h2/local.env` para Compose y para leer `H2_TEST_USERNAME` y `H2_TEST_PASSWORD`;
no imprime credenciales ni cookies. Esas dos claves adicionales son solo para las
pruebas, no cambian la configuración de autenticación de las apps.

El auxiliar `ProvisionarUsuario` reutiliza `InicializadorUsuarios`,
`UsuarioRepository` y `Argon2idPasswordHasher`. Se publica fuera de la imagen de
producción y se copia únicamente a `/tmp/h2-provision` de app1. Lee usuario y
contraseña por stdin; rechaza otra base, servidor, entorno o nombre de usuario,
y nunca sobrescribe una cuenta existente que no coincida. No usa el fixture
general de MySQL, que crea otras bases distintas.

Con la cuenta ya provisionada, configurar `PLAYWRIGHT_MODULE` con la ruta local
del módulo Playwright instalado y ejecutar desde la raíz:

```powershell
node tests/h2/redis_real.cjs
```

La prueba no sigue la redirección del login al dashboard: mide la primera visita
autenticada explícitamente. Si la clave ya existe, espera su expiración natural.
Comprueba los tres clientes en Redis, diagnósticos PONG, balanceo Nginx, MISS A,
HIT B, identidad de la entrada, TTL decreciente y diferencias exactas en los conteos.
El resultado observado de esta iteración está en `resultado-real.json`.

Las tablas de Mecánicos, Vehículos y Servicios están vacías en este entorno:
el resumen devuelve ceros reales y el general_log demuestra las consultas físicas.
No se insertaron datos de otros CRUD para esta comprobación.

## Iteración 2: estados y contenido recuperado

La lectura distingue `Hit`, `Miss`, `Error` y `Deshabilitado`. El coordinador acepta
HIT únicamente tras validar estructura, los tres conteos enteros no negativos,
réplica y fecha de generación. Un valor presente pero inválido se identifica como
`EntradaInvalida`, no como MISS. El formato JSON se conserva.

El resultado del dashboard conserva el estado de lectura independientemente de
`Almacenado`: true indica SET exitoso, false fallo y null escritura no intentada.
Los errores de GET y SET se reproducen con dobles en las pruebas .NET, sin detener
Redis ni afectar sesiones. La regresión real ampliada se ejecuta así:

```powershell
node tests/h2/redis_real.cjs --validar-datos --esperar-expiracion
```

Esta opción escribe datos inválidos exclusivamente en la clave del Redis H2,
verifica el indicador Razor y los tres COUNT de recuperación, y comprueba un HIT
posterior desde B sin repetir conteos. Finalmente espera la expiración natural,
sin DEL, FLUSHALL ni FLUSHDB. Las aserciones del indicador decodifican entidades
HTML para no confundir la codificación de Razor con el texto visible ni volcar
formularios con tokens en mensajes de error.

La evidencia nueva se guarda en `resultado-iteracion2.json`; `resultado-real.json`
conserva la medición de la Iteración 1. Invalidación, concurrencia, sesiones,
Data Protection, configuración Docker y esquema MySQL no se refactorizan aquí.

## Iteración 3: invalidación automática y manual

`ResultadoEliminacionCache` distingue Eliminada, Inexistente, NoDisponible, Error
y Deshabilitado. El adaptador registra cada DEL, incluso cuando el CRUD no utiliza
su retorno: una desconexión o excepción genera advertencia sobre datos anteriores
hasta su expiración. No se revierte una escritura de negocio ya confirmada.

Los puntos automáticos existentes se conservan, después de Add/Delete exitosos:
MecanicoService, VehiculoService y ServicioService. No se modificaron esos servicios
ni repositorios: las validaciones y comprobaciones de inexistencia preceden a DEL,
y las actualizaciones de nombres, placas o precios no invalidan los conteos.
La búsqueda de INSERT/DELETE y cascadas no encontró otra operación que cambie
estos tres totales; la cascada de Ordenes solo afecta DetalleOrdenes.

La acción manual sigue siendo el POST `/?handler=InvalidarCache`, con la comprobación
de Administrador y antiforgery existentes. Su mensaje diferencia los resultados y
explica que seguir la redirección puede regenerar el resumen. Que la clave reaparezca
tras ese GET no implica un fallo de DEL. Otro consumidor también podría regenerarla
antes de que llegue el GET: no se promete un MISS incondicional en el mensaje.

Para las pruebas reales se agregó la cuenta aislada `h2_administrador`. Sus claves
`H2_ADMIN_USERNAME` y `H2_ADMIN_PASSWORD` se guardan únicamente en `local.env`,
ignorado por Git. El auxiliar ProvisionarUsuario permite solo esa cuenta y
`h2_recepcionista`, comprueba la conexión H2 y no sobrescribe cuentas incompatibles.

Con las cuentas ya provisionadas y PLAYWRIGHT_MODULE configurado:

```powershell
node tests/h2/invalidacion_real.cjs
node tests/h2/redis_real.cjs --esperar-expiracion
```

La primera prueba utiliza formularios HTTP, validaciones y antiforgery reales para
crear, editar y eliminar registros de los tres módulos. Solo inserta un cliente
fixture en H2 para respetar la relación obligatoria del vehículo; lo conserva para
auditoría. Solo elimina los registros de prueba que ella misma creó. También se
conserva el historial de precio generado por el trigger existente.

Antes de cada cambio exige HIT y margen de TTL, comprueba ausencia de la clave
antes de seguir la redirección y compara el resumen con MySQL. Cada MISS debe
incrementar una vez cada COUNT; el HIT posterior desde otra réplica no los repite.
Las ediciones, validaciones fallidas, CI duplicado y eliminaciones inexistentes
conservan entrada y TTL. Un canario con TTL y las sesiones existentes permiten
comprobar que DEL no vacía Redis. No se registran valores ni nombres de sesiones.

La invalidación manual se verifica tanto con clave presente como ya inexistente.
Un POST sin antiforgery debe devolver 400. Recepcionista recibe la redirección de
Forbid a AccessDenied y no ejecuta DEL. No se detiene Redis para simular errores:
timeouts, desconexiones y fallos posteriores a escrituras confirmadas se verifican
mediante dobles en las pruebas .NET.

### Diagnóstico de concurrencia al cierre de la Iteración 3

La prueba `Concurrencia_MissPuedePublicarConteoAnteriorTrasInvalidacion` reproduce
determinísticamente, con dobles, este intercalado del coordinador real: A obtiene
el conteo anterior durante MISS; B modifica el conteo e invalida; A guarda después
su resultado anterior; el siguiente HIT queda desactualizado. No se presenta esta
prueba como una reproducción con dos contenedores reales.

Una prueba distribuida futura puede usar barreras en un adaptador exclusivo de
pruebas para pausar A antes de SET, efectuar la escritura e invalidación desde B
y reanudar A. La corrección mínima propuesta es una generación compartida en Redis:
invalidar incrementa la generación y elimina el resumen atómicamente; publicar
desde MISS compara la generación previa a los COUNT y hace SET solo si no cambió,
también atómicamente (por ejemplo mediante Lua). Un lock local no coordina réplicas.
No se implementaron versiones, locks ni reintentos en esta iteración.

Existe además una ventana GetById/Delete: los contratos actuales Delete devuelven
void, por lo que no informan filas afectadas si otro actor borró entre ambas llamadas.
Los no-op secuenciales están cubiertos; esta carrera no se corrige ampliando los
contratos CRUD en esta iteración de análisis de concurrencia.

## Iteración 4: publicación condicional entre réplicas

El punto vulnerable era el SET incondicional posterior a los tres COUNT: un DEL
intermedio no impedía que una consulta anterior repusiera el resumen antiguo.
Ahora `ICacheResumenDashboardPort` separa esta coordinación del contrato genérico
`ICacheDistribuidaPort`, que no se modificó. El mismo adaptador y multiplexer
implementan ambos puertos; sesiones y Data Protection conservan su configuración.

Tres scripts Lua pequeños realizan operaciones indivisibles en el Redis compartido:

1. Leer generación y resumen juntos. Si falta la generación, crear un identificador
   aleatorio nuevo y descartar un posible resumen huérfano, dentro del mismo script.
2. Publicar solo si la generación y el contenido capturados no han cambiado.
   La comparación del contenido evita que dos MISS (o reparaciones de JSON inválido)
   sobrescriban al primer publicador y renueven innecesariamente su TTL.
3. Invalidar cambiando la generación por otro identificador y eliminando el resumen.
   Incluso si DEL devuelve cero, se cambia la generación para rechazar escritores
   que ya estaban en curso. Se conservan los cinco resultados de invalidación.

La clave del resumen sigue siendo `autotaller:dashboard:resumen`, con TTL absoluto
configurable (60 segundos, aplicado con PX). Los HIT no escriben ni renuevan el TTL.
`autotaller:dashboard:generacion` es metadato sin expiración (TTL=-1); no es una sesión.
No utiliza un contador que vuelva a cero: identificadores nuevos evitan reutilizar
versiones cuando se pierde/expulsa el metadato o Redis reinicia. Un publicador nunca
crea la generación ausente. La prueba de pérdida borra solamente este metadato H2;
no reinicia Redis ni borra sesiones para simularla.

El servicio diferencia Publicada, RechazadaPorGeneracion, EntradaExistente y errores.
Relee caché/MySQL como máximo una vez ante concurrencia. Si vuelve a cambiar, devuelve
los últimos datos consultados sin publicarlos; el panel explica el descarte, no lo
confunde con desconexión. Una lectura de generación fallida nunca habilita SET sin
protección. Se preservan HIT/MISS/EntradaInvalida/NoDisponible/Deshabilitado y el JSON.

La adaptación mínima de MecanicoService, VehiculoService y ServicioService cambia
solo el puerto inyectado y las llamadas posteriores a Add/Delete por InvalidarResumen.
Actualizar sigue sin invalidar conteos. La acción manual sigue protegida por el rol
Administrador y antiforgery. No cambian SQL, transacciones ni esquema MySQL.

### Prueba distribuida reproducible

Compilar el auxiliar (sin paquetes nuevos), copiarlo exclusivamente a /tmp de las
réplicas H2 ya desplegadas con la imagen de esta iteración, y ejecutar:

```powershell
dotnet restore tests/h2/ConcurrenciaDashboard/ConcurrenciaDashboard.csproj
dotnet publish tests/h2/ConcurrenciaDashboard/ConcurrenciaDashboard.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false -o tests/h2/ConcurrenciaDashboard/bin/publicado
docker compose --env-file tests/h2/local.env -f tests/h2/docker-compose.yml cp tests/h2/ConcurrenciaDashboard/bin/publicado/. app1:/tmp/h2-concurrencia
docker compose --env-file tests/h2/local.env -f tests/h2/docker-compose.yml cp tests/h2/ConcurrenciaDashboard/bin/publicado/. app2:/tmp/h2-concurrencia
node tests/h2/concurrencia_real.cjs
node tests/h2/invalidacion_real.cjs
node tests/h2/redis_real.cjs --validar-datos --esperar-expiracion
```

El auxiliar valida servidor, usuario, base H2, Redis y entorno antes de operar.
Ejecuta DashboardCacheService, repositorios ADO.NET y RedisCacheAdapter reales en
procesos .NET distintos dentro de app1/app2. Un decorador exclusivo de pruebas pausa
la primera publicación y espera una señal por stdin. No agrega rutas de depuración
a la aplicación. El anfitrión realiza un INSERT o invalidación manual por el servidor
HTTP real de app2 antes de liberar A. No son dobles en memoria ni se atribuye el
auxiliar a una solicitud HTTP de app1: es una invocación del servicio real dentro
de esa réplica. Las aserciones inspeccionan los resultados reales del script Lua.

La prueba comprueba dos MISS, rechazo tras INSERT, invalidación manual, pérdida de
generación y recreación por otra réplica (sin ABA); además mide COUNT en general_log,
HIT cruzado, JSON y TTL. El único servicio creado se elimina mediante su CRUD normal
y exclusivamente por su Id de prueba. No se utiliza FLUSHALL/FLUSHDB ni MySQL local.
El informe de ejecución se conserva en `resultado-iteracion4.json`.

### Garantías y límites

Con una invalidación confirmada, una consulta con generación anterior no puede
republicar el resumen: comparar y escribir, así como avanzar y borrar, son atómicos.
Los errores se diagnostican sin incluir secretos; MySQL sigue siendo la fuente de
verdad y no se revierte una operación confirmada por un fallo de Redis. Un timeout
significa falta de confirmación, no certeza de que el servidor no ejecutó el script.

No hay transacción distribuida MySQL/Redis: queda una ventana entre commit e
invalidación, y una invalidación fallida puede dejar datos temporales anteriores.
El TTL limita cada publicación, pero no garantiza frescura estricta desde el momento
del commit si esa invalidación falla. Los tres COUNT tampoco constituyen un snapshot
MySQL transaccional único. Tras agotar el reintento se entrega una lectura sin caché,
no una promesa de ausencia de escrituras concurrentes posteriores.

Todos los procesos que invaliden/publican deben usar el puerto nuevo; no se garantiza
la protección mientras convivan réplicas antiguas que hagan SET/DEL incondicional.
Esta configuración usa Redis único, no Redis Cluster (los dos nombres actuales no
comparten hash tag para un script multiclave en Cluster). No se refactoriza para un
Cluster no utilizado. La ventana GetById/Delete de los CRUD continúa como pendiente
separado, sin ampliar sus contratos.

### Archivos de la Iteración 4

Nuevos:

- Aplicacion/Puertos/ICacheResumenDashboardPort.cs
- Aplicacion/Dto/LecturaResumenDashboard.cs (captura y resultado de publicación)
- tests/h2/ConcurrenciaDashboard/ConcurrenciaDashboard.csproj
- tests/h2/ConcurrenciaDashboard/Program.cs
- tests/h2/concurrencia_real.cjs
- tests/h2/resultado-iteracion4.json

Modificados:

- Aplicacion/Dto/ResultadoDashboard.cs
- Aplicacion/Servicios/ClavesCache.cs
- Aplicacion/Servicios/DashboardCacheService.cs
- Aplicacion/Servicios/MecanicoService.cs
- Aplicacion/Servicios/VehiculoService.cs
- Aplicacion/Servicios/ServicioService.cs
- Infraestructura/Cache/RedisCacheAdapter.cs
- Infraestructura/Cache/CacheDistribuidaDeshabilitada.cs
- Program.cs (registros de ambos puertos sobre el mismo singleton)
- Pages/Index.cshtml (mensaje específico de descarte por concurrencia)
- tests/TallerMecanico.IntegrationTests/DashboardCacheServiceTests.cs
- tests/TallerMecanico.IntegrationTests/RedisCacheAdapterTests.cs
- tests/TallerMecanico.IntegrationTests/InvalidacionDashboardTests.cs
- tests/h2/README.md

### Resultados observados

- Build: 0 errores y 0 advertencias; auxiliar de pruebas también compilado sin advertencias.
- Pruebas específicas: 72 aprobadas, 0 fallos.
- Regresión segura: 322 aprobadas, 107 omitidas, 0 fallos. Se excluyeron expresamente
  las seis clases que podrían consultar MySQL local o enviar correo real.
- Cinco escenarios sincronizados reales aprobados; no se reinició Redis para simular
  pérdida de metadatos y no se probaron caídas reales de contenedores.
- Regresión HTTP de invalidación: seis altas/bajas, diez casos sin invalidación,
  permisos/antiforgery correctos y cuatro sesiones existentes preservadas.
- MISS normal: cada COUNT pasó de 55 a 56; HIT desde B permaneció en 56.
- PTTL de resumen: 58363 ms tras MISS y 55760 ms tras HIT. Expiró naturalmente a -2;
  la generación permaneció idéntica y con TTL=-1. Se repararon tres entradas inválidas.
- MySQL, Redis y Nginx conservaron sus contenedores. Las tres réplicas se actualizaron
  únicamente dentro de H2. El contenedor ajeno permaneció detenido, sin cambios.
- Se eliminaron únicamente registros creados por estas pruebas, mediante sus CRUD.
  El cliente fixture H2 Id=3 y el historial de precio generado se conservan para auditoría.

La evidencia JSON incluye resultados, TTL, valores del resumen, identificadores de
réplicas y logs pertinentes, pero no contraseñas, cookies ni tokens antiforgery.

## Iteración 5: configuración y sesiones

Ver `INFORME-ITERACION5.md` para defectos reproducidos, cambios, pruebas, límites de
durabilidad y alternativas de infraestructura pendientes de autorización.
La evidencia anterior/posterior se conserva en `resultado-iteracion5.json`.
`sesiones_real.cjs` detiene y arranca únicamente Redis H2, después de verificar su
aislamiento; el reinicio elimina las sesiones volátiles de pruebas. No ejecutarlo
en paralelo con otras pruebas H2 que dependan de conservar esos borradores/tokens.

## Iteración 6: regresión final e integración

Ver `INFORME-ITERACION6.md` y `resultado-iteracion6.json`: H2 específico/real pasa.
En el cierre, los nueve fallos de integración se reprodujeron con idéntico mensaje
en main intacto; no se detectaron regresiones nuevas de H2. El equipo aceptó el
riesgo de appsettings.json. Lista para commit final, push y PR, declarando que la
regresión completa no está verde. No se realizó merge, push ni commit.

`autenticacion_real.cjs` prueba login A, logout B y acceso posterior C, con
antiforgery y credenciales exclusivamente H2. Requiere PLAYWRIGHT_MODULE y local.env.
`ejecutar_regresion_mysql.sh` se ejecuta dentro de un SDK temporal conectado solo
a la red H2, con fuente/paquetes de solo lectura y resultados ignorados. Usa
credenciales H2 para bases autotaller_test_* y excluye las seis clases inseguras;
no habilita correo ni navegador Linux. No debe ejecutarse en paralelo con la
prueba que reinicia Redis ni con sus comprobaciones de aislamiento de red.
