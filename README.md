# TallerMecanico
  Sistema de gestión para taller mecánico

## Base de datos (MySQL 8, rama feature/mysql-factory-method)
- Levantar: `docker compose up -d` (crea BD `taller_mecanico`, usuario `taller`/`taller123`, puerto 3306).
- `appsettings.json`: `ConnectionStrings:MySqlConnection`. No commitear claves reales: usar UserSecrets en local.
- Las tablas + trigger se autocrean al arrancar (`DatabaseInitializer`). DDL de referencia en `Data/Scripts/MySql/`.
- Reset DB: `docker compose down -v` (borra el volumen) y reiniciar.
- Si el arranque falla con "SUPER privilege and binary logging": el compose ya trae `--log-bin-trust-function-creators=1`. En MySQL local propio, ejecutar como root: `SET GLOBAL log_bin_trust_function_creators = 1;`

## Inicio (rama feature/home-inicio)
- Hero con `wwwroot/images/hero-taller.jpg` (copia de `docs/Gemini_Generated_Image_qq3q1yqq3q1yqq3q.jfif`; `watermarked_img_11084677959285558999.jpg` no existe en el repo).
- `Pages/Index.cshtml`: Hero + 3 cards + Dashboard conservado + banner auditoría + footer "AutoTaller Pro - v1.0". Sin botones por decisión. Estilos en `wwwroot/css/site.css` (bloque INICIO/HOME).

## Equipo y resúmenes (insumo del informe Anexo 1)
- Ver `docs/CLASES_CLAVE.md` → sección "Aportes por integrante": US01 Dashboard (Ariel), US02 Mecánicos (Santiago), US03 Vehículos (Adrian), US04 Servicios (Aldair), US05 Historial de Costos (Alex).
- Ver `docs/RUBRICA.md` → estados 10/09/2026 y mapeo SOLID por integrante.
