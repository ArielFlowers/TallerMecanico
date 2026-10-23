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
