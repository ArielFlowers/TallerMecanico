param(
    [Parameter(Position = 0)]
    [ValidateSet('local', 'nube', 'ayuda')]
    [string]$Modo = 'ayuda',
    [Parameter(Position = 1)]
    [ValidateSet('up', 'down', 'seed', 'status', 'logs', 'build', 'usuarios', 'ayuda')]
    [string]$Accion = 'up'
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$proyectos = @{ local = 'tallermecanico-local-84'; nube = 'tallermecanico' }
$servicios = @{
    local = @('mysql', 'mailpit', 'redis', 'app1', 'app2', 'app3', 'nginx')
    nube = @('redis', 'app1', 'app2', 'app3', 'nginx')
}

function Mostrar-Ayuda {
    Write-Host @'
Uso desde PowerShell: .\taller <local|nube> <comando>

  .\taller local up        Construir y levantar el entorno local.
  .\taller local seed      Cargar datos_demo.sql en MySQL local.
  .\taller local down      Apagar el entorno local, conservando los datos.
  .\taller nube up         Construir y levantar la app conectada a Aiven.
  .\taller nube down       Apagar ese entorno, conservando los datos.

En ambos modos: status, logs, build y usuarios.
  status     Mostrar los contenedores del modo elegido.
  logs       Seguir los logs de app1; Ctrl+C para salir.
  build      Reconstruir la imagen sin cambiar el modo activo.
  usuarios   Crear cuentas interactivamente desde tu propia terminal.

up detiene el otro modo antes de arrancar. Ningun comando elimina volumenes.
El seed se permite solo en local y no incluye cuentas de acceso.
Aplicacion: http://127.0.0.1:8088
Correo local (Mailpit): http://127.0.0.1:8025
'@
}

function Invocar-Docker([string[]]$ArgumentosDocker) {
    & docker @ArgumentosDocker
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker no pudo completar la operacion. Revisa el mensaje anterior.'
    }
}

function Obtener-ArgumentosCompose([string]$ModoCompose) {
    $argumentos = @('compose', '--project-directory', $raiz)
    if ($ModoCompose -eq 'local') {
        $argumentos += @('-f', (Join-Path $raiz 'docker-compose.local.yml'))
    } else {
        $argumentos += @('-f', (Join-Path $raiz 'docker-compose.yml'), '-f', (Join-Path $raiz 'docker-compose.build.yml'))
    }
    return $argumentos
}

function Invocar-Compose([string]$ModoCompose, [string[]]$ArgumentosCompose) {
    Invocar-Docker ((Obtener-ArgumentosCompose $ModoCompose) + $ArgumentosCompose)
}

function Comprobar-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Instala Docker Desktop antes de ejecutar este comando.'
    }
    $tipoServidor = Invocar-Docker @('info', '--format', '{{.OSType}}')
    if ($tipoServidor -ne 'linux') {
        throw 'Abre Docker Desktop y selecciona el modo de contenedores Linux.'
    }
    if ($env:COMPOSE_PROJECT_NAME) {
        throw 'Quita COMPOSE_PROJECT_NAME de esta terminal para usar los proyectos del repositorio.'
    }
}

