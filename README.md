# TallerMecanico

Sistema de gestión de taller mecánico con .NET 10, tres réplicas, Nginx, HTTPS,
Redis y una base compartida en **Aiven MySQL 8.4**. Esta es la guía principal de
instalación y uso diario en Windows. Para trabajar se necesita Internet y el
servicio Aiven del equipo disponible.

## Preparar la PC una vez

1. Instalar Git, **SDK de .NET 10**, Docker Desktop y
   [mkcert](https://github.com/FiloSottile/mkcert#installation). Iniciar Docker
   Desktop con contenedores Linux. No hace falta instalar MySQL ni Node para
   ejecutar la aplicación.
2. Clonar la rama acordada y abrir PowerShell en la carpeta que contiene
   `TallerMecanico.csproj`. Guardar los cambios propios antes de actualizar un clon.
3. Crear `.env` solo si todavía no existe y completar los valores `CAMBIAR`:

   ```powershell
   if (-not (Test-Path -LiteralPath .env)) { Copy-Item .env.example .env }
   notepad .env
   ```

   | Variables visibles (7) | Uso |
   |---|---|
   | `MYSQL_CONNECTION` | Conexión privada al mismo Aiven del equipo. Conservar `Database=taller_mecanico`, `SslMode=Required` y `Maximum Pool Size=3`. |
   | `SMTP_HOST`, `SMTP_PORT`, `SMTP_SECURITY` | Para Gmail: `smtp.gmail.com`, `465`, `SslOnConnect`. |
   | `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM` | Cuenta remitente, contraseña de aplicación y remitente de esa cuenta. |

   Conservar las comillas simples para tratar `$` literalmente. No definir
   `COMPOSE_PROJECT_NAME` ni sobrescribir los `.env` existentes. Las claves del
   antiguo MySQL local que todavía tengan esos archivos ya no se utilizan.
   La imagen `taller-mecanico:local`, escucha `127.0.0.1`, URL pública
   `http://127.0.0.1:8088` y permiso HTTP no seguro `false` tienen valores
   predeterminados. `APP_IMAGE`, `APP_BIND_ADDRESS`, `APP_PUBLIC_BASE_URL` y
   `APP_PERMITIR_HTTP_NO_SEGURO` siguen admitidas como opciones.
4. Generar los certificados desde la raíz:

   ```powershell
   New-Item -ItemType Directory -Force nginx/certs | Out-Null
   mkcert -install
   mkcert -cert-file nginx/certs/localhost.pem -key-file nginx/certs/localhost-key.pem 127.0.0.1 localhost ::1
   ```

   Si los PEM actuales ya están generados con mkcert y Windows confía en su
   raíz, se conservan. Nginx necesita ambos archivos incluso al entrar por HTTP.
   Cada PC genera sus certificados y conserva privada la clave.

## Trabajar cada día con .NET

```powershell
dotnet msbuild -t:NubeUp
```

Abrir http://127.0.0.1:8088 o https://127.0.0.1. Cada PC construye su imagen desde
el código actual y consulta el Aiven existente; no crea otra base ni importa
una seed. La imagen no necesita publicarse en un registry.

| Acción | Comando |
|---|---|
| Construir y levantar | `dotnet msbuild -t:NubeUp` |
| Apagar conservando datos | `dotnet msbuild -t:NubeDown` |
| Estado | `dotnet msbuild -t:NubeStatus` |
| Logs de app1 (Ctrl+C para salir) | `dotnet msbuild -t:NubeLogs` |
| Construir sin detener la aplicación | `dotnet msbuild -t:NubeBuild` |
| Ayuda | `dotnet msbuild -t:Ayuda` |

`NubeUp` valida configuración y certificados, construye la imagen, actualiza los
contenedores y espera HTTP 200 de las tres réplicas y Nginx. Un fallo de build
conserva los contenedores activos. `NubeDown` detiene solo los servicios de la
aplicación; no elimina contenedores ni volúmenes. Los filtros por proyecto y
servicio protegen el MySQL original conservado de la migración.
`Directory.Build.rsp` permite mostrar el enlace final en consola. Los targets
solo actúan al solicitarlos: build, publish y test siguen su curso habitual.

Para crear cuentas, una persona usa una terminal interactiva:

```powershell
.\taller nube usuarios
```

El comando pide las contraseñas. La cuenta web es distinta de las credenciales
SQL o SMTP. También siguen disponibles `.\taller nube up`, `down`, `status`,
`logs`, `build` y `ayuda`. Los comandos locales y de seed se retiraron.

## Qué ejecuta la PC

El único Compose de uso diario es `docker-compose.yml`, con proyecto
`tallermecanico`: Redis, app1/app2/app3 y Nginx. MySQL 8.4 está en Aiven; las
claves Data Protection se guardan en un volumen compartido entre las réplicas.
Los puertos se conservan en loopback: réplicas `5000/5001/5002`, Nginx
`80/8088/443` y Redis `6379`. El correo de verificación usa el SMTP configurado.

Los datos y cuentas de Aiven se conservan. El inicializador del esquema, las
migraciones y el catálogo de servicios siguen formando parte de la aplicación.
La seed de demostración y el modo offline se retiraron.

## Pruebas y recursos anteriores

Las pruebas automáticas usan **MySQL 8.4 temporal y aislado**, nunca Aiven. Ese
Compose se ejecuta únicamente para pruebas y no es otro modo de trabajo. Ver
[tests/README.md](tests/README.md) para preparar MySQL, Edge y PDF.

Los contenedores y volúmenes antiguos se conservan detenidos. Si una PC aún tiene
el entorno local anterior activo, detener únicamente ese proyecto antes del
primer `NubeUp`, siguiendo [BD_NUBE.md](docs/BD_NUBE.md#recursos-locales-antiguos).
El lanzador ya no cambia de modo automáticamente.

## Administración y referencias

- [Conectar otra PC](docs/COMPARTIR_EQUIPO.md): datos que entrega el responsable.
- [Responsable de la base](docs/BD_NUBE.md): respaldo, restauración y diagnóstico;
  `bd-nube.env` es exclusivamente administrativo y no sustituye `.env`.
- [Validación de nube única](docs/VALIDACION_NUBE_UNICA.md),
  [pruebas corregidas](docs/VALIDACION_PRUEBAS.md) y
  [registro de migración](docs/VALIDACION_MYSQL84.md), con sus fechas y límites.
- [H1: HTTPS y balanceo](docs/H1-HTTPS.md) y [H2: Redis](docs/H2-REDIS.md).

No incorporar `.env`, `bd-nube.env`, `respaldos/` ni `nginx/certs/` a Git.
Conservar los volúmenes antiguos y los respaldos privados; no usar `down -v`.

## Inicio (rama feature/home-inicio)
- Hero con `wwwroot/images/hero-taller.jpg` (copia de `docs/Gemini_Generated_Image_qq3q1yqq3q1yqq3q.jfif`; `watermarked_img_11084677959285558999.jpg` no existe en el repo).
- `Pages/Index.cshtml`: Hero + 3 cards + Dashboard conservado + banner auditoría + footer "AutoTaller Pro - v1.0". Sin botones por decisión. Estilos en `wwwroot/css/site.css` (bloque INICIO/HOME).

## Patrón Fachada en autenticación

- **Cliente:** `Pages/Auth/Registro.cshtml.cs` (`RegistroModel`).
- **Fachada:** `Aplicacion/Fachadas/RegistroFacade.cs`.
- **Subsistemas:** `RegistroUsuarioService`, que valida y guarda la solicitud pendiente, e `IEmailService`, que valida la configuración y envía el enlace; `EnlaceVerificacion` construye la URL segura.
- **Operación principal:** `SolicitarRegistroAsync`, que presenta al cliente una sola operación para completar el flujo de solicitud y envío.
- **Beneficio:** Razor Pages no necesita conocer el orden de validación, persistencia y correo, y cada subsistema conserva una responsabilidad concreta.

## Equipo y resúmenes (insumo del informe Anexo 1)
- Ver `docs/CLASES_CLAVE.md` → sección "Aportes por integrante": US01 Dashboard (Ariel), US02 Mecánicos (Santiago), US03 Vehículos (Adrian), US04 Servicios (Aldair), US05 Historial de Costos (Alex).
- Ver `docs/RUBRICA.md` → estados 10/09/2026 y mapeo SOLID por integrante.

## H2 - Redis como caché distribuida
- Ver [docs/H2-REDIS.md](docs/H2-REDIS.md): servicio `redis` en Docker Compose, caché del dashboard (MISS/HIT), sesión compartida entre réplicas, expiración e invalidación.
