# Carga scripts\bd\seed\datos_demo.sql en un MySQL LOCAL (nunca en la nube).
# Antes hay que arrancar la app una vez contra esa base para que cree las tablas.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\bd\aplicar-seed-local.ps1
param(
    [string]$Contenedor = 'taller-mecanico-mysql-local',
    [string]$BaseDatos = 'taller_mecanico'
)
$ErrorActionPreference = 'Stop'
$archivo = Join-Path $PSScriptRoot 'seed\datos_demo.sql'
if (-not (Test-Path $archivo)) { throw "No existe $archivo. Generalo con generar-seed.ps1." }

docker cp $archivo "${Contenedor}:/tmp/datos_demo.sql"
if ($LASTEXITCODE -ne 0) { throw "No se pudo copiar al contenedor $Contenedor. Esta encendido?" }

docker exec $Contenedor sh -c "MYSQL_PWD=`$MYSQL_ROOT_PASSWORD mysql -uroot --default-character-set=utf8mb4 $BaseDatos < /tmp/datos_demo.sql"
if ($LASTEXITCODE -ne 0) { throw 'Fallo la carga del seed. Si dice que falta una tabla, arranca la app una vez contra esta base.' }
docker exec $Contenedor rm -f /tmp/datos_demo.sql

Write-Host "OK: seed aplicado en $Contenedor/$BaseDatos" -ForegroundColor Green
