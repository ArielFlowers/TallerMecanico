# TallerMecanico

Sistema de gestión de taller con .NET 10, tres réplicas, Nginx, HTTPS
y una base compartida en Aiven MySQL 8.4.

## Preparar Windows o macOS

1. Instalar Git, SDK de .NET 10, Docker Desktop y
   [mkcert](https://github.com/FiloSottile/mkcert#installation). Iniciar Docker
   Desktop con contenedores Linux. En Mac instalar las versiones correspondientes
   a Apple Silicon (ARM64) o Intel (x64); las imágenes Docker seleccionan la
   arquitectura automáticamente. No es necesario instalar PowerShell en macOS.
2. Desde la raíz del repositorio, crear `.env` si todavía no existe:

   En Windows (PowerShell):

   ```powershell
   if (-not (Test-Path -LiteralPath .env)) { Copy-Item .env.example .env }
   notepad .env
   ```

   En macOS (Terminal, zsh o Bash):

   ```bash
   test -f .env || cp .env.example .env
   nano .env
   ```

   Completar `MYSQL_CONNECTION` con la conexión privada al Aiven del equipo y
   las variables `SMTP_*` con la cuenta de correo. Conservar las comillas simples,
   `Database=taller_mecanico`, `SslMode=Required` y `Maximum Pool Size=3`.
   Gmail requiere una contraseña de aplicación.

   La aplicación y los scripts de `scripts/bd/` usan esta misma configuración.
   `bd-nube.env` ya no se utiliza; una copia privada anterior puede conservarse
   como respaldo. No definir `COMPOSE_PROJECT_NAME`.
3. Generar los certificados de este equipo, si todavía no existen.
   En Windows (PowerShell):

   ```powershell
   New-Item -ItemType Directory -Force nginx/certs | Out-Null
   mkcert -install
   mkcert -cert-file nginx/certs/localhost.pem -key-file nginx/certs/localhost-key.pem 127.0.0.1 localhost ::1
   ```

   En macOS (con [Homebrew](https://brew.sh/) instalado):

   ```bash
   brew install mkcert
   mkdir -p nginx/certs
   mkcert -install
   mkcert -cert-file nginx/certs/localhost.pem -key-file nginx/certs/localhost-key.pem 127.0.0.1 localhost ::1
   ```

   `mkcert -install` puede solicitar la contraseña del sistema para confiar en
   la autoridad local. Cada equipo debe generar y confiar en sus certificados.

## Uso diario

Los mismos targets funcionan desde PowerShell en Windows y desde Terminal en
macOS. Ejecutarlos desde la raíz del repositorio:

```console
dotnet msbuild -t:NubeUp
```

Abrir http://127.0.0.1:8088 o https://127.0.0.1. Cada PC construye la imagen de su
código y se conecta al mismo Aiven; no necesita instalar MySQL ni Node para usar
la aplicación. Los datos y las cuentas del equipo se conservan.

| Acción | Comando |
|---|---|
| Construir y levantar | `dotnet msbuild -t:NubeUp` |
| Apagar conservando datos | `dotnet msbuild -t:NubeDown` |
| Estado | `dotnet msbuild -t:NubeStatus` |
| Logs de app1 | `dotnet msbuild -t:NubeLogs` |
| Construir sin detener | `dotnet msbuild -t:NubeBuild` |
| Ayuda | `dotnet msbuild -t:Ayuda` |
| Crear cuentas en Windows (terminal interactiva) | `.\taller nube usuarios` |
| Crear cuentas en macOS (terminal interactiva) | `bash ./taller.sh nube usuarios` |

También se pueden ejecutar los atajos directamente: `.\taller nube status` en
Windows o `bash ./taller.sh nube status` en macOS. El lanzador Bash acepta
`up`, `down`, `status`, `logs`, `build`, `usuarios` y `ayuda`; no requiere `chmod`.
`Directory.Build.targets` selecciona PowerShell en Windows y Bash en macOS/Linux.
Estos atajos solo se ejecutan al pedirlos: `dotnet build`, `restore`, `publish`
y `test` conservan su comportamiento normal.

`NubeUp` valida la configuración y los certificados, construye y espera a las
réplicas y Nginx. `NubeDown` detiene los servicios de la aplicación. No usar
`down -v` para apagar el entorno.

## Pruebas y CI

Ejecutar la suite xUnit desde la raíz:

```powershell
dotnet test tests/frontend-app.Tests/frontend-app.Tests.csproj -- xUnit.ParallelizeTestCollections=false
```

Las pruebas de lógica funcionan sin configuración. Las de persistencia requieren
un MySQL temporal y crean bases `autotaller_test_<guid>` que eliminan al terminar.
Nunca usan `MYSQL_CONNECTION` ni deben apuntarse a Aiven.

Para incluir persistencia en una ejecución local:

Windows (PowerShell):

```powershell
if (-not (Test-Path -LiteralPath tests/.env.test)) { Copy-Item tests/.env.test.example tests/.env.test }
notepad tests/.env.test
docker compose --project-directory . --env-file tests/.env.test -f tests/docker-compose.test.yml up -d --wait
# Usar la misma clave que se puso en tests/.env.test:
$env:TALLER_TEST_MYSQL = 'Server=127.0.0.1;Port=3308;User ID=root;Password=CLAVE_DE_PRUEBAS;SslMode=Disabled;'
dotnet test tests/frontend-app.Tests/frontend-app.Tests.csproj -- xUnit.ParallelizeTestCollections=false
```

macOS (Terminal):

```bash
test -f tests/.env.test || cp tests/.env.test.example tests/.env.test
nano tests/.env.test
docker compose --project-directory . --env-file tests/.env.test -f tests/docker-compose.test.yml up -d --wait
# Usar la misma clave que se puso en tests/.env.test:
export TALLER_TEST_MYSQL='Server=127.0.0.1;Port=3308;User ID=root;Password=CLAVE_DE_PRUEBAS;SslMode=Disabled;'
dotnet test tests/frontend-app.Tests/frontend-app.Tests.csproj -- xUnit.ParallelizeTestCollections=false
```

`tests/.env.test` solo configura el contenedor descartable de pruebas y mantiene
su clave separada de la base compartida. Para detenerlo:

```powershell
docker compose --project-directory . --env-file tests/.env.test -f tests/docker-compose.test.yml down
```

Hay un solo [CI](.github/workflows/ci.yml), organizado por propósito:

| Check | Responsabilidad |
|---|---|
| Backend | Lógica de negocio, servicios, validaciones, caché y PDF. |
| API / HTTP | Endpoints, formularios, autenticación, permisos y respuestas HTTPS. |
| Base de datos / MySQL | Repositorios, migraciones, transacciones e integridad. |
| Frontend / navegador | Interacciones reales en Microsoft Edge con Playwright. |
| Compatibilidad (Windows / macOS) | Compilar, publicar y comprobar lanzadores y PDF en cada sistema. |

Se ejecuta una vez por actualización del PR y en los pushes a `main`.
Un nuevo commit cancela la ejecución anterior del mismo PR. Windows y macOS
comparten la misma definición de job; cada uno usa su propia máquina.
CI prepara MySQL temporal, Edge, Playwright y Python para PDF y no necesita
credenciales del equipo. Los resultados TRX se guardan por área durante siete días.

La [guía de CI](docs/CI.md) explica los filtros por área, los checks y cómo
reproducirlos. Por ejemplo, para ejecutar solo la lógica de backend:

```console
dotnet test tests/frontend-app.Tests/frontend-app.Tests.csproj --configuration Release --filter "Area=Backend" -- xUnit.ParallelizeTestCollections=false
```

Localmente, los recorridos de navegador y la extracción de texto PDF se omiten
si no están configuradas sus herramientas. Para habilitarlos:

- Navegador: `npm ci --prefix tests/browser` e instalar Edge con
  `node tests/browser/node_modules/playwright/cli.js install msedge`.
  Definir `PLAYWRIGHT_MODULE` con la ruta absoluta a
  `tests/browser/node_modules/playwright`.
- Contenido PDF: `python -m pip install -r tests/requirements-ci.txt` y definir
  `TALLER_PDF_PYTHON` con la ruta del ejecutable Python.

## Referencias

- [Conectar otra PC](docs/COMPARTIR_EQUIPO.md).
- [Administrar, respaldar y restaurar Aiven](docs/BD_NUBE.md).
- [Clean Architecture de Órdenes](docs/ARQUITECTURA_CLEAN.md),
  [Arquitectura hexagonal de Usuarios y Reportes](docs/ARQUITECTURA_HEXAGONAL.md),
  [HTTPS y balanceo](docs/H1-HTTPS.md).

No subir `.env`, `tests/.env.test`, respaldos ni claves de certificados a Git.
