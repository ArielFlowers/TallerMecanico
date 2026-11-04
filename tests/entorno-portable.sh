#!/usr/bin/env bash
# Prueba los lanzadores en un clon aislado, sin Docker, red ni credenciales.
set -euo pipefail

raiz="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$raiz/tmp"
temporal="$(mktemp -d "$raiz/tmp/portabilidad.XXXXXX")"
limpiar() {
    # Solo eliminar el directorio absoluto creado por esta prueba.
    case "$temporal" in "$raiz"/tmp/portabilidad.*) rm -rf -- "$temporal" ;; esac
}
trap limpiar EXIT

export TALLER_PRUEBA_RAIZ="$temporal/Clon con espacios"
export TALLER_PRUEBA_LOG="$temporal/comandos.log"
mkdir -p "$TALLER_PRUEBA_RAIZ/scripts" "$TALLER_PRUEBA_RAIZ/nginx/certs" "$temporal/bin"
cp "$raiz/taller.sh" "$TALLER_PRUEBA_RAIZ/taller.sh"
cp "$raiz/scripts/entorno.sh" "$TALLER_PRUEBA_RAIZ/scripts/entorno.sh"
printf 'name: tallermecanico\n' > "$TALLER_PRUEBA_RAIZ/docker-compose.yml"
: > "$TALLER_PRUEBA_LOG"

cat > "$temporal/bin/docker" <<'DOCKER'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$TALLER_PRUEBA_LOG"
case "$1" in
    info)
        [[ "${TALLER_PRUEBA_DOCKER_ERROR:-0}" == 0 ]] || exit 23
        printf '%s\n' "${TALLER_PRUEBA_OS:-linux}"
        ;;
    ps)
        [[ "${TALLER_PRUEBA_PS_ERROR:-0}" == 0 ]] || exit 24
        [[ "${TALLER_PRUEBA_VACIO:-0}" == 0 ]] || exit 0
        printf '%s\n' \
            'activo|app1|taller-app1|running|taller:local' \
            'pausado|app2|taller-app2|paused|taller:local' \
            'reiniciando|redis|taller-redis|restarting|redis:alpine' \
            'apagado|nginx|taller-nginx|exited|nginx:alpine' \
            'ajeno|mysql|base-historica|running|mysql:8.4'
        ;;
    compose)
        shift
        [[ "$1" == '--project-directory' && "$2" == "$TALLER_PRUEBA_RAIZ" ]] || exit 25
        [[ "$3" == '-f' && "$4" == "$TALLER_PRUEBA_RAIZ/docker-compose.yml" ]] || exit 26
        [[ "$PWD" == "$TALLER_PRUEBA_RAIZ" ]] || exit 27
        shift 4
        case "$1" in
            config)
                [[ "${TALLER_PRUEBA_CONFIG_ERROR:-0}" == 0 ]] || exit 28
                if [[ "$2" == '--no-interpolate' ]]; then
                    printf 'name: %s\n' "${TALLER_PRUEBA_PROYECTO:-tallermecanico}"
                fi
                ;;
            build) [[ "${TALLER_PRUEBA_BUILD_ERROR:-0}" == 0 ]] || exit 29 ;;
            up|logs|exec) ;;
            *) exit 30 ;;
        esac
        ;;
    unpause|stop) ;;
    *) exit 31 ;;
esac
DOCKER

cat > "$temporal/bin/curl" <<'CURL'
#!/usr/bin/env bash
set -euo pipefail
printf 'curl %s\n' "$*" >> "$TALLER_PRUEBA_LOG"
# Simular una replica que aun no responde en el primer intento.
if [[ ! -f "$TALLER_PRUEBA_RAIZ/web-lista" ]]; then
    : > "$TALLER_PRUEBA_RAIZ/web-lista"
    exit 7
fi
printf '200'
CURL

printf '#!/usr/bin/env bash\nexit 0\n' > "$temporal/bin/sleep"
chmod +x "$temporal/bin/docker" "$temporal/bin/curl" "$temporal/bin/sleep"
export PATH="$temporal/bin:$PATH"
unset COMPOSE_PROJECT_NAME

ejecutar() { bash "$TALLER_PRUEBA_RAIZ/taller.sh" "$@"; }
fallar() { printf '%s\n' "$*" >&2; exit 1; }
exigir_log() { grep -F -- "$1" "$TALLER_PRUEBA_LOG" >/dev/null || fallar "No se ejecuto: $1"; }
esperar_fallo() {
    if "$@" > "$temporal/fallo.log" 2>&1; then
        fallar "Debio fallar: $*"
    fi
}

# La ayuda, el estado y el apagado no necesitan .env ni certificados.
ejecutar ayuda >/dev/null
[[ ! -s "$TALLER_PRUEBA_LOG" ]] || fallar 'La ayuda ejecuto Docker.'
ejecutar nube status > "$temporal/estado.log"
grep -F 'taller-app1' "$temporal/estado.log" >/dev/null
if grep -F 'base-historica' "$temporal/estado.log" >/dev/null; then
    fallar 'Se incluyo un servicio ajeno a la aplicacion.'
fi
ejecutar nube down >/dev/null
exigir_log 'unpause pausado'
exigir_log 'stop activo pausado reiniciando'
TALLER_PRUEBA_VACIO=1 ejecutar nube down >/dev/null
TALLER_PRUEBA_PS_ERROR=1 esperar_fallo ejecutar nube down
esperar_fallo ejecutar nube build

printf "MYSQL_CONNECTION='conexion-de-prueba'\n" > "$TALLER_PRUEBA_RAIZ/.env"
esperar_fallo ejecutar nube up
: > "$TALLER_PRUEBA_RAIZ/nginx/certs/localhost.pem"
: > "$TALLER_PRUEBA_RAIZ/nginx/certs/localhost-key.pem"

ejecutar nube up >/dev/null
exigir_log 'build app1'
exigir_log 'up -d --no-build --pull missing'
for puerto in 5000 5001 5002 8088; do
    exigir_log "http://127.0.0.1:$puerto/Auth/Login"
done
ejecutar nube build >/dev/null
ejecutar nube logs >/dev/null
exigir_log 'logs --follow --tail 80 app1'
ejecutar nube usuarios >/dev/null
exigir_log 'exec app1 dotnet TallerMecanico.dll --inicializar-usuarios'

TALLER_PRUEBA_DOCKER_ERROR=1 esperar_fallo ejecutar nube status
TALLER_PRUEBA_OS=windows esperar_fallo ejecutar nube status
TALLER_PRUEBA_PROYECTO=otro esperar_fallo ejecutar nube build
TALLER_PRUEBA_CONFIG_ERROR=1 esperar_fallo ejecutar nube up
TALLER_PRUEBA_BUILD_ERROR=1 esperar_fallo ejecutar nube up
COMPOSE_PROJECT_NAME=otro esperar_fallo ejecutar nube status
esperar_fallo ejecutar desconocido
esperar_fallo ejecutar nube desconocido
esperar_fallo ejecutar nube up extra

if grep -E '(^| )(down|rm|volume)( |$)' "$TALLER_PRUEBA_LOG" >/dev/null; then
    fallar 'Se intento eliminar contenedores o volumenes.'
fi
printf '%s\n' 'OK: lanzadores, rutas con espacios, comandos, espera HTTP y propagacion de errores.'
