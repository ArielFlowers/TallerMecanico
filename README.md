# TallerMecanico
  Sistema de gestión para taller mecánico

## Base de datos (MySQL 8, rama feature/mysql-factory-method)
- Levantar: `docker compose up -d` (crea BD `taller_mecanico`, usuario `taller`/`taller123`, puerto 3306).
- `appsettings.json`: `ConnectionStrings:MySqlConnection`. No commitear claves reales: usar UserSecrets en local.
- Las tablas + trigger se autocrean al arrancar (`DatabaseInitializer`). El esquema de autenticación incluye correo, solicitudes y control de reenvío; las estructuras faltantes de las versiones anteriores se agregan sin recrear las tablas. Ver [instalación de autenticación](docs/migraciones/README.md). DDL de referencia en `Data/Scripts/MySql/`.
- Reset DB: `docker compose down -v` (borra el volumen) y reiniciar.
- Si el arranque falla con "SUPER privilege and binary logging": el compose ya trae `--log-bin-trust-function-creators=1`. En MySQL local propio, ejecutar como root: `SET GLOBAL log_bin_trust_function_creators = 1;`

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
