# Muestra el estado de la BD en la NUBE: cantidad de tablas, trigger y filas por tabla. Solo lectura.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\bd\verificar-nube.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cliente-nube.ps1')

$sql = @'
SELECT COUNT(*) AS tablas_esperadas_12 FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE();
SELECT COUNT(*) AS trigger_esperado_1 FROM information_schema.TRIGGERS
 WHERE TRIGGER_SCHEMA = DATABASE() AND TRIGGER_NAME = 'TRG_Servicios_HistorialCosto';
SELECT @@max_connections AS max_conexiones, @@version AS version_mysql;
SELECT 'Clientes' AS tabla, COUNT(*) AS filas FROM Clientes
UNION ALL SELECT 'Vehiculos', COUNT(*) FROM Vehiculos
UNION ALL SELECT 'Mecanicos', COUNT(*) FROM Mecanicos
UNION ALL SELECT 'Servicios', COUNT(*) FROM Servicios
UNION ALL SELECT 'HistorialCostoServicios', COUNT(*) FROM HistorialCostoServicios
UNION ALL SELECT 'Productos', COUNT(*) FROM Productos
UNION ALL SELECT 'Ordenes', COUNT(*) FROM Ordenes
UNION ALL SELECT 'DetalleOrdenes', COUNT(*) FROM DetalleOrdenes
UNION ALL SELECT 'DetalleServiciosOrdenes', COUNT(*) FROM DetalleServiciosOrdenes
UNION ALL SELECT 'Usuarios', COUNT(*) FROM Usuarios
UNION ALL SELECT 'SolicitudesRegistro', COUNT(*) FROM SolicitudesRegistro
UNION ALL SELECT 'Auditoria', COUNT(*) FROM Auditoria;
'@

$carpeta = Join-Path $env:TEMP 'taller-bd'
New-Item -ItemType Directory -Force $carpeta | Out-Null
[System.IO.File]::WriteAllText((Join-Path $carpeta 'verificar.sql'), $sql)

Invocar-ClienteNube -Montajes @('-v', "${carpeta}:/trabajo:ro") -Comando 'mysql -h "$NUBE_HOST" -P "$NUBE_PUERTO" -u "$NUBE_USUARIO" --ssl-mode=REQUIRED --table "$NUBE_BD" < /trabajo/verificar.sql'
