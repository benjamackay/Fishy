#!/usr/bin/env bash
#
# Resuelve un conflicto de merge en una escena (o prefab/asset) de Unity.
#
#   ./resolver-escena.sh                       → lista lo que está en conflicto
#   ./resolver-escena.sh SampleScene           → intenta fusionar esa escena
#   ./resolver-escena.sh SampleScene --ours    → conflictos → se queda con HEAD
#   ./resolver-escena.sh SampleScene --theirs  → conflictos → se queda con la rama
#   ./resolver-escena.sh SampleScene --no-add  → fusiona pero no hace git add
#
# Acepta el nombre suelto, el nombre con extensión o la ruta completa.
#
# ── Por qué existe ────────────────────────────────────────────────────────────
#
# El .gitattributes declara `*.unity merge=unityyamlmerge`, pero el driver de git
# NO funciona: UnityYAMLMerge decide cómo fusionar **por la extensión del
# archivo**, y git le pasa temporales sin extensión (.merge_file_XXXX). Falla con
# "Don't know how to merge" y se rinde dejando la versión de HEAD tal cual — sin
# marcadores de conflicto y sin avisar de que no fusionó nada. Es fácil dar por
# bueno un merge que se comió la mitad del trabajo.
#
# Esto extrae las tres etapas del índice a ficheros CON la extensión correcta,
# que es lo único que le faltaba a la herramienta para hacer su trabajo.
#
# ── Dos trampas que este script ya tiene resueltas ────────────────────────────
#
#  1. El orden de los lados. UnityYAMLMerge espera <base> <left> <right>, donde
#     left = "theirs" y right = "mine". En git, "ours" (etapa 2) es HEAD y
#     "theirs" (etapa 3) es la rama que entra. O sea: left=etapa3, right=etapa2.
#     Invertirlos hace que -l/-r elijan justo el lado contrario, en silencio.
#
#  2. El premerge (-p). Resuelve conflictos por su cuenta y no siempre acierta:
#     con dos ramas añadiendo el mismo campo, se queda con el vacío. Aquí no se
#     usa: si hay conflicto se prefiere parar y preguntar.
#
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

rojo()  { printf '\033[31m%s\033[0m\n' "$*"; }
verde() { printf '\033[32m%s\033[0m\n' "$*"; }
ambar() { printf '\033[33m%s\033[0m\n' "$*"; }
gris()  { printf '\033[90m%s\033[0m\n' "$*"; }

# ── 1. La herramienta ─────────────────────────────────────────────────────────
# Se puede forzar con UNITY_YAML_MERGE=/ruta/a/UnityYAMLMerge
buscar_merge() {
  if [ -n "${UNITY_YAML_MERGE:-}" ]; then
    printf '%s\n' "$UNITY_YAML_MERGE"; return
  fi
  local c
  for c in \
    "$HOME"/Unity/Hub/Editor/*/Editor/Data/Tools/UnityYAMLMerge \
    /opt/unity/editors/*/Editor/Data/Tools/UnityYAMLMerge \
    /Applications/Unity/Hub/Editor/*/Unity.app/Contents/Tools/UnityYAMLMerge \
    "/c/Program Files/Unity/Hub/Editor"/*/Editor/Data/Tools/UnityYAMLMerge.exe
  do
    # `if` y no `[ ... ] && ...`: como última sentencia del bucle, un test que
    # falla en el último candidato devuelve 1 y `set -e` mata el script.
    if [ -x "$c" ]; then printf '%s\n' "$c"; fi
  done | sort -V | tail -1     # la versión más nueva que haya
}

MERGE_BIN="$(buscar_merge)"
if [ -z "$MERGE_BIN" ]; then
  rojo "No encuentro UnityYAMLMerge."
  gris "Indícalo a mano:"
  gris "  UNITY_YAML_MERGE=/ruta/a/Editor/Data/Tools/UnityYAMLMerge $0 $*"
  exit 1
fi

# ── 2. Qué está en conflicto ──────────────────────────────────────────────────
mapfile -t EN_CONFLICTO < <(git diff --name-only --diff-filter=U)

listar() {
  if [ ${#EN_CONFLICTO[@]} -eq 0 ]; then
    verde "No hay ningún archivo en conflicto."
  else
    ambar "Archivos en conflicto:"
    printf '  %s\n' "${EN_CONFLICTO[@]}"
    gris ""
    gris "Para resolver una escena:  $0 <nombre>"
  fi
}

if [ $# -eq 0 ]; then listar; exit 0; fi

# ── 3. Argumentos ─────────────────────────────────────────────────────────────
PEDIDO=""
LADO=""          # vacío = parar si hay conflictos
HACER_ADD=1

while [ $# -gt 0 ]; do
  case "$1" in
    --ours)    LADO="-r" ;;   # right = mine = HEAD  (ver trampa 1 arriba)
    --theirs)  LADO="-l" ;;   # left  = theirs = la rama que entra
    --no-add)  HACER_ADD=0 ;;
    -h|--help) sed -n '2,14p' "$0" | sed 's/^# \?//'; exit 0 ;;
    -*)        rojo "Opción desconocida: $1"; exit 1 ;;
    *)         PEDIDO="$1" ;;
  esac
  shift
done

