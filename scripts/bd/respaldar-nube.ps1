# Descarga un respaldo completo de la BD en la NUBE a la carpeta respaldos\.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\bd\respaldar-nube.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
. (Join-Path $PSScriptRoot 'cliente-nube.ps1')

$respaldos = Join-Path $raiz 'respaldos'
New-Item -ItemType Directory -Force $respaldos | Out-Null
$nombre = "dump_nube_$(Get-Date -Format 'yyyyMMdd_HHmmss_fff').sql"

Invocar-ClienteNube -Montajes @('-v', "${respaldos}:/respaldos") -Comando "mysqldump -h `"`$NUBE_HOST`" -P `"`$NUBE_PUERTO`" -u `"`$NUBE_USUARIO`" --ssl-mode=REQUIRED --single-transaction --skip-triggers --no-tablespaces --set-gtid-purged=OFF --default-character-set=utf8mb4 --result-file=/respaldos/$nombre `"`$NUBE_BD`""

Write-Host "OK: respaldo guardado en respaldos\$nombre" -ForegroundColor Green
