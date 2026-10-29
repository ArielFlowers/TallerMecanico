# Descarga un respaldo completo de la BD en la NUBE a la carpeta respaldos\.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\bd\respaldar-nube.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$envFile = Join-Path $raiz 'bd-nube.env'
if (-not (Test-Path $envFile)) { throw "No existe $envFile." }

$respaldos = Join-Path $raiz 'respaldos'
New-Item -ItemType Directory -Force $respaldos | Out-Null
$nombre = "dump_nube_$(Get-Date -Format 'yyyyMMdd_HHmmss_fff').sql"

docker run --rm --env-file $envFile -v "${respaldos}:/respaldos" mysql:8.4 sh -c "mysqldump -h `$NUBE_HOST -P `$NUBE_PUERTO -u `$NUBE_USUARIO --ssl-mode=REQUIRED --single-transaction --skip-triggers --no-tablespaces --set-gtid-purged=OFF --default-character-set=utf8mb4 --result-file=/respaldos/$nombre `$NUBE_BD"
if ($LASTEXITCODE -ne 0) { throw 'Fallo el respaldo.' }

Write-Host "OK: respaldo guardado en respaldos\$nombre" -ForegroundColor Green
