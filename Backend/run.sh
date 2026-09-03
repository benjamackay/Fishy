#!/usr/bin/env bash
#
# Levanta el backend de Fishy! cargando el .env automáticamente.
#
#   ./run.sh                    → servidor en 127.0.0.1:8000
#   ./run.sh 0.0.0.0:8000       → escuchando en todas las interfaces
#   ./run.sh --global           → test global: todas las capas de una
#   ./run.sh --global --fase 5  → solo esa fase del test global
#   ./run.sh --smoke            → corre el smoke test end-to-end
#   ./run.sh --check            → verifica config y drift de migraciones
#
# Funciona en Git Bash (Windows) y en WSL/Linux: detecta solo qué intérprete usar.
#
set -euo pipefail

cd "$(dirname "$0")"   # siempre trabajar desde Backend/, pase lo que pase

rojo()  { printf '\033[31m%s\033[0m\n' "$*"; }
verde() { printf '\033[32m%s\033[0m\n' "$*"; }
gris()  { printf '\033[90m%s\033[0m\n' "$*"; }

# ── 1. El .env ────────────────────────────────────────────────────────────────
if [ ! -f .env ]; then
  rojo "No existe Backend/.env"
  gris "Cópialo de la plantilla y pon la contraseña de Supabase:"
  gris "    cp .env.example .env"
  exit 1
fi

# Se lee línea por línea en vez de hacer 'source': así un valor con \$, (, ) o
# espacios (típico en DJANGO_SECRET_KEY) no lo interpreta bash y rompe todo.
while IFS='=' read -r clave valor; do
  clave="${clave#"${clave%%[![:space:]]*}"}"   # sin espacios al inicio
  clave="${clave%"${clave##*[![:space:]]}"}"   # ni al final
  case "$clave" in ''|\#*) continue ;; esac
  valor="${valor%$'\r'}"                        # el .env viene de Windows: CRLF
  export "$clave=$valor"
done < .env

# ── 2. Qué Python usar ────────────────────────────────────────────────────────
if   [ -x ".venv/Scripts/python.exe" ]; then PY=".venv/Scripts/python.exe"; ENTORNO="venv de Windows"
elif [ -x ".venv/bin/python" ];         then PY=".venv/bin/python";         ENTORNO="venv del repo"
elif [ -x "$HOME/.venvs/fishy/bin/python" ]; then PY="$HOME/.venvs/fishy/bin/python"; ENTORNO="venv de Linux (~/.venvs/fishy)"
else
  rojo "No encontré ningún entorno virtual."
  gris "En Windows:  python -m venv .venv && .venv/Scripts/python -m pip install -r backend/requirements.txt"
  gris "En Linux:    python3 -m venv ~/.venvs/fishy && ~/.venvs/fishy/bin/pip install -r backend/requirements.txt"
  exit 1
fi

# ── 3. Aviso de IPv6 en WSL ───────────────────────────────────────────────────
# El host directo de Supabase solo publica AAAA (IPv6) y WSL no tiene IPv6, así
# que desde ahí la conexión falla con "Network is unreachable". La salida es el
# Session Pooler, que sí da IPv4.
if grep -qi microsoft /proc/version 2>/dev/null && [[ "${DB_HOST:-}" == db.*.supabase.co ]]; then
  rojo "Aviso: estás en WSL y DB_HOST es el host directo de Supabase (solo IPv6)."
  gris "WSL no tiene IPv6, así que la conexión va a fallar. Usa el Session Pooler:"
  gris "    export DB_HOST=aws-0-<region>.pooler.supabase.com"
  gris "    export DB_USER=postgres.<project-ref>    # ojo: el usuario también cambia"
  gris "    export DB_PORT=5432                      # session mode, no 6543"
  gris "Sácalos de Supabase → Project Settings → Database → Session pooler."
  echo
fi

gris "Usando $ENTORNO · BD en ${DB_HOST:-?}:${DB_PORT:-?}"

# ── 4. Qué hacer ──────────────────────────────────────────────────────────────
case "${1:-}" in
  --global)
    verde "Test global: todas las capas (levanta y baja el servidor solo)"
    exec "$PY" scripts/test_global.py "${@:2}"
    ;;
  --smoke)
    verde "Smoke test end-to-end (necesita el servidor corriendo en otra terminal)"
    exec "$PY" scripts/smoke_test.py "${@:2}"
    ;;
  --check)
    "$PY" backend/manage.py check
    exec "$PY" backend/manage.py makemigrations --check --dry-run
    ;;
  *)
    DIR="${1:-127.0.0.1:8000}"
    verde "Servidor en http://$DIR/api/  ·  Ctrl+C para detener"
    exec "$PY" backend/manage.py runserver "$DIR"
    ;;
esac
