# Exporta (dump) la base de un contenedor MySQL LOCAL a la carpeta respaldos\.
# La clave root se usa dentro del contenedor; no se escribe en ningun lado.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\bd\exportar-dump-local.ps1 -Contenedor taller-mecanico-mysql
param(
    [Parameter(Mandatory = $true)][string]$Contenedor,
    [string]$BaseDatos = 'taller_mecanico'
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$respaldos = Join-Path $raiz 'respaldos'
New-Item -ItemType Directory -Force $respaldos | Out-Null
$nombre = "dump_local_$(Get-Date -Format 'yyyyMMdd_HHmmss_fff').sql"

# --skip-triggers: el trigger lo vuelve a crear la app al arrancar (evita errores de DEFINER)
docker exec $Contenedor sh -c "MYSQL_PWD=`$MYSQL_ROOT_PASSWORD mysqldump -uroot --single-transaction --skip-triggers --no-tablespaces --set-gtid-purged=OFF --default-character-set=utf8mb4 --result-file=/tmp/$nombre $BaseDatos"
if ($LASTEXITCODE -ne 0) { throw 'Fallo mysqldump. Revisa el nombre del contenedor y de la base.' }

# docker cp copia los bytes tal cual (no usar > en PowerShell 5.1: guarda en UTF-16)
docker cp "${Contenedor}:/tmp/$nombre" (Join-Path $respaldos $nombre)
if ($LASTEXITCODE -ne 0) { throw 'Fallo docker cp.' }
docker exec $Contenedor rm -f "/tmp/$nombre"

Write-Host "OK: dump guardado en respaldos\$nombre" -ForegroundColor Green
