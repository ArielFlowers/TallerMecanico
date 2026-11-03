# Importa un dump a la BD en la NUBE. ATENCION: reemplaza (DROP + CREATE) las tablas del dump.
# Por seguridad exige el parametro -Confirmar.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\bd\importar-dump-nube.ps1 -Archivo respaldos\dump_local_XXXX.sql -Confirmar
param(
    [Parameter(Mandatory = $true)][string]$Archivo,
    [switch]$Confirmar
)
$ErrorActionPreference = 'Stop'
if (-not $Confirmar) { throw 'Este comando reemplaza tablas en la nube. Vuelve a ejecutarlo con -Confirmar.' }

. (Join-Path $PSScriptRoot 'cliente-nube.ps1')

$ruta = (Resolve-Path $Archivo).Path
$nombre = Split-Path $ruta -Leaf

# El archivo se monta en el contenedor: asi no pasa por PowerShell y no se rompen tildes ni enies
Invocar-ClienteNube -Montajes @('-v', "${ruta}:/trabajo/dump.sql:ro") -Comando 'mysql -h "$NUBE_HOST" -P "$NUBE_PUERTO" -u "$NUBE_USUARIO" --ssl-mode=REQUIRED --default-character-set=utf8mb4 "$NUBE_BD" < /trabajo/dump.sql'

Write-Host "OK: $nombre importado en la nube." -ForegroundColor Green
