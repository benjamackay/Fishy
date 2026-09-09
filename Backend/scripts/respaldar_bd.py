r"""Respalda los datos de la base a un archivo, fuera del repositorio.

**Por que existe:** los `.bundle` de `~/respaldos-fishy` respaldan el CODIGO. Los
datos que generan los ninos jugando -partidas, decisiones, progreso, inventario-
no estan en ningun archivo del repo, y el plan gratuito de Supabase no da
respaldos que uno pueda restaurar. Sin esto, un `migrate` mal puesto o un drop de
tablas se los lleva sin vuelta: ya se dropearon todas las tablas dos veces en
este proyecto (2026-08-09 y 2026-08-12), y las dos veces no habia nada que
perder. Hoy si lo hay.

**Que respalda y que no.** Solo los DATOS de la app `api`, en formato `dumpdata`.
El esquema no va y no hace falta: lo rehacen las migraciones, que estan
versionadas. Por eso el `.info.txt` que acompana cada respaldo anota en que
migracion quedo la base: restaurar contra un esquema distinto es la unica forma
de que esto falle en silencio.

**Los tokens de sesion se excluyen a proposito.** `authtoken.Token` son
credenciales vivas: dejarlas en un JSON en el disco es peor que perderlas, y no
se pierde nada -las cuentas se restauran con su hash de contrasena, asi que el
adulto vuelve a entrar con la misma clave-.

**El archivo lleva datos personales de menores** (nombre, edad) y hashes de
contrasena. Por eso el destino por defecto esta FUERA del repo, en
`~/respaldos-fishy`, junto a los bundles. No lo muevas adentro.

Uso (desde Backend/):
    .\run.ps1 --respaldo                    # lo normal
    .\.venv\Scripts\python .\scripts\respaldar_bd.py --destino D:\otra\carpeta
    .\.venv\Scripts\python .\scripts\respaldar_bd.py --conservar 20

Para restaurar, ver `~/respaldos-fishy/COMO_RESTAURAR.md`.

Sale con codigo 1 si el respaldo no se pudo verificar. No sirve un respaldo que
nadie comprobo: el modo de falla que importa es el archivo de 2 bytes que se
descubre el dia que se necesita.
"""
import argparse
import glob
import io
import json
import os
import subprocess
import sys
from collections import Counter
from datetime import datetime

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESTINO_POR_DEFECTO = os.path.join(os.path.expanduser("~"), "respaldos-fishy")

# Modelos que, si vienen en cero, significan que algo salio mal y no que la base
# este vacia: son catalogo, los carga `cargar_banco` y siempre tienen filas.
IMPRESCINDIBLES = ["api.preguntabanco", "api.opcionbanco"]


def cargar_env(ruta):
    """Mete el .env en os.environ. Igual que hace run.ps1 antes de arrancar."""
    if not os.path.exists(ruta):
        return
    with io.open(ruta, encoding="utf-8-sig") as f:
        for linea in f:
            linea = linea.strip()
            if not linea or linea.startswith("#") or "=" not in linea:
                continue
            clave, valor = linea.split("=", 1)
            os.environ.setdefault(clave.strip(), valor.strip().strip('"').strip("'"))


def arrancar_django():
    cargar_env(os.path.join(RAIZ, ".env"))
    sys.path.insert(0, os.path.join(RAIZ, "backend"))
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "juego_backend.settings")
    import django

    django.setup()


def commit_actual():
    try:
        salida = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            cwd=RAIZ, capture_output=True, text=True, timeout=15,
        )
        return salida.stdout.strip() or "desconocido"
    except Exception:
        return "desconocido"


def contar_en_la_base():
    """Filas por modelo, preguntandole al ORM."""
    from django.apps import apps

    cuentas = {}
    for modelo in apps.get_app_config("api").get_models():
        etiqueta = f"api.{modelo._meta.model_name}"
        cuentas[etiqueta] = modelo.objects.count()
    return cuentas


def contar_en_el_archivo(ruta):
    with io.open(ruta, encoding="utf-8") as f:
        filas = json.load(f)
    return Counter(fila["model"] for fila in filas), len(filas)


def podar(destino, conservar):
    """Deja solo los `conservar` respaldos mas nuevos."""
    viejos = sorted(glob.glob(os.path.join(destino, "fishy-bd-*.json")))
    sobran = viejos[:-conservar] if conservar > 0 else []
    for ruta in sobran:
        os.remove(ruta)
        info = ruta.replace(".json", ".info.txt")
        if os.path.exists(info):
            os.remove(info)
        print(f"  borrado por antiguedad: {os.path.basename(ruta)}")
    return len(sobran)


