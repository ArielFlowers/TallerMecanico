# TallerMecanico
  Sistema de gestión para taller mecánico

## Base de datos (MySQL 8)
- **Entorno compartido (recomendado):** una sola base MySQL en la PC de un integrante, accesible por Tailscale. La PC anfitriona levanta `docker compose -f docker-compose.host.yml up -d` (MySQL + Mailpit). El resto del equipo configura `MYSQL_CONNECTION` y `APP_IMAGE` en `.env` y ejecuta `docker compose up -d`.
- **Anfitrión actual:** nodo `taller-db`, IP `100.99.175.117`, URL HTTPS `https://taller-db.tail590083.ts.net`. MySQL en `100.99.175.117:3307`; Mailpit SMTP `100.99.175.117:1026` y UI `http://100.99.175.117:8026`.
- **Stack del anfitrión:** `docker compose -f docker-compose.host.yml up -d` y luego `docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build`. El HTTPS se publica con `tailscale serve --bg 8088`; Nginx escucha en `127.0.0.1:8088`.
- **Entorno local/offline:** `docker compose -f docker-compose.local.yml up --build` levanta MySQL, Mailpit, Redis, 3 réplicas y Nginx sin depender de Tailscale. MySQL queda en `127.0.0.1:3307` y Mailpit en `127.0.0.1:8025`.
- Las tablas + trigger se autocrean al arrancar (`DatabaseInitializer`), protegidas por un lock de MySQL para que las réplicas no compitan. Ver [instalación de autenticación](docs/migraciones/README.md). DDL de referencia en `Data/Scripts/MySql/`.
- Si el arranque falla con "SUPER privilege and binary logging": los compose ya traen `--log-bin-trust-function-creators=1`. En MySQL local propio, ejecutar como root: `SET GLOBAL log_bin_trust_function_creators = 1;`
- El correo de verificación acepta Mailpit (`SMTP_SECURITY=None`, sin credenciales) o Gmail (`SslOnConnect`, usuario y contraseña de aplicación). Ver [plan de entorno remoto](docs/PLAN_ENTORNO_REMOTO.md).
- No commitear `.env` ni claves reales. Para desarrollo sin Docker usar UserSecrets.
- Reset de la base local: `docker compose -f docker-compose.local.yml down -v` (borra el volumen).

## Inicio (rama feature/home-inicio)
- Hero con `wwwroot/images/hero-taller.jpg` (copia de `docs/Gemini_Generated_Image_qq3q1yqq3q1yqq3q.jfif`; `watermarked_img_11084677959285558999.jpg` no existe en el repo).
- `Pages/Index.cshtml`: Hero + 3 cards + Dashboard conservado + banner auditoría + footer "AutoTaller Pro - v1.0". Sin botones por decisión. Estilos en `wwwroot/css/site.css` (bloque INICIO/HOME).

## Patrón Fachada en autenticación

- **Cliente:** `Pages/Auth/Registro.cshtml.cs` (`RegistroModel`).
- **Fachada:** `Application/Facades/RegistroFacade.cs`.
- **Subsistemas:** `RegistroUsuarioService`, que valida y guarda la solicitud pendiente, e `IEmailService`, que valida la configuración y envía el enlace; `EnlaceVerificacion` construye la URL segura.
- **Operación principal:** `SolicitarRegistroAsync`, que presenta al cliente una sola operación para completar el flujo de solicitud y envío.
- **Beneficio:** Razor Pages no necesita conocer el orden de validación, persistencia y correo, y cada subsistema conserva una responsabilidad concreta.

## Equipo y resúmenes (insumo del informe Anexo 1)
- Ver `docs/CLASES_CLAVE.md` → sección "Aportes por integrante": US01 Dashboard (Ariel), US02 Mecánicos (Santiago), US03 Vehículos (Adrian), US04 Servicios (Aldair), US05 Historial de Costos (Alex).
- Ver `docs/RUBRICA.md` → estados 10/09/2026 y mapeo SOLID por integrante.

## H2 - Redis como caché distribuida
- Ver [docs/H2-REDIS.md](docs/H2-REDIS.md): servicio `redis` en Docker Compose, caché del dashboard (MISS/HIT), sesión compartida entre réplicas, expiración e invalidación.
