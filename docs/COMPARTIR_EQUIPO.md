# Probar el proyecto desde otra PC del equipo

Todos usan la rama `feature/bd-nube` y la misma base `taller_mecanico` de Aiven.
Cada PC ejecuta su propia aplicación, Redis y Nginx. El servicio de Aiven conserva
los datos compartidos aunque se apague la PC de otro integrante.

## Qué entrega el responsable

- Acceso al repositorio y nombre de la rama `feature/bd-nube`.
- Por un canal privado: host, puerto, usuario SQL y contraseña del servicio Aiven
  existente. La plantilla espera `avnadmin`, usado en la migración y el trigger.
- Para probar el registro por correo: cuenta SMTP de pruebas y su contraseña de
  aplicación, o la indicación de configurar una cuenta propia.
- Una cuenta activa para entrar en la aplicación. Este usuario y su contraseña
  son distintos de los de MySQL y Gmail; el inicio de sesión no se define en `.env`.

La base compartida ya tiene datos y cuentas. Los compañeros no deben crear otro
servicio Aiven, importar el dump, ejecutar la prueba auxiliar del trigger ni cargar
el seed para conectarse. Esas tareas se coordinan con el responsable de la base.
El archivo `bd-nube.env` solo hace falta a quien ejecute los scripts de
administración; `NubeUp` utiliza únicamente `.env`.

## Preparación en cada Windows

Instalar Git, **SDK de .NET 10** (incluye el comando `dotnet msbuild`),
Docker Desktop y [mkcert](https://github.com/FiloSottile/mkcert#installation).
Iniciar Docker Desktop con contenedores Linux. La primera construcción necesita
Internet para descargar las imágenes y dependencias.

Si no se utiliza Chocolatey o Scoop, mkcert tiene ejecutables de Windows en sus
[publicaciones oficiales](https://github.com/FiloSottile/mkcert/releases).
Instalarlo de modo que el comando `mkcert` esté disponible en PowerShell.

Clonar la rama desde PowerShell:

```powershell
git clone --branch feature/bd-nube https://github.com/ArielFlowers/TallerMecanico.git
Set-Location TallerMecanico
```

Si ya existe un clon, guardar los cambios propios antes de cambiar de rama y
actualizarlo con `git fetch origin`, `git switch feature/bd-nube` y
`git pull --ff-only`.

## Completar el `.env`

Desde la raíz, crear el archivo solo si todavía no existe:

```powershell
if (-not (Test-Path -LiteralPath .env)) { Copy-Item .env.example .env }
notepad .env
```

[`.env.example`](../.env.example) es la plantilla completa para los modos actuales.
Sustituir los valores `CAMBIAR` y guardar como `.env`, sin añadir `.txt`.

| Variables | Qué colocar |
|---|---|
| `APP_IMAGE` | Conservar `taller-mecanico:local`; el lanzador construye la imagen. |
| `MYSQL_CONNECTION` | Conexión al mismo servicio Aiven del equipo. Completar host, puerto y clave; conservar `Database=taller_mecanico`, `SslMode=Required` y `Maximum Pool Size=3`. |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_SECURITY` | Conservar `smtp.gmail.com`, `465` y `SslOnConnect` al usar Gmail. |
| `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM` | Correo remitente, contraseña de aplicación y remitente de la misma cuenta. Pueden usar una cuenta de pruebas compartida o cuentas propias. |
| `APP_BIND_ADDRESS`, `APP_PUBLIC_BASE_URL`, `APP_PERMITIR_HTTP_NO_SEGURO` | Conservar `127.0.0.1`, `http://127.0.0.1:8088` y `false` en cada PC. |
| `MYSQL_ROOT_PASSWORD`, `MYSQL_PASSWORD` | Claves elegidas por cada integrante para su MySQL local; no intervienen en `NubeUp`. Si ya hay un volumen local, conservar las claves originales. |

Mantener las comillas simples que rodean la conexión y las contraseñas en la
plantilla: Docker Compose toma su contenido literalmente, incluyendo `$`.
Si un valor contiene una comilla simple, escaparla como `\'`, según la
[sintaxis de Docker Compose](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/#env-file-syntax).
No definir `COMPOSE_PROJECT_NAME` ni añadir variables de Tailscale del plan anterior.

Las claves locales se usan al inicializar un volumen nuevo. Cambiarlas en `.env`
después no cambia las contraseñas guardadas en un MySQL ya existente.
La configuración SMTP es necesaria para probar los correos de registro; el inicio
de sesión con una cuenta existente no envía correo.

## Certificados propios de cada PC

Nginx necesita los dos archivos incluso para abrir la web mediante HTTP.
Desde la raíz del repositorio:

```powershell
New-Item -ItemType Directory -Force nginx/certs | Out-Null
mkcert -install
mkcert -cert-file nginx/certs/localhost.pem -key-file nginx/certs/localhost-key.pem localhost 127.0.0.1 ::1
```

Cada integrante genera sus certificados; las claves privadas de mkcert y los
archivos de `nginx/certs/` se conservan en su equipo.

## Levantar y comprobar la base compartida

```powershell
dotnet msbuild -t:NubeUp
```

Abrir <http://127.0.0.1:8088> e iniciar sesión con la cuenta de la aplicación
recibida. Las cuentas ya están en Aiven; no se inicializan de nuevo en cada PC.
Coordinar las cuentas nuevas con el administrador y usar nombres únicos.

Cada integrante usa su propia dirección `127.0.0.1`: la aplicación de su PC se
conecta a la misma base remota. Los enlaces de verificación también deben abrirse
en la PC desde la que se solicitó el correo, con su aplicación iniciada.

Comandos diarios:

```powershell
dotnet msbuild -t:NubeStatus
dotnet msbuild -t:NubeLogs
dotnet msbuild -t:NubeDown
```

`NubeDown` apaga los contenedores de esa PC. Aiven y las aplicaciones de los demás
integrantes continúan funcionando.

Para verificarlo entre dos PC: crear un cliente de prueba identificable, buscarlo
desde la otra PC, editarlo y comprobar el cambio al recargar. El dashboard puede
conservar su caché hasta 60 segundos. Después, apagar un stack con `NubeDown` y
comprobar que la otra PC sigue consultando los datos.

## Uso local y seed

Para trabajar con una base independiente en la propia PC:

```powershell
dotnet msbuild -t:LocalUp
dotnet msbuild -t:LocalSeed
```

`LocalSeed` requiere recibir el archivo revisado y colocarlo en
`scripts/bd/seed/datos_demo.sql`. El seed actual permanece fuera de Git, pendiente
de revisión de datos personales. No incluye cuentas ni sincroniza la copia local
con Aiven. Si la base local está vacía de usuarios, una persona crea sus cuentas
en su propia terminal con `.\taller local usuarios` (administrador y recepcionista).
Mailpit local está en <http://127.0.0.1:8025>.

## Archivos privados y administración

No publicar `.env`, `bd-nube.env`, respaldos, contraseñas ni claves privadas de
certificados. Entregar las credenciales necesarias por un canal privado y usar
las plantillas del repositorio para completar el archivo en cada PC.

Los scripts de respaldo, importación y verificación están documentados en
[BD_NUBE.md](BD_NUBE.md). Las pruebas de integración usan una base local aislada;
no ejecutar `dotnet test` contra la base compartida.
