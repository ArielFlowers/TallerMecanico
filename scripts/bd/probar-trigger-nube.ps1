# Prueba si la BD en la nube permite crear triggers (la app crea TRG_Servicios_HistorialCosto).
# Crea una base temporal, prueba un trigger y la borra. No toca taller_mecanico.
# Uso (desde la raiz del repo): powershell -ExecutionPolicy Bypass -File scripts\bd\probar-trigger-nube.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cliente-nube.ps1')

$sql = @'
DROP DATABASE IF EXISTS prueba_trigger_taller;
CREATE DATABASE prueba_trigger_taller;
USE prueba_trigger_taller;
CREATE TABLE t (id INT PRIMARY KEY, costo DECIMAL(10,2));
CREATE TABLE h (id INT AUTO_INCREMENT PRIMARY KEY, t_id INT);
CREATE TRIGGER trg_prueba AFTER UPDATE ON t FOR EACH ROW INSERT INTO h (t_id) VALUES (NEW.id);
INSERT INTO t VALUES (1, 10);
UPDATE t SET costo = 20 WHERE id = 1;
SELECT COUNT(*) AS filas_historial FROM h;
DROP DATABASE prueba_trigger_taller;
'@

$carpeta = Join-Path $env:TEMP 'taller-bd'
New-Item -ItemType Directory -Force $carpeta | Out-Null
# WriteAllText guarda en UTF-8 sin BOM (Out-File de PowerShell 5.1 no sirve aqui)
[System.IO.File]::WriteAllText((Join-Path $carpeta 'prueba_trigger.sql'), $sql)

Invocar-ClienteNube -Montajes @('-v', "${carpeta}:/trabajo:ro") -Comando 'mysql -h "$NUBE_HOST" -P "$NUBE_PUERTO" -u "$NUBE_USUARIO" --ssl-mode=REQUIRED < /trabajo/prueba_trigger.sql'
Write-Host 'OK: la BD en la nube permite crear triggers.' -ForegroundColor Green
