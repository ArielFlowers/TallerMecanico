param(
    [Parameter(Position = 0)]
    [ValidateSet('nube', 'ayuda')]
    [string]$Modo = 'ayuda',
    [Parameter(Position = 1)]
    [ValidateSet('up', 'down', 'status', 'logs', 'build', 'usuarios', 'ayuda')]
    [string]$Accion = 'up'
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$proyecto = 'tallermecanico'
$servicios = @('redis', 'app1', 'app2', 'app3', 'nginx')

function Mostrar-Ayuda {
    Write-Host @'
Uso principal desde PowerShell: dotnet msbuild -t:COMANDO

  NubeUp / NubeDown / NubeStatus / NubeLogs / NubeBuild

Atajos: .\taller nube <comando>

  status     Mostrar los contenedores de la aplicacion.
  logs       Seguir los logs de app1; Ctrl+C para salir.
  build      Reconstruir la imagen sin detener la aplicacion.
  usuarios   Crear cuentas interactivamente desde tu propia terminal.

up construye primero y actualiza la aplicacion conectada a Aiven.
Ningun comando elimina volumenes.
Aplicacion: http://127.0.0.1:8088
'@
}

function Invocar-Docker([string[]]$ArgumentosDocker) {
    & docker @ArgumentosDocker
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker no pudo completar la operacion. Revisa el mensaje anterior.'
    }
}

function Invocar-Compose([string[]]$ArgumentosCompose) {
    Invocar-Docker (@('compose', '--project-directory', $raiz, '-f', (Join-Path $raiz 'docker-compose.yml')) + $ArgumentosCompose)
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

function Comprobar-Configuracion([switch]$Certificados) {
    if (-not (Test-Path -LiteralPath (Join-Path $raiz '.env'))) {
        throw 'Copia .env.example como .env y completa la conexion de Aiven; consulta README.md.'
    }
    if ($Certificados) {
        foreach ($certificado in @('localhost.pem', 'localhost-key.pem')) {
            if (-not (Test-Path -LiteralPath (Join-Path $raiz ('nginx\certs\' + $certificado)))) {
                throw 'Faltan certificados locales. Generalos con mkcert siguiendo docs/H1-HTTPS.md.'
            }
        }
    }
    Invocar-Compose @('config', '--quiet')
    $configuracionSinValores = @(Invocar-Compose @('config', '--no-interpolate', '--format', 'json'))
    try {
        $nombreProyecto = (($configuracionSinValores -join "`n") | ConvertFrom-Json).name
    } catch {
        throw 'No se pudo comprobar el nombre del proyecto Compose.'
    }
    if ($nombreProyecto -ne $proyecto) {
        throw 'El nombre del proyecto Compose fue cambiado. Quita COMPOSE_PROJECT_NAME de .env y de esta terminal.'
    }
}

function Obtener-Contenedores {
    $filtro = 'label=com.docker.compose.project=' + $proyecto
    $filas = @(Invocar-Docker @('ps', '--all', '--filter', $filtro, '--format', '{{json .}}'))
    foreach ($fila in $filas) {
        if ([string]::IsNullOrWhiteSpace($fila)) { continue }
        $contenedor = $fila | ConvertFrom-Json
        $etiqueta = [regex]::Match($contenedor.Labels, '(?:^|,)com\.docker\.compose\.service=([^,]+)')
        if ($etiqueta.Success -and $etiqueta.Groups[1].Value -in $servicios) {
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

function Detener-Entorno {
    $activos = @(Obtener-Contenedores | Where-Object { $_.Estado -in @('running', 'restarting', 'paused') })
    if ($activos.Count -eq 0) { return }
    Write-Host 'Apagando la aplicacion...'
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
    throw 'La aplicacion no estuvo lista a tiempo. Consulta .\taller nube logs.'
}

if ($Modo -eq 'ayuda' -or $Accion -eq 'ayuda') {
    Mostrar-Ayuda
    return
}
Push-Location $raiz
try {
    Comprobar-Docker
    switch ($Accion) {
        'up' {
            Comprobar-Configuracion -Certificados
            Write-Host 'Construyendo la imagen...'
            Invocar-Compose @('build', 'app1')
            Invocar-Compose @('up', '-d', '--no-build', '--pull', 'missing')
            Write-Host 'Esperando a que la aplicacion este lista...'
            foreach ($puerto in @(5000, 5001, 5002, 8088)) {
                Esperar-Web ('http://127.0.0.1:' + $puerto + '/Auth/Login')
            }
            Write-Host 'Entorno nube listo: http://127.0.0.1:8088' -ForegroundColor Green
        }
        'down' {
            Detener-Entorno
            Write-Host 'Entorno nube apagado; contenedores y datos conservados.'
        }
        'status' {
            Obtener-Contenedores | Format-Table Servicio, Nombre, Estado, Imagen -AutoSize
        }
        'logs' {
            Comprobar-Configuracion
            Invocar-Compose @('logs', '--follow', '--tail', '80', 'app1')
        }
        'build' {
            Comprobar-Configuracion
            Invocar-Compose @('build', 'app1')
        }
        'usuarios' {
            Comprobar-Configuracion
            Invocar-Compose @('exec', 'app1', 'dotnet', 'TallerMecanico.dll', '--inicializar-usuarios')
        }
    }
} finally {
    Pop-Location
}
