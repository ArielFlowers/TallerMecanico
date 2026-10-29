# Importa un dump a la BD en la NUBE. ATENCION: reemplaza (DROP + CREATE) las tablas del dump.
# Por seguridad exige el parametro -Confirmar.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\bd\importar-dump-nube.ps1 -Archivo respaldos\dump_local_XXXX.sql -Confirmar
param(
    [Parameter(Mandatory = $true)][string]$Archivo,
    [switch]$Confirmar
)
$ErrorActionPreference = 'Stop'
if (-not $Confirmar) { throw 'Este comando reemplaza tablas en la nube. Vuelve a ejecutarlo con -Confirmar.' }

$raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$envFile = Join-Path $raiz 'bd-nube.env'
if (-not (Test-Path $envFile)) { throw "No existe $envFile." }

$ruta = (Resolve-Path $Archivo).Path
$carpeta = Split-Path $ruta -Parent
$nombre = Split-Path $ruta -Leaf

# El archivo se monta en el contenedor: asi no pasa por PowerShell y no se rompen tildes ni enies
docker run --rm --env-file $envFile -v "${carpeta}:/respaldos" mysql:8.4 sh -c "mysql -h `$NUBE_HOST -P `$NUBE_PUERTO -u `$NUBE_USUARIO --ssl-mode=REQUIRED --default-character-set=utf8mb4 `$NUBE_BD < /respaldos/$nombre"
if ($LASTEXITCODE -ne 0) { throw 'Fallo la importacion. No sigas: avisa al humano con el mensaje de error.' }

Write-Host "OK: $nombre importado en la nube." -ForegroundColor Green