function Comprobar-Configuracion([string]$ModoCompose, [switch]$Certificados) {
    if ($ModoCompose -eq 'nube' -and -not (Test-Path -LiteralPath (Join-Path $raiz '.env'))) {
        throw 'Copia .env.example como .env y completa la conexion de Aiven; consulta docs/BD_NUBE.md.'
    }
    if ($Certificados) {
        foreach ($certificado in @('localhost.pem', 'localhost-key.pem')) {
            if (-not (Test-Path -LiteralPath (Join-Path $raiz ('nginx\certs\' + $certificado)))) {
                throw 'Faltan certificados locales. Generalos con mkcert siguiendo docs/H1-HTTPS.md.'
            }
        }
    }
    Invocar-Compose $ModoCompose @('config', '--quiet')
    $configuracionSinValores = @(Invocar-Compose $ModoCompose @('config', '--no-interpolate', '--format', 'json'))
    try {
        $nombreProyecto = (($configuracionSinValores -join "`n") | ConvertFrom-Json).name
    } catch {
        throw 'No se pudo comprobar el nombre del proyecto Compose.'
    }
    if ($nombreProyecto -ne $proyectos[$ModoCompose]) {
        throw 'El nombre del proyecto Compose fue cambiado. Quita COMPOSE_PROJECT_NAME de .env y de esta terminal.'
    }
}

function Obtener-Contenedores([string]$ModoContenedores) {
    $filtro = 'label=com.docker.compose.project=' + $proyectos[$ModoContenedores]
    $filas = @(Invocar-Docker @('ps', '--all', '--filter', $filtro, '--format', '{{json .}}'))
    foreach ($fila in $filas) {
        if ([string]::IsNullOrWhiteSpace($fila)) { continue }
        $contenedor = $fila | ConvertFrom-Json
        $etiqueta = [regex]::Match($contenedor.Labels, '(?:^|,)com\.docker\.compose\.service=([^,]+)')
        if ($etiqueta.Success -and $etiqueta.Groups[1].Value -in $servicios[$ModoContenedores]) {
            [pscustomobject]@{
                Id = $contenedor.ID
                Servicio = $etiqueta.Groups[1].Value
                Nombre = $contenedor.Names
                Estado = $contenedor.State
                Imagen = $contenedor.Image
            }
        }
    }
}

function Detener-Entorno([string]$ModoDetener) {
    $activos = @(Obtener-Contenedores $ModoDetener | Where-Object { $_.Estado -in @('running', 'restarting', 'paused') })
    if ($activos.Count -eq 0) { return }
    Write-Host ('Apagando el entorno ' + $ModoDetener + '...')
    foreach ($pausado in @($activos | Where-Object { $_.Estado -eq 'paused' })) {
        Invocar-Docker @('unpause', $pausado.Id) | Out-Null
    }
    Invocar-Docker (@('stop') + @($activos | ForEach-Object { $_.Id })) | Out-Null
}

function Esperar-Web([string]$Direccion) {
    $limite = [DateTime]::UtcNow.AddSeconds(90)
    do {
        try {
            $respuesta = Invoke-WebRequest -Uri $Direccion -UseBasicParsing -DisableKeepAlive -TimeoutSec 5
            if ($respuesta.StatusCode -eq 200) { return }
        } catch {
            # Durante el arranque el esquema y las replicas pueden seguir preparandose.
        }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $limite)
    throw ('La aplicacion no estuvo lista a tiempo. Consulta .\taller ' + $Modo + ' logs.')
}

if ($Modo -eq 'ayuda' -or $Accion -eq 'ayuda') {
    Mostrar-Ayuda
    return
}
if ($Accion -eq 'seed' -and $Modo -ne 'local') {
    throw 'El seed solo se carga en local. Usa .\taller local seed.'
}

Push-Location $raiz
try {
    Comprobar-Docker
    switch ($Accion) {
        'up' {
            Comprobar-Configuracion $Modo -Certificados
            Write-Host ('Construyendo la imagen para ' + $Modo + '...')
            Invocar-Compose $Modo @('build', 'app1')
            $otroModo = if ($Modo -eq 'local') { 'nube' } else { 'local' }
            Detener-Entorno $otroModo
            Invocar-Compose $Modo @('up', '-d', '--no-build', '--pull', 'missing')
            Write-Host 'Esperando a que la aplicacion este lista...'
            foreach ($puerto in @(5000, 5001, 5002, 8088)) {
                Esperar-Web ('http://127.0.0.1:' + $puerto + '/Auth/Login')
            }
            Write-Host ('Entorno ' + $Modo + ' listo: http://127.0.0.1:8088') -ForegroundColor Green
            if ($Modo -eq 'local') {
                Write-Host 'Datos de ejemplo: .\taller local seed | Correo local: http://127.0.0.1:8025'
            }
        }
        'down' {
            Detener-Entorno $Modo
            Write-Host ('Entorno ' + $Modo + ' apagado; contenedores y datos conservados.')
        }
        'status' {
            Obtener-Contenedores $Modo | Format-Table Servicio, Nombre, Estado, Imagen -AutoSize
        }
        'logs' {
            Comprobar-Configuracion $Modo
            Invocar-Compose $Modo @('logs', '--follow', '--tail', '80', 'app1')
        }
        'build' {
            Comprobar-Configuracion $Modo
            Invocar-Compose $Modo @('build', 'app1')
        }
        'seed' {
            $archivoSeed = Join-Path $raiz 'scripts\bd\seed\datos_demo.sql'
            if (-not (Test-Path -LiteralPath $archivoSeed)) {
                throw 'Falta scripts/bd/seed/datos_demo.sql. Solicita el seed revisado del equipo.'
            }
            $contenedoresLocales = @(Obtener-Contenedores 'local')
            $mysqlLocal = @($contenedoresLocales | Where-Object { $_.Servicio -eq 'mysql' -and $_.Estado -eq 'running' -and $_.Nombre -eq 'taller-mecanico-mysql-local' -and $_.Imagen -eq 'mysql:8.4' })
            $appLocal = @($contenedoresLocales | Where-Object { $_.Servicio -eq 'app1' -and $_.Estado -eq 'running' })
            if ($mysqlLocal.Count -ne 1 -or $appLocal.Count -ne 1) {
                throw 'Primero levanta el entorno local: .\taller local up'
            }
            Esperar-Web 'http://127.0.0.1:5000/Auth/Login'
            & (Join-Path $PSScriptRoot 'bd\aplicar-seed-local.ps1') -Contenedor $mysqlLocal[0].Nombre -BaseDatos 'taller_mecanico'
        }
        'usuarios' {
            Comprobar-Configuracion $Modo
            Invocar-Compose $Modo @('exec', 'app1', 'dotnet', 'TallerMecanico.dll', '--inicializar-usuarios')
        }
    }
} finally {
    Pop-Location
}
