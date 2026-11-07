# TallerMecanico

Sistema de gestión de taller en .NET 10 con cuatro aplicaciones SBA y una base
MySQL 8.4 compartida en Aiven.

```text
Navegador -> Frontend Razor -> HTTP -> Servicio 1: Órdenes y catálogos (Clean)
                           -> HTTP -> Servicio 2: Usuarios (Hexagonal)
                           -> HTTP -> Servicio 3: Reportes (Hexagonal)
                                         |            |            |
                                         +------ MySQL Aiven ------+
```

El Frontend mantiene las cookies y una sesión en memoria. Las APIs validan la
clave interna y los roles; solo ellas acceden a MySQL. Órdenes prepara el esquema
antes de que arranquen Usuarios y Reportes. Kestrel atiende HTTPS directamente.

## Preparar Windows o macOS

1. Instalar Git, SDK de .NET 10, Docker Desktop con contenedores Linux y
   [mkcert](https://github.com/FiloSottile/mkcert#installation). En Mac elegir las
   versiones correspondientes a Apple Silicon o Intel. No se requiere PowerShell
   en macOS, ni MySQL o Node para ejecutar el producto con Docker.
2. Crear `.env` solamente si todavía no existe:

   Windows (PowerShell):

   ```powershell
   if (-not (Test-Path -LiteralPath .env)) { Copy-Item .env.example .env }
   notepad .env
   ```

   macOS (Terminal):

   ```bash
   test -f .env || cp .env.example .env
   nano .env
   ```

   Completar `MYSQL_CONNECTION` y `SMTP_*` con los datos privados del equipo.
   Conservar las comillas simples, `Database=taller_mecanico`, `SslMode=Required`
   y `Maximum Pool Size=3`. Gmail requiere una contraseña de aplicación.
   `bd-nube.env` es un respaldo antiguo; la aplicación y `scripts/bd/` utilizan
   `.env`. No definir `COMPOSE_PROJECT_NAME`.
3. Agregar `CLAVE_INTERNA` a `.env`, incluso si se conserva una configuración
   anterior. Debe ser aleatoria, tener al menos 32 caracteres y ser distinta de
   las contraseñas de MySQL y de correo. Para generar 32 bytes aleatorios y
   copiarlos al portapapeles sin mostrarlos en la terminal:

   Windows (PowerShell):

   ```powershell
   $bytesClave = New-Object byte[] 32
   $generadorClave = [System.Security.Cryptography.RandomNumberGenerator]::Create()
   $generadorClave.GetBytes($bytesClave)
   $generadorClave.Dispose()
   Set-Clipboard -Value ([BitConverter]::ToString($bytesClave).Replace('-', '').ToLowerInvariant())
   ```

   macOS:

   ```bash
   openssl rand -hex 32 | pbcopy
   ```

   Pegar el valor en `CLAVE_INTERNA='...'`. Compose entrega la misma clave al
   Frontend y a las tres APIs. No publicar esa clave ni la configuración resuelta.
4. Guardar los certificados de este equipo en `certs/`. Si ya existen, conservarlos;
   los certificados anteriores de `nginx/certs/` pueden copiarse a esta carpeta.
   Para generarlos por primera vez:

   Windows:

   ```powershell
   New-Item -ItemType Directory -Force certs | Out-Null
   mkcert -install
   mkcert -cert-file certs/localhost.pem -key-file certs/localhost-key.pem 127.0.0.1 localhost ::1
   ```

   macOS (con Homebrew):

   ```bash
   brew install mkcert
   mkdir -p certs
   mkcert -install
   mkcert -cert-file certs/localhost.pem -key-file certs/localhost-key.pem 127.0.0.1 localhost ::1
   ```

   Cada equipo confía en su propia autoridad local. `mkcert -install` puede pedir
   la contraseña del sistema. Certificados y claves quedan fuera de Git y de las
   imágenes Docker.

## Uso diario

Desde la raíz del repositorio:

```console
dotnet msbuild -t:NubeUp
```

Abrir http://127.0.0.1:8088 o https://127.0.0.1. También se publica HTTP en el
puerto 80. Las APIs solo exponen su puerto 8080 dentro de la red de Compose;
no se publican sus puertos hacia la PC.

| Acción | Comando |
|---|---|
| Construir las cuatro apps y levantar | `dotnet msbuild -t:NubeUp` |
| Apagar conservando contenedores y volúmenes | `dotnet msbuild -t:NubeDown` |
| Estado | `dotnet msbuild -t:NubeStatus` |
| Logs de las cuatro apps | `dotnet msbuild -t:NubeLogs` |
| Construir sin detener | `dotnet msbuild -t:NubeBuild` |
| Ayuda | `dotnet msbuild -t:Ayuda` |
| Crear cuentas en Windows, desde terminal interactiva | `.\taller nube usuarios` |
| Crear cuentas en macOS, desde terminal interactiva | `bash ./taller.sh nube usuarios` |

`NubeUp` valida `.env`, clave interna y certificados, construye las cuatro
imágenes y espera los healthchecks de `/salud`. Usuarios y Reportes esperan a
Órdenes; el Frontend espera a las tres APIs. Luego comprueba el acceso HTTP desde
la PC. En la primera transición al despliegue SBA, Compose retira los contenedores
del despliegue anterior mediante `--remove-orphans`, conservando los volúmenes.

El comando `usuarios` ejecuta `TallerMecanico.ServicioUsuarios.dll
--inicializar-usuarios` en `servicio-usuarios`. Los comandos de administración de
cuentas ya no se ejecutan en el Frontend. Se pueden consultar logs de una sola app
con `docker compose logs --tail 80 servicio-usuarios` (o el nombre correspondiente).

Los atajos también admiten `.\taller nube status` en Windows y
`bash ./taller.sh nube status` en macOS. `Directory.Solution.targets` selecciona
PowerShell o Bash y solo actúa al pedir estos targets. `dotnet build`, `restore`,
`publish` y `test` mantienen su comportamiento normal. No usar `down -v` para
apagar el entorno.

## Organización del repositorio

| Ubicación | Responsabilidad |
|---|---|
| `frontend-app/` | Razor Pages, recursos web, sesión, cookies y clientes HTTP en `Api/` |
| `backend-services/servicio-ordenes/` | Servicio 1: dominio, casos de uso Clean, catálogos, dashboard y esquema MySQL |
| `backend-services/servicio-usuarios/` | Servicio 2: autenticación, cuentas, invitaciones, correo y auditoría de acceso |
| `backend-services/servicio-reportes/` | Servicio 3: consultas y generación de PDF y Excel mediante puertos y adaptadores |
| `shared/TallerMecanico.Contratos/` | DTO compartidos; no contiene acceso a datos |
| `tests/` | Proyectos de pruebas por aplicación y herramientas comunes |
| `scripts/` | Lanzadores Windows/macOS y administración de la base |
| `certs/` | Certificados locales ignorados por Git, montados solo en el Frontend |

Cada aplicación tiene su Dockerfile. El contexto de construcción es la raíz
para incluir `shared/`. El Frontend no recibe la conexión MySQL ni las credenciales
SMTP. La sesión vive en memoria de su única instancia; reiniciarla descarta los
borradores en curso. Las claves de cookies se conservan en `taller-dpkeys`.

## Pruebas y CI

Compilar y ejecutar las suites desde la raíz:

```console
dotnet build TallerMecanico.sln
dotnet test TallerMecanico.sln -- xUnit.ParallelizeTestCollections=false
```

Para ejecutar solo una aplicación, usar su `.csproj` en `tests/frontend-app.Tests`,
`tests/servicio-ordenes.Tests`, `tests/servicio-usuarios.Tests` o
`tests/servicio-reportes.Tests`. Las pruebas MySQL requieren una instancia temporal;
nunca deben apuntar a Aiven ni usar `MYSQL_CONNECTION`.

Windows:

```powershell
if (-not (Test-Path -LiteralPath tests/.env.test)) { Copy-Item tests/.env.test.example tests/.env.test }
notepad tests/.env.test
docker compose --project-directory . --env-file tests/.env.test -f tests/docker-compose.test.yml up -d --wait
$env:TALLER_TEST_MYSQL = 'Server=127.0.0.1;Port=3308;User ID=root;Password=CLAVE_DE_PRUEBAS;SslMode=Disabled;'
dotnet test TallerMecanico.sln -- xUnit.ParallelizeTestCollections=false
```

macOS:

```bash
test -f tests/.env.test || cp tests/.env.test.example tests/.env.test
nano tests/.env.test
docker compose --project-directory . --env-file tests/.env.test -f tests/docker-compose.test.yml up -d --wait
export TALLER_TEST_MYSQL='Server=127.0.0.1;Port=3308;User ID=root;Password=CLAVE_DE_PRUEBAS;SslMode=Disabled;'
dotnet test TallerMecanico.sln -- xUnit.ParallelizeTestCollections=false
```

Usar en `TALLER_TEST_MYSQL` la misma clave privada de `tests/.env.test`. Las pruebas
crean bases `autotaller_test_<guid>` y las eliminan al terminar. Para detener solo
el MySQL de pruebas:

```console
docker compose --project-directory . --env-file tests/.env.test -f tests/docker-compose.test.yml down
```

El workflow y sus filtros están en `.github/workflows/ci.yml` y en
[la guía de CI](docs/CI.md); ejecuta las cuatro suites de la solución con los filtros por área y conserva
un resultado TRX por ensamblado. Los recorridos de navegador y la lectura de PDF requieren:

- Navegador: `npm ci --prefix tests/browser`, instalar Edge con
  `node tests/browser/node_modules/playwright/cli.js install msedge` y definir
  `PLAYWRIGHT_MODULE` con la ruta absoluta a `tests/browser/node_modules/playwright`.
  Como alternativa, instalar `chromium` con el mismo comando y definir
  `PLAYWRIGHT_CHANNEL=chromium`; Edge continúa siendo el predeterminado.
- Contenido PDF: `python -m pip install -r tests/requirements-ci.txt` y definir
  `TALLER_PDF_PYTHON` con la ruta del ejecutable Python.

## Referencias

- [Arquitectura SBA y despliegue](docs/SBA.md).
- [Clean Architecture de Órdenes](docs/ARQUITECTURA_CLEAN.md).
- [Arquitectura hexagonal de Usuarios y Reportes](docs/ARQUITECTURA_HEXAGONAL.md).
- [HTTPS directo en Kestrel](docs/H1-HTTPS.md).
- [Administrar, respaldar y restaurar Aiven](docs/BD_NUBE.md).
- [Estado y evidencia de la rúbrica](docs/CUMPLIMIENTO_RUBRICA.md).
- [Resultados de validación SBA](docs/VALIDACION_SBA.md).
- [Material de entrega de Usuarios y Reportes](docs/ENTREGA_SBA_USUARIOS_REPORTES.md).
- [Guía de integración para los compañeros](docs/GUIA_COMPANEROS_SBA.md).

No subir `.env`, `tests/.env.test`, respaldos ni claves de certificados a Git.