[ -n "$PEDIDO" ] || { rojo "Falta el nombre de la escena."; listar; exit 1; }

# ── 4. Resolver el nombre contra la lista de conflictos ───────────────────────
ARCHIVO=""
for f in "${EN_CONFLICTO[@]}"; do
  base="$(basename "$f")"
  if [ "$f" = "$PEDIDO" ] || [ "$base" = "$PEDIDO" ] || [ "${base%.*}" = "$PEDIDO" ]; then
    ARCHIVO="$f"; break
  fi
done

if [ -z "$ARCHIVO" ]; then
  rojo "'$PEDIDO' no está en conflicto."
  listar
  exit 1
fi

EXT="${ARCHIVO##*.}"
NOMBRE="$(basename "$ARCHIVO")"

# ── 5. Sacar las tres etapas CON la extensión correcta ────────────────────────
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

BASE="$TMP/base.$EXT"      # etapa 1: ancestro común
OURS="$TMP/ours.$EXT"      # etapa 2: HEAD (la rama en la que estás)
THEIRS="$TMP/theirs.$EXT"  # etapa 3: la rama que estás fusionando
DEST="$TMP/merged.$EXT"

git show ":1:$ARCHIVO" > "$BASE"   2>/dev/null || { rojo "Sin etapa base: ¿conflicto de add/add?"; exit 1; }
git show ":2:$ARCHIVO" > "$OURS"   2>/dev/null || { rojo "Sin etapa 'ours'.";   exit 1; }
git show ":3:$ARCHIVO" > "$THEIRS" 2>/dev/null || { rojo "Sin etapa 'theirs'."; exit 1; }
cp "$OURS" "$DEST"

echo
gris "Archivo      $ARCHIVO"
gris "Herramienta  $MERGE_BIN"
gris "Líneas       base $(wc -l < "$BASE")  ·  ours(HEAD) $(wc -l < "$OURS")  ·  theirs(rama) $(wc -l < "$THEIRS")"
echo

# ── 6. Fusionar ───────────────────────────────────────────────────────────────
# Sin -p: el premerge resuelve conflictos solo y no siempre bien.
RC=0
SALIDA="$("$MERGE_BIN" merge -h ${LADO:+$LADO} --fallback none "$BASE" "$THEIRS" "$OURS" "$DEST" 2>&1)" || RC=$?

CONFLICTOS="$(printf '%s\n' "$SALIDA" | grep -E '^(Left|Right) ' || true)"

if [ -n "$CONFLICTOS" ]; then
  ambar "Conflictos encontrados:"
  printf '%s\n' "$CONFLICTOS" | sed 's/^/  /'
  echo
fi

if [ "$RC" -ne 0 ] && [ -z "$LADO" ]; then
  rojo "No se pudo fusionar sin elegir un lado. El archivo NO se ha tocado."
  gris ""
  gris "Mira los conflictos de arriba y decide. Después:"
  AQUI="$(git rev-parse --abbrev-ref HEAD)"
  [ "$AQUI" = "HEAD" ] && AQUI="$(git rev-parse --short HEAD)"   # HEAD desacoplado
  gris "  $0 $PEDIDO --ours     (te quedas con HEAD: $AQUI)"
  gris "  $0 $PEDIDO --theirs   (te quedas con la rama que entra)"
  gris ""
  gris "Ojo: eso aplica a TODOS los conflictos del archivo, no a uno."
  exit 2
fi

# ── 7. Comprobar antes de escribir ────────────────────────────────────────────
if grep -q '^<<<<<<<\|^>>>>>>>' "$DEST"; then
  rojo "El resultado tiene marcadores de conflicto. No lo escribo."
  exit 1
fi

if [ ! -s "$DEST" ]; then
  rojo "El resultado está vacío. No lo escribo."
  exit 1
fi

# ── 8. Instalarlo ─────────────────────────────────────────────────────────────
cp "$ARCHIVO" "$ARCHIVO.antes-de-resolver"   # por si acaso; bórralo cuando estés conforme
cp "$DEST" "$ARCHIVO"

verde "Fusionado: $NOMBRE  ($(wc -l < "$ARCHIVO") líneas)"
gris  "Copia previa en $NOMBRE.antes-de-resolver"

if [ -n "$LADO" ] && [ -n "$CONFLICTOS" ]; then
  ambar "Los conflictos se resolvieron con $([ "$LADO" = "-r" ] && echo "--ours (HEAD)" || echo "--theirs (la rama)")."
fi

if [ "$HACER_ADD" -eq 1 ]; then
  git add "$ARCHIVO"
  verde "Añadido al índice."
fi

# ── 9. Qué revisar ────────────────────────────────────────────────────────────
echo
gris "Comprueba que estén los cambios de los dos lados antes de commitear:"
gris "  git diff --cached -- $ARCHIVO"
gris ""
gris "Y ábrela en Unity una vez: que el YAML fusione no garantiza que la escena"
gris "cargue — un fileID que apunte a un objeto que el otro lado borró compila"
gris "igual y revienta al abrir."

RESTANTES="$(git diff --name-only --diff-filter=U | wc -l)"
if [ "$RESTANTES" -gt 0 ]; then
  echo
  ambar "Quedan $RESTANTES archivo(s) en conflicto:"
  git diff --name-only --diff-filter=U | sed 's/^/  /'
fi
