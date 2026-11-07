#!/usr/bin/env bash
# Compatible con Bash 3.2, incluido en macOS; no requiere PowerShell ni jq.
set -euo pipefail

raiz="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
proyecto='tallermecanico'
modo="${1:-ayuda}"
accion="${2:-up}"

fallar() {
    printf '%s\n' "$*" >&2
    exit 1
}

mostrar_ayuda() {
    cat <<'AYUDA'
Uso principal en Windows y macOS: dotnet msbuild -t:COMANDO

  NubeUp / NubeDown / NubeStatus / NubeLogs / NubeBuild

Atajos: bash ./taller.sh nube <comando>

  status     Mostrar los contenedores de la aplicacion.
  logs       Seguir los logs de app1; Ctrl+C para salir.
  build      Reconstruir la imagen sin detener la aplicacion.
  usuarios   Crear cuentas interactivamente desde tu propia terminal.

up construye primero y actualiza la aplicacion conectada a Aiven.
Ningun comando elimina volumenes.
Aplicacion: http://127.0.0.1:8088
AYUDA
}

invocar_docker() {
    docker "$@" || fallar 'Docker no pudo completar la operacion. Revisa el mensaje anterior.'
}

invocar_compose() {
    invocar_docker compose --project-directory "$raiz" -f "$raiz/docker-compose.yml" "$@"
}

comprobar_docker() {
    command -v docker >/dev/null 2>&1 || fallar 'Instala Docker Desktop antes de ejecutar este comando.'
    local tipo_servidor
    tipo_servidor="$(invocar_docker info --format '{{.OSType}}')"
    [[ "$tipo_servidor" == 'linux' ]] || fallar 'Abre Docker Desktop y selecciona el modo de contenedores Linux.'
    [[ -z "${COMPOSE_PROJECT_NAME:-}" ]] || fallar 'Quita COMPOSE_PROJECT_NAME de esta terminal para usar los proyectos del repositorio.'
}

comprobar_configuracion() {
    [[ -f "$raiz/.env" ]] || fallar 'Copia .env.example como .env y completa la conexion de Aiven; consulta README.md.'
    if [[ "${1:-}" == 'certificados' ]]; then
        local certificado
        for certificado in localhost.pem localhost-key.pem; do
            [[ -f "$raiz/nginx/certs/$certificado" ]] || fallar 'Faltan certificados locales. Generalos con mkcert siguiendo README.md.'
        done
    fi
    invocar_compose config --quiet
    local nombre_proyecto
    # Compose necesita interpolar la IP antes de analizar los puertos.
    # El filtro solo captura el nombre; la configuración y sus secretos no se imprimen.
    nombre_proyecto="$(invocar_compose config | sed -n 's/^name: //p')" ||
        fallar 'No se pudo comprobar el nombre del proyecto Compose.'
    [[ "$nombre_proyecto" == "$proyecto" ]] || fallar 'El nombre del proyecto Compose fue cambiado. Quita COMPOSE_PROJECT_NAME de .env y de esta terminal.'
}

obtener_contenedores() {
    local filas id servicio nombre estado imagen
    filas="$(invocar_docker ps --all --filter "label=com.docker.compose.project=$proyecto" --format '{{.ID}}|{{.Label "com.docker.compose.service"}}|{{.Names}}|{{.State}}|{{.Image}}')" || return $?
    while IFS='|' read -r id servicio nombre estado imagen; do
        case "$servicio" in
            app1|app2|app3|nginx)
                printf '%s|%s|%s|%s|%s\n' "$id" "$servicio" "$nombre" "$estado" "$imagen"
                ;;
        esac
    done <<< "$filas"
}

detener_entorno() {
    local filas id servicio nombre estado imagen
    local activos=()
    local cantidad=0
    filas="$(obtener_contenedores)"
    while IFS='|' read -r id servicio nombre estado imagen; do
        case "$estado" in
            running|restarting|paused)
                activos+=("$id")
                cantidad=$((cantidad + 1))
                if [[ "$estado" == 'paused' ]]; then
                    invocar_docker unpause "$id" >/dev/null
                fi
                ;;
        esac
    done <<< "$filas"
    if [[ "$cantidad" -gt 0 ]]; then
        printf '%s\n' 'Apagando la aplicacion...'
        invocar_docker stop "${activos[@]}" >/dev/null
    fi
}

mostrar_estado() {
    local filas id servicio nombre estado imagen
    filas="$(obtener_contenedores)"
    printf '%-10s %-32s %-12s %s\n' 'Servicio' 'Nombre' 'Estado' 'Imagen'
    while IFS='|' read -r id servicio nombre estado imagen; do
        [[ -n "$id" ]] || continue
        printf '%-10s %-32s %-12s %s\n' "$servicio" "$nombre" "$estado" "$imagen"
    done <<< "$filas"
}

esperar_web() {
    local direccion="$1" codigo limite=$((SECONDS + 90))
    while (( SECONDS < limite )); do
        if codigo="$(curl --silent --location --output /dev/null --write-out '%{http_code}' --connect-timeout 5 --max-time 5 "$direccion")" && [[ "$codigo" == '200' ]]; then
            return
        fi
        sleep 2
    done
    fallar 'La aplicacion no estuvo lista a tiempo. Consulta dotnet msbuild -t:NubeLogs.'
}

[[ $# -le 2 ]] || fallar 'Uso: bash ./taller.sh nube <up|down|status|logs|build|usuarios|ayuda>'
case "$modo" in nube|ayuda) ;; *) fallar "Modo no valido: $modo. Usa nube o ayuda." ;; esac
case "$accion" in up|down|status|logs|build|usuarios|ayuda) ;; *) fallar "Accion no valida: $accion." ;; esac

if [[ "$modo" == 'ayuda' || "$accion" == 'ayuda' ]]; then
    mostrar_ayuda
    exit 0
fi

cd -- "$raiz"
comprobar_docker
case "$accion" in
    up)
        comprobar_configuracion certificados
        command -v curl >/dev/null 2>&1 || fallar 'Se requiere curl para comprobar que la aplicacion este lista.'
        printf '%s\n' 'Construyendo la imagen...'
        invocar_compose build app1
        invocar_compose up -d --no-build --pull missing
        printf '%s\n' 'Esperando a que la aplicacion este lista...'
        for puerto in 5000 5001 5002 8088; do
            esperar_web "http://127.0.0.1:$puerto/Auth/Login"
        done
        printf '%s\n' 'Entorno nube listo: http://127.0.0.1:8088'
        ;;
    down)
        detener_entorno
        printf '%s\n' 'Entorno nube apagado; contenedores y datos conservados.'
        ;;
    status) mostrar_estado ;;
    logs)
        comprobar_configuracion
        invocar_compose logs --follow --tail 80 app1
        ;;
    build)
        comprobar_configuracion
        invocar_compose build app1
        ;;
    usuarios)
        comprobar_configuracion
        invocar_compose exec app1 dotnet TallerMecanico.Frontend.dll --inicializar-usuarios
        ;;
esac
