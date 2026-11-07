# Comparte con la aplicacion MYSQL_CONNECTION de .env, resuelto por Compose.
function Invocar-ClienteNube {
    param(
        [string[]]$Montajes,
        [Parameter(Mandatory = $true)][string]$Comando
    )

    $raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $archivoEnv = Join-Path $raiz '.env'
    if (-not (Test-Path -LiteralPath $archivoEnv)) {
        throw 'Copia .env.example como .env y configura MYSQL_CONNECTION.'
    }

    # Compose interpreta comillas y $ igual que al iniciar la aplicacion.
    # Capturar el JSON en memoria: contiene secretos y no debe imprimirse.
    $json = @(& docker compose --project-directory $raiz --env-file $archivoEnv -f (Join-Path $raiz 'docker-compose.yml') config --format json 2>$null)
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer la configuracion de .env con Docker Compose.' }

    Add-Type -AssemblyName System.Data
    $conexion = New-Object System.Data.Common.DbConnectionStringBuilder
    try {
        $configuracion = ($json -join "`n") | ConvertFrom-Json
        # Usar el metodo: PowerShell trata las propiedades del builder como claves.
        $conexion.set_ConnectionString([string]$configuracion.services.'servicio-ordenes'.environment.ConnectionStrings__MySqlConnection)
    } catch {
        throw 'MYSQL_CONNECTION de .env no es una cadena de conexion valida.'
    }

    function Leer-Campo([string[]]$Nombres, [string]$Predeterminado = '') {
        foreach ($nombre in $Nombres) {
            if ($conexion.ContainsKey($nombre)) { return [string]$conexion[$nombre] }
        }
        return $Predeterminado
    }

    $variables = @{
        NUBE_HOST = Leer-Campo @('Server', 'Host', 'Data Source', 'DataSource', 'Address', 'Addr', 'Network Address')
        NUBE_PUERTO = Leer-Campo @('Port') '3306'
        NUBE_USUARIO = Leer-Campo @('User ID', 'UserID', 'Username', 'User name', 'User', 'Uid')
        NUBE_BD = Leer-Campo @('Database', 'Initial Catalog')
        MYSQL_PWD = Leer-Campo @('Password', 'Pwd')
    }
    foreach ($nombre in $variables.Keys) {
        if ([string]::IsNullOrWhiteSpace($variables[$nombre])) {
            throw 'MYSQL_CONNECTION debe incluir servidor, usuario, base y clave.'
        }
    }
    $puerto = 0
    if (-not [int]::TryParse($variables.NUBE_PUERTO, [ref]$puerto) -or $puerto -lt 1 -or $puerto -gt 65535) {
        throw 'El puerto de MYSQL_CONNECTION debe estar entre 1 y 65535.'
    }

    $anteriores = @{}
    try {
        $argumentosCliente = @('run', '--rm')
        foreach ($nombre in $variables.Keys) {
            $anteriores[$nombre] = [Environment]::GetEnvironmentVariable($nombre, 'Process')
            [Environment]::SetEnvironmentVariable($nombre, $variables[$nombre], 'Process')
            # Docker hereda el valor; la clave no aparece en sus argumentos.
            $argumentosCliente += @('--env', $nombre)
        }
        $argumentosCliente += $Montajes
        $argumentosCliente += @('mysql:8.4', 'sh', '-c', $Comando)
        & docker @argumentosCliente
        if ($LASTEXITCODE -ne 0) { throw 'Fallo el cliente MySQL. Revisa la conexion y los permisos.' }
    } finally {
        foreach ($nombre in $anteriores.Keys) {
            [Environment]::SetEnvironmentVariable($nombre, $anteriores[$nombre], 'Process')
        }
    }
}