def main():
    ap = argparse.ArgumentParser(description="Respalda los datos de la base de Fishy!")
    ap.add_argument("--destino", default=DESTINO_POR_DEFECTO,
                    help=f"carpeta donde dejarlo (por defecto {DESTINO_POR_DEFECTO})")
    ap.add_argument("--salida", default=None, help="ruta exacta del .json, ignora --destino")
    ap.add_argument("--conservar", type=int, default=10,
                    help="cuantos respaldos mantener; 0 = no borrar ninguno")
    args = ap.parse_args()

    arrancar_django()

    from django.conf import settings
    from django.core.management import call_command
    from django.db.migrations.recorder import MigrationRecorder

    bd = settings.DATABASES["default"]
    sello = datetime.now().strftime("%Y-%m-%d_%H%M")
    destino = os.path.dirname(args.salida) if args.salida else args.destino
    ruta = args.salida or os.path.join(destino, f"fishy-bd-{sello}.json")
    os.makedirs(destino, exist_ok=True)

    print(f"Respaldando {bd['NAME']} en {bd['HOST']}")
    print(f"  -> {ruta}")

    antes = contar_en_la_base()
    total_en_bd = sum(antes.values())
    if total_en_bd == 0:
        print("\nLa base no tiene ni una fila. No se escribe nada: un respaldo vacio")
        print("pisando a uno bueno es la forma mas rapida de perderlo todo.")
        return 1

    # `authtoken` no se incluye a proposito (ver el docstring). `api` es la unica
    # app con datos propios: el esquema y los permisos los rehacen las migraciones.
    #
    # El archivo se abre aqui y se pasa como `stdout`, en vez de usar `output=`:
    # esa opcion abre el archivo con la codificacion del sistema, que en Windows
    # es cp1252, y el banco trae emoji. Con `output=` esto reventaba con
    # UnicodeEncodeError sobre el 📱 de una pregunta.
    try:
        with io.open(ruta, "w", encoding="utf-8") as f:
            call_command("dumpdata", "api", indent=2, stdout=f)
    except Exception as e:
        # Si revienta a mitad queda un JSON truncado, sin su `.info.txt`, con cara
        # de respaldo bueno. Se borra: un hueco se nota, un archivo a medias no.
        if os.path.exists(ruta):
            os.remove(ruta)
        print(f"\nFallo el volcado y se borro el archivo a medias:\n  {e}")
        return 1

    # ── Verificar. Un respaldo sin comprobar no es un respaldo ────────────────
    if not os.path.exists(ruta) or os.path.getsize(ruta) < 3:
        print("\nEl archivo quedo vacio o no se escribio.")
        return 1

    en_archivo, filas = contar_en_el_archivo(ruta)

    print(f"\n{'modelo':<34}{'en la base':>12}{'respaldado':>12}")
    problemas = []
    for etiqueta in sorted(antes):
        esperado, guardado = antes[etiqueta], en_archivo.get(etiqueta, 0)
        marca = "" if esperado == guardado else "   <<< NO CALZA"
        if esperado != guardado:
            problemas.append(f"{etiqueta}: {esperado} en la base, {guardado} en el archivo")
        if etiqueta in IMPRESCINDIBLES and guardado == 0:
            problemas.append(f"{etiqueta} quedo en cero y es catalogo: revisa la carga del banco")
        print(f"{etiqueta:<34}{esperado:>12}{guardado:>12}{marca}")

    ultima = MigrationRecorder.Migration.objects.filter(app="api").order_by("-id").first()
    migracion = ultima.name if ultima else "ninguna"
    tamano = os.path.getsize(ruta)

    print(f"\n{'total':<34}{total_en_bd:>12}{filas:>12}")
    print(f"tamano: {tamano / 1024:.1f} KB   migracion: {migracion}")

    if problemas:
        print("\nEl respaldo NO quedo bien:")
        for p in problemas:
            print(f"  - {p}")
        return 1

    # El companero de cada respaldo: sin saber contra que migracion se tomo, una
    # restauracion puede entrar en una base con otro esquema y fallar callada.
    info = ruta.replace(".json", ".info.txt")
    with io.open(info, "w", encoding="utf-8") as f:
        f.write(f"Respaldo de datos de Fishy!\n")
        f.write(f"fecha        : {datetime.now():%Y-%m-%d %H:%M:%S}\n")
        f.write(f"base         : {bd['NAME']} en {bd['HOST']}\n")
        f.write(f"migracion    : {migracion}\n")
        f.write(f"commit       : {commit_actual()}\n")
        f.write(f"objetos      : {filas}\n")
        f.write(f"tamano       : {tamano} bytes\n")
        f.write(f"excluye      : authtoken (credenciales vivas), esquema (lo rehacen las migraciones)\n")
        f.write("\nfilas por modelo:\n")
        for etiqueta in sorted(en_archivo):
            f.write(f"  {etiqueta:<32}{en_archivo[etiqueta]}\n")
        f.write("\nRestaurar (contra una base ya migrada y VACIA):\n")
        f.write("  .\\.venv\\Scripts\\python .\\backend\\manage.py migrate\n")
        f.write(f"  .\\.venv\\Scripts\\python .\\backend\\manage.py loaddata \"{ruta}\"\n")

    borrados = podar(destino, args.conservar)
    print(f"\nListo. {filas} objetos verificados uno a uno contra la base.")
    print(f"Ficha: {os.path.basename(info)}")
    if borrados:
        print(f"Se conservan los {args.conservar} mas recientes.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
