# Base de datos compartida con Aiven y MySQL 8.4

El entorno compartido se configura con una única base MySQL 8.4 en Aiven Free.
Cada integrante ejecuta Redis, tres réplicas de la aplicación y Nginx en su PC.
La base compartida funciona sin Tailscale y sin depender de una PC anfitriona.
El entorno offline y los entornos de pruebas también usan la serie MySQL 8.4.

La configuración del repositorio no acredita que la migración ya se haya ejecutado.
Antes de usar la nube hay que crear el servicio, comprobar el trigger e importar
los datos siguiendo los pasos de esta guía.

## Comandos cortos con .NET para el uso diario en Windows

El repositorio incluye tareas en `Directory.Build.targets` que se ejecutan con
MSBuild, incluido en el SDK de .NET 10. Ese SDK debe estar instalado en la PC.
Las tareas reutilizan `scripts/entorno.ps1` y el PowerShell incluido en Windows
para manejar Docker. La aplicación mantiene sus dependencias NuGet existentes.
No se instala un gestor de paquetes de JavaScript ni se añaden paquetes NuGet.

```powershell
# Base local y datos de ejemplo:
dotnet msbuild -t:LocalUp
dotnet msbuild -t:LocalSeed

# Cambiar a la base compartida:
dotnet msbuild -t:NubeUp
```

Los comandos `LocalUp` y `NubeUp` construyen la imagen de la aplicación con la caché de Docker,
detienen los servicios del otro modo y esperan a que las tres réplicas y Nginx
respondan. Validan configuración y certificados antes del cambio; un fallo durante
la construcción no apaga el modo activo. Docker Desktop debe estar iniciado en
modo Linux. La primera construcción necesita Internet para las descargas.

Para apagar, consultar estado o seguir logs de app1:

```powershell
dotnet msbuild -t:LocalDown
dotnet msbuild -t:LocalStatus
dotnet msbuild -t:LocalLogs
```

Reemplazar el prefijo `Local` por `Nube` para esas mismas operaciones sobre Aiven.
Los comandos `Down` detienen los contenedores del modo elegido, conservándolos junto con sus
volúmenes. El MySQL original 8.0 no es un servicio del modo nube y no se toca.
Los datos locales permanecen en el equipo y los de Aiven en el servicio compartido.
Los logs se cierran con Ctrl+C; `dotnet msbuild -t:Ayuda` muestra la ayuda.
`LocalBuild` y `NubeBuild` reconstruyen sin cambiar el modo activo.

`LocalSeed` se ejecuta después de `LocalUp` y usa el importador
existente. El archivo `scripts/bd/seed/datos_demo.sql` debe haberse recibido y
revisado; el lanzador no lo genera ni lo publica. No incluye cuentas ni sincroniza
con Aiven. Para crear cuentas, una persona ejecuta `.\taller local usuarios`
o `.\taller nube usuarios` y responde a la consola en su propia terminal. El
comando crea un administrador y un recepcionista con nombres nuevos.

El lanzador anterior `.\taller local up` / `.\taller nube up` se conserva como
alternativa. Las tareas de MSBuild son atajos explícitos: no se ejecutan durante
las operaciones habituales de compilación, publicación o pruebas. Las cuentas
siguen creándose directamente en una terminal interactiva, mediante `.\taller`,
para que el humano escriba sus contraseñas.

Los comandos largos que siguen se conservan para migración, administración y
diagnóstico; el trabajo diario puede hacerse con el lanzador.

## Crear el servicio y configurar la conexión

