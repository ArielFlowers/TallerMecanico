#!/usr/bin/env bash
set -euo pipefail

directorio="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec bash "$directorio/scripts/entorno.sh" "$@"
