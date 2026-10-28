# Genera scripts\bd\seed\datos_demo.sql con los DATOS de la nube (sin estructura).
# No incluye Usuarios, SolicitudesRegistro ni Auditoria (tienen claves, correos y registros personales).
# Usa INSERT IGNORE: se puede aplicar varias veces sin duplicar.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\bd\generar-seed.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$envFile = Join-Path $raiz 'bd-nube.env'
if (-not (Test-Path $envFile)) { throw "No existe $envFile." }

$seed = Join-Path $PSScriptRoot 'seed'
New-Item -ItemType Directory -Force $seed | Out-Null

$opciones = '--no-create-info --skip-triggers --insert-ignore --complete-insert --skip-extended-insert --skip-comments --single-transaction --no-tablespaces --set-gtid-purged=OFF --default-character-set=utf8mb4'
$excluir = '--ignore-table=$NUBE_BD.Usuarios --ignore-table=$NUBE_BD.SolicitudesRegistro --ignore-table=$NUBE_BD.Auditoria'

docker run --rm --env-file $envFile -v "${seed}:/seed" mysql:8.4 sh -c "mysqldump -h `$NUBE_HOST -P `$NUBE_PUERTO -u `$NUBE_USUARIO --ssl-mode=REQUIRED $opciones $excluir --result-file=/seed/datos_demo.sql `$NUBE_BD"
if ($LASTEXITCODE -ne 0) { throw 'Fallo la generacion del seed.' }

Write-Host 'OK: seed generado en scripts\bd\seed\datos_demo.sql. Revisalo antes de commitear.' -ForegroundColor Green