1. Crear una cuenta en [Aiven](https://console.aiven.io/) y un servicio **MySQL Free**
   llamado `taller-mysql`, con versión **8.4**. Elegir Free, no un servicio de pago
   cubierto temporalmente por créditos. El plan Free puede asignar la región automáticamente.
2. Esperar a que el servicio esté ejecutándose y crear la base `taller_mecanico`
   desde la sección de bases de datos. No usar `defaultdb` para la aplicación.
3. Obtener host, puerto y contraseña del usuario `avnadmin` desde la información
   de conexión. No compartir la contraseña por chats públicos ni guardarla en Git.
4. Copiar `.env.example` como `.env` **solo si todavía no existe**. Si existe,
   conservar sus claves locales y su configuración SMTP; modificar únicamente
   los valores del entorno compartido:

   ```dotenv
   APP_IMAGE=taller-mecanico:local
   MYSQL_CONNECTION=Server=<host>;Port=<puerto>;Database=taller_mecanico;User ID=avnadmin;Password=<clave>;SslMode=Required;Maximum Pool Size=3;
   APP_BIND_ADDRESS=127.0.0.1
   APP_PUBLIC_BASE_URL=http://127.0.0.1:8088
   APP_PERMITIR_HTTP_NO_SEGURO=false
   ```

5. Para los scripts, copiar `bd-nube.env.example` como `bd-nube.env`. Completar
   `NUBE_HOST`, `NUBE_PUERTO`, `NUBE_USUARIO=avnadmin`, `NUBE_BD=taller_mecanico`
   y `MYSQL_PWD` con la misma conexión. Sin espacios alrededor de `=` ni extensión
   `.txt`. Guardar como UTF-8 y no pegar su contenido en el chat.
6. Usar Gmail en el entorno compartido: `SMTP_HOST=smtp.gmail.com`, `SMTP_PORT=465`
   y `SMTP_SECURITY=SslOnConnect`. Completar remitente, usuario y contraseña de
   aplicación de forma privada. El Mailpit del antiguo anfitrión ya no es necesario.

`MYSQL_PASSWORD` y `MYSQL_ROOT_PASSWORD` son claves del MySQL **local**, distintas
de las de Aiven. `MYSQL_IMAGE=mysql:8.4` solo se utiliza en el Compose del antiguo
anfitrión. No conservar un digest de una imagen 8.0 al cambiar esta variable.

## Comprobar Aiven antes de importar datos

Desde la raíz del repositorio, en Windows PowerShell 5.1:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bd\probar-trigger-nube.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bd\verificar-nube.ps1
```

La prueba del trigger crea y elimina la base auxiliar `prueba_trigger_taller`;
debe devolver `filas_historial = 1`. Ejecutarla una sola persona a la vez y nunca
guardar datos propios en esa base auxiliar. La prueba debe comprobarse en Aiven
real: una simulación local no garantiza sus permisos.

En `taller_mecanico` vacía, el verificador muestra cero tablas y cero triggers;
el conteo de filas termina con un error de tabla inexistente. Es esperable antes
de importar o arrancar la aplicación. Después del arranque se esperan **12 tablas**,
`TRG_Servicios_HistorialCosto = 1` y una versión `8.4.x`.

Registrar el límite real `max_conexiones`. Mantener `Maximum Pool Size=3` en cada
réplica: cinco integrantes con tres réplicas pueden usar hasta 45 conexiones de
aplicación, además de las de administración. Antes de aumentarlo, revisar el uso
total y el límite del servidor.

## Migrar los datos existentes

1. Inventariar los contenedores y confirmar qué base tiene los datos más completos:

   ```powershell
   docker ps -a --format '{{.Names}} | {{.Image}} | {{.Status}}'
   ```

2. Coordinar que el equipo deje de escribir en la base original durante la
   exportación final y el cambio de conexión. Si el contenedor está detenido,
   iniciarlo únicamente para exportar, conservando su imagen y volumen originales.
3. Exportar su base de aplicación:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bd\exportar-dump-local.ps1 -Contenedor <contenedor-origen> -BaseDatos taller_mecanico
   ```

4. Validar el archivo de `respaldos/`: estructura de las tablas esperadas, tamaño
   mayor que cero y ausencia de `CREATE TRIGGER`, `DEFINER` y `GTID_PURGED`.
   El dump conserva usuarios y datos; no debe publicarse.
5. Verificar el destino y obtener confirmación explícita antes de importar.
   **El dump reemplaza las tablas que contiene.** Si el destino ya tiene datos,
   respaldarlo primero y revisar qué se reemplazará.

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bd\importar-dump-nube.ps1 -Archivo respaldos\<dump-validado>.sql -Confirmar
   ```

6. Comparar los conteos de filas con el origen. Arrancar una réplica para crear
   las tablas que falten y recrear el trigger, que se excluye del dump para evitar
   problemas con `DEFINER`. No descartar la copia ni el volumen originales.

Los scripts usan archivos y `docker cp` para conservar UTF-8. No exportar SQL con
`>` ni `Out-File` en PowerShell 5.1.
Los nombres de los dumps incluyen segundos y milisegundos para conservar cada
copia aunque se ejecuten varios respaldos dentro del mismo minuto.

## Arrancar la aplicación y usarla cada día

Nginx necesita `nginx/certs/localhost.pem` y `localhost-key.pem` incluso al acceder
por HTTP. Si faltan, generarlos como indica [H1 HTTPS](H1-HTTPS.md).

La primera vez:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml build
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d redis app1
docker compose -f docker-compose.yml -f docker-compose.build.yml logs app1 --tail 80
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bd\verificar-nube.ps1
```

Esperar a `Now listening`, comprobar las 12 tablas y el trigger y después iniciar
las otras réplicas y Nginx:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d
(Invoke-WebRequest http://127.0.0.1:8088/Auth/Login -UseBasicParsing).StatusCode
1..6 | ForEach-Object { (Invoke-RestMethod http://127.0.0.1:8088/h1/replica).replica }
Invoke-RestMethod http://127.0.0.1:8088/h2/redis
```

Se esperan HTTP `200`, los identificadores de las tres réplicas y Redis `PONG`.
Para el uso diario:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
docker compose -f docker-compose.yml -f docker-compose.build.yml down
```

Abrir http://127.0.0.1:8088. No agregar `-v` al apagar. Si los usuarios vinieron
en el dump, conservarlos. Si no existen, una persona ejecuta en su propia terminal:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml exec app1 dotnet TallerMecanico.dll --inicializar-usuarios
```

El comando es interactivo y solicita las contraseñas. No automatizarlo ni pasar
contraseñas como argumentos. Las credenciales del equipo se comparten por privado.

### Acceso a cuentas importadas sin recordar la contraseña

La consola existente solo restablece una cuenta cuyo nombre de usuario sea
exactamente `admin`, con rol Administrador y activa. Si esa cuenta existe, una
persona ejecuta desde su propia terminal, con el stack en Development:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml exec app1 dotnet TallerMecanico.dll --restablecer-admin
```

Escribir `CONFIRMAR` y después una contraseña de entre 12 y 128 caracteres; no
se muestra al escribir. Iniciar sesión con `admin` y la contraseña elegida.
El cambio se aplica a la base conectada a esa réplica, no a la copia offline.

Si el administrador importado tiene otro nombre, ese comando no puede
restablecerlo. Como alternativa, el humano puede usar `--inicializar-usuarios`
para añadir un administrador y un recepcionista con nombres nuevos, sin borrar
las cuentas importadas. El comando pide `CONFIRMAR`, nombre y contraseña para
cada cuenta; los nombres deben tener entre 3 y 50 caracteres y las contraseñas,
entre 12 y 128. Cada cuenta se guarda al crearla: si falla la segunda, la primera
puede haber quedado creada. No repetir con un nombre ya existente.

## Trabajar offline y conservar los datos locales

El Compose local usa MySQL 8.4 con el volumen nuevo
`taller-mysql-data-local-84`; el antiguo `taller-mysql-data-local` queda conservado.
El Compose de anfitrión también usa `taller-mysql-data-84` en lugar de
`taller-mysql-data`. Docker agrega el prefijo del proyecto a estos nombres.
Los entornos de pruebas usan almacenamiento temporal.

El proyecto local se llama `tallermecanico-local-84`, separado del stack compartido
y del MySQL anterior. Por eso sus comandos no recrean el contenedor original
`taller-mecanico-mysql` ni reutilizan sus volúmenes. No definir otro
`COMPOSE_PROJECT_NAME` ni usar `-p` al seguir estos comandos.

Antes de sustituir un MySQL persistente, exportar su base. Restaurar el dump en
el servidor 8.4 nuevo, sin montar directamente el volumen anterior en él ni borrar
la copia original. La migración del servidor MySQL no requiere importar sus tablas
de sistema: el contenedor nuevo crea las cuentas SQL con sus variables locales.
Los usuarios de la aplicación sí se conservan en su tabla `Usuarios`.

Para una restauración local, con el dump ya validado y el destino nuevo confirmado:

```powershell
docker compose -f docker-compose.local.yml up -d mysql
# Esperar a que MySQL este healthy antes de importar.
docker cp respaldos\<dump-validado>.sql taller-mecanico-mysql-local:/tmp/restaurar.sql
docker exec taller-mecanico-mysql-local sh -c 'MYSQL_PWD=$MYSQL_ROOT_PASSWORD mysql -uroot --default-character-set=utf8mb4 taller_mecanico < /tmp/restaurar.sql'
# Continuar solo si la importacion termino sin errores.
docker exec taller-mecanico-mysql-local rm -f /tmp/restaurar.sql
```

El archivo SQL no debe pasar por la salida de PowerShell. No detener un contenedor
que ocupa el puerto 3307 sin identificarlo antes; nunca eliminarlo para liberar el puerto.

Para preparar datos de ejemplo desde Aiven:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bd\generar-seed.ps1
```

Revisar `scripts/bd/seed/datos_demo.sql` antes de incorporarlo a Git: excluye
`Usuarios`, `SolicitudesRegistro` y `Auditoria`, pero clientes y mecánicos aún
pueden contener nombres, CI o celulares reales. Publicar el seed únicamente
después de aprobar esa revisión.
Los archivos del seed se excluyen del contexto Docker; se aplican desde el host
con el script y no son necesarios dentro de la imagen de la aplicación.

Para cambiar al entorno offline:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml down
docker compose -f docker-compose.local.yml up -d --build
# Esperar a Now listening en app1 para que exista el esquema.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bd\aplicar-seed-local.ps1
```

Aplicar el seed dos veces y comprobar que no duplica filas. No incluye cuentas:
si la base local no tenía usuarios, crearlos interactivamente con el comando de
inicialización usando `-f docker-compose.local.yml`. Para volver a Aiven:

```powershell
docker compose -f docker-compose.local.yml down
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d
```

Los dos stacks completos usan los mismos puertos; ejecutar uno a la vez. Para
probar el seed se puede iniciar solo el servicio `mysql` local, aplicar el archivo
y detener ese servicio, manteniendo el stack web de Aiven encendido.

## Scripts disponibles

| Script en `scripts/bd/` | Uso |
|---|---|
| `probar-trigger-nube.ps1` | Probar conexión, creación y ejecución de un trigger auxiliar. |
| `verificar-nube.ps1` | Consultar tablas, trigger, versión, conexiones y conteos. |
| `exportar-dump-local.ps1 -Contenedor X` | Exportar la base de un contenedor local a `respaldos/`. |
| `importar-dump-nube.ps1 -Archivo X -Confirmar` | Reemplazar las tablas del dump en el destino de nube confirmado. |
| `respaldar-nube.ps1` | Descargar un respaldo de la nube antes de cambios grandes. |
| `generar-seed.ps1` | Exportar datos sin las tres tablas privadas; requiere revisión humana. |
| `aplicar-seed-local.ps1` | Cargar el seed en el contenedor local, con tablas ya creadas. |

Ejecutar con `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bd\<script>.ps1`.
Los dumps omiten triggers; para restaurarlos hay que arrancar la aplicación.

## Pruebas y comprobación con el equipo

- Usar MySQL 8.4 aislado y `TALLER_TEST_MYSQL` exclusivamente local; ver
  [tests/README.md](../tests/README.md). Nunca ejecutar `dotnet test` contra Aiven.
- Cambiar el costo de un servicio y comprobar una nueva fila de historial.
- Crear un cliente de prueba válido desde una PC y encontrarlo desde otra.
- Con el stack de una PC apagado, comprobar que otra sigue usando Aiven.
- Ejecutar `respaldar-nube.ps1` y comprobar que el archivo existe y contiene datos.
- No incorporar `.env`, `bd-nube.env`, `respaldos/` ni certificados a Git o al build.

## Problemas frecuentes

| Síntoma | Acción |
|---|---|
| Reintentos de base no disponible | Revisar host/puerto y que Aiven esté ejecutándose. Si se agotaron los diez intentos, reiniciar las réplicas después de corregir la causa. |
| Error 1419 o privilegio SUPER | Comprobar `avnadmin` y la prueba del trigger. Revisar `log_bin_trust_function_creators` en Aiven si el servicio permite configurarlo. No continuar con un trigger fallido. |
| Access denied | Revisar privadamente las credenciales y el formato de los archivos. |
| Pool de conexiones agotado | Revisar uso y límite total antes de aumentar el pool de cada réplica. |
| Puerto ocupado | Identificar el proceso o contenedor y acordar qué detener. Conservar los volúmenes. |
| Nginx no carga certificados | Comprobar ambos archivos PEM en `nginx/certs/`. |
| Docker no resuelve el registro | Revisar Docker Desktop y su red antes de volver a descargar. |

Aiven Free puede apagarse por inactividad y no tiene disponibilidad garantizada.
Incluye 1 GB de disco y un límite documentado de 76 conexiones; comprobar los
valores reales del servicio. Fuentes: [plan Free](https://aiven.io/docs/products/mysql/concepts/mysql-free-tier),
[versiones de Aiven](https://aiven.io/docs/products/mysql/reference/version-lifecycle)
y [actualización de MySQL](https://dev.mysql.com/doc/refman/8.4/en/upgrade-paths.html).
