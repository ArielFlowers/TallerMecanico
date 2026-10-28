# TallerMecanico
  Sistema de gestión para taller mecánico

## Base de datos compartida en la nube (Aiven MySQL 8.4)

- **Entorno compartido:** configurar Aiven for MySQL Free y `MYSQL_CONNECTION` en `.env`. Cada integrante ejecuta Redis, tres réplicas y Nginx en su PC; el acceso a la base no depende de Tailscale ni de la PC anfitriona.
- **Inicio:** `docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build`. Abrir http://127.0.0.1:8088.
- **Configuración, migración y scripts:** [docs/BD_NUBE.md](docs/BD_NUBE.md). Incluye dump, respaldo, seed y comprobación del trigger en Aiven real.
- **Entorno local/offline:** `docker compose -f docker-compose.local.yml up -d --build` levanta MySQL 8.4, Mailpit, Redis, tres réplicas y Nginx. MySQL escucha en `127.0.0.1:3307` y Mailpit en `http://127.0.0.1:8025`.
- **Datos anteriores:** los Compose persistentes usan volúmenes nuevos para 8.4. Exportar y restaurar los datos; conservar los volúmenes anteriores. No usar `down -v`.
- **Esquema:** las 12 tablas y el trigger se crean al arrancar, con un lock entre réplicas. En Aiven se debe comprobar el trigger con `avnadmin`; los MySQL locales incluyen `--log-bin-trust-function-creators=1`.
- **Correo:** usar Gmail (`SMTP_SECURITY=SslOnConnect`) para el entorno compartido. El entorno offline configura Mailpit automáticamente.
- **Pruebas:** los entornos de integración también usan MySQL 8.4; `TALLER_TEST_MYSQL` debe apuntar exclusivamente a un servidor local aislado.
- **Validación de la migración:** [resultados, conservación de datos y límites de la regresión](docs/VALIDACION_MYSQL84.md).
- **Plan B:** `docker-compose.host.yml` conserva el antiguo esquema de anfitrión, actualizado a 8.4. No iniciarlo con Aiven. El [plan anterior](docs/PLAN_ENTORNO_REMOTO.md) y el [resumen de esa configuración](docs/RESUMEN_SESION_2026-10-23.md) se conservan como antecedentes.
- No commitear `.env`, `bd-nube.env`, `respaldos/` ni `nginx/certs/`. Para desarrollo sin Docker usar User Secrets.

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
