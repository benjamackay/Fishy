"""Prueba a mano los endpoints nuevos, contra un servidor de verdad.

    python scripts/probar_endpoints_nuevos.py                 # SQLite local (por defecto)
    python scripts/probar_endpoints_nuevos.py --url http://127.0.0.1:8000/api

Por qué existe teniendo 267 pruebas automáticas: aquéllas usan el cliente de
test de Django, que no pasa por HTTP, ni por el servidor, ni por las rutas
reales. Esto recorre lo mismo que va a recorrer Unity —token, URL, JSON— y sirve
para mirar las respuestas con los ojos cuando algo no calza.

**Por defecto levanta su propio servidor contra `local_db.sqlite3`**, no contra
Supabase: escribe partidas y chats de prueba, y esos no tienen por qué quedar en
la base que comparte el equipo. Con `--url` apunta a donde se le diga, bajo tu
responsabilidad.

Sale con código 1 si algo falla, así que sirve igual desde un script.
"""
import argparse
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MANAGE = os.path.join(RAIZ, "backend", "manage.py")
SETTINGS_LOCAL = "juego_backend.settings_local_sqlite"

ok_total = fallas_total = 0


def ok(msg):
    global ok_total
    ok_total += 1
    print(f"  [OK   ] {msg}")


def falla(msg):
    global fallas_total
    fallas_total += 1
    print(f"  [FALLA] {msg}")


def comprobar(condicion, msg):
    ok(msg) if condicion else falla(msg)


def pedir(url, metodo="GET", cuerpo=None, token=None):
    """Devuelve (codigo, datos). No revienta con 4xx: el código es parte de lo
    que se prueba."""
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(url, data=datos, method=metodo)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Token {token}")
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, json.loads(r.read() or b"null")
    except urllib.error.HTTPError as e:
        cuerpo_error = e.read()
        try:
            return e.code, json.loads(cuerpo_error or b"null")
        except json.JSONDecodeError:
            return e.code, cuerpo_error[:300].decode(errors="replace")


def python_del_venv():
    for c in (os.path.join(RAIZ, ".venv", "Scripts", "python.exe"),
              os.path.join(RAIZ, ".venv", "bin", "python")):
        if os.path.exists(c):
            return c
    return sys.executable


def preparar_base_local(py):
    print("-- Preparando la base local (SQLite) --")
    for args in (["migrate", "--no-input"], ["cargar_banco"], ["cargar_detective"]):
        r = subprocess.run([py, MANAGE, *args, f"--settings={SETTINGS_LOCAL}"],
                           capture_output=True, text=True,
                           encoding="utf-8", errors="replace")
        if r.returncode != 0:
            print(r.stdout, r.stderr)
            sys.exit(f"falló: manage.py {args[0]}")
        print(f"  {args[0]}: listo")


def levantar_servidor(py, puerto):
    proc = subprocess.Popen(
        [py, MANAGE, "runserver", f"127.0.0.1:{puerto}", "--noreload",
         f"--settings={SETTINGS_LOCAL}"],
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    base = f"http://127.0.0.1:{puerto}/api"
    for _ in range(40):
        try:
            urllib.request.urlopen(f"{base}/health/", timeout=2)
            return proc, base
        except Exception:
            time.sleep(0.5)
    proc.terminate()
    sys.exit("el servidor local no levantó")


def cuenta_de_prueba(base):
    """Una cuenta nueva por corrida: así no depende de lo que haya quedado antes."""
    sufijo = str(int(time.time()))
    codigo, datos = pedir(f"{base}/auth/registro/", "POST", {
        "nombre": f"prueba_{sufijo}",
        "email": f"prueba_{sufijo}@local.test",
        "password": "clave-de-prueba-123",
    })
    if codigo not in (200, 201):
        sys.exit(f"no pude crear la cuenta de prueba: {codigo} {datos}")
    token = datos["token"]

    _, jugador = pedir(f"{base}/jugadores/", "POST",
                       {"nombre": "Otto de prueba", "edad": 9}, token)
    codigo, partida = pedir(f"{base}/partidas/", "POST",
                            {"usuario_jugador_id": jugador["id"]}, token)
    if codigo not in (200, 201):
        sys.exit(f"no pude crear la partida de prueba: {codigo} {partida}")
    return token, partida["id"]


# ── Las pruebas ──────────────────────────────────────────────────────────────

def probar_catalogo(base):
    print("\n-- B.2 · Catálogo de misiones (sin sesión) --")
    codigo, datos = pedir(f"{base}/misiones/")
    comprobar(codigo == 200, f"GET /misiones/ sin token responde 200 (dio {codigo})")
    comprobar(isinstance(datos, list),
              "la raíz es un arreglo, que es lo que espera ApiManager")
    if not isinstance(datos, list):
        return
    print(f"         {len(datos)} misiones")

    con_objetivos = [m for m in datos if m["objetivos"]]
    comprobar(bool(con_objetivos), f"{len(con_objetivos)} misiones traen objetivos")
    sin_objetivos = [m for m in datos if not m["objetivos"]]
    comprobar(all(m["objetivos"] == [] for m in sin_objetivos),
              "las que no tienen mandan [] explícito, no omiten el campo")

    campos = ("item_id", "cantidad", "dialogo_id", "escenario_ids", "zona_id",
              "caso_id", "descripcion")
    completos = all(all(c in o for c in campos)
                    for m in datos for o in m["objetivos"])
    comprobar(completos, "los campos que no aplican a un tipo van vacíos, no ausentes")

    comprobar(all(m["titulo"] == m["nombre"] for m in datos),
              "`titulo` y `nombre` llegan los dos, para que Unity tome cualquiera")

    uno = datos[0]["mision_id"]
    codigo, detalle = pedir(f"{base}/misiones/{uno}/")
    comprobar(codigo == 200 and detalle["mision_id"] == uno,
              f"GET /misiones/{uno}/ devuelve esa misión")
    codigo, _ = pedir(f"{base}/misiones/NO_EXISTE/")
    comprobar(codigo == 404, f"un id inventado da 404 (dio {codigo})")


def probar_objetivos(base, token, partida_id):
    print("\n-- B.3 · Avance por objetivo --")
    ruta = f"{base}/partidas/{partida_id}/objetivos/"
    cuerpo = {"mision_id": "MISION_NPC_03", "orden": 1, "cumplido": True}

    codigo, datos = pedir(ruta, "POST", cuerpo, token)
    comprobar(codigo == 200 and datos.get("cumplido") is True,
              f"marcar un objetivo lo guarda (dio {codigo})")

    for _ in range(2):
        pedir(ruta, "POST", cuerpo, token)
    _, lista = pedir(ruta, token=token)
    comprobar(len(lista) == 1,
              f"repetir el mismo aviso 3 veces deja UNA fila (hay {len(lista)})")

    _, vuelta = pedir(ruta, "POST",
                      {**cuerpo, "cumplido": False}, token)
    comprobar(vuelta.get("cumplido") is True,
              "un `cumplido: false` posterior NO lo devuelve a pendiente")

    codigo, _ = pedir(ruta, "POST", {"mision_id": "MISION_NPC_03"}, token)
    comprobar(codigo == 400, f"sin `orden` responde 400 (dio {codigo})")

    codigo, _ = pedir(ruta, "POST",
                      {"mision_id": "NO_ESTA_EN_EL_CATALOGO", "orden": 7,
                       "cumplido": True}, token)
    comprobar(codigo == 200,
              "un objetivo fuera del catálogo se guarda igual, con aviso en el log")

    codigo, _ = pedir(ruta)
    comprobar(codigo == 401, f"sin token responde 401 (dio {codigo})")


def probar_chat_completo(base, token, partida_id):
    print("\n-- A.3 · Conversación completa en una petición --")
    ruta = f"{base}/partidas/{partida_id}/chats/completo/"
    conversacion = {
        "npc": {"nombre": "Alex", "area": "zona_2", "tipo": "enemigo", "confianza": 0},
        "chat": {"categoria_riesgo": "desconocidos"},
        "mensajes": [
            {"tipo": "start", "respuesta": "Hola!"},
            {"tipo": "request", "respuesta": "¿Me pasas tu dirección?",
             "pregunta_banco_id": "HDU2_NPC01_F2_Q01",
             "posibles_respuestas": [
                 {"texto": "Claro", "orden": 0, "calidad_respuesta": "mala"},
                 {"texto": "No", "orden": 1, "calidad_respuesta": "buena"}]},
            {"tipo": "chain", "respuesta": "No te la voy a dar",
             "calidad_respuesta": "buena",
             "opcion_banco_id": "HDU2_NPC01_F2_Q01_R2"},
        ],
        "finalizar": True,
    }

    inicio = time.time()
    codigo, datos = pedir(ruta, "POST", conversacion, token)
    tardo = time.time() - inicio
    comprobar(codigo == 201, f"una sola petición deja la conversación (dio {codigo})")
    if codigo != 201:
        print(f"         {datos}")
        return
    print(f"         tardó {tardo:.2f} s · la cadena larga son 5 peticiones")

    comprobar(len(datos["mensajes"]) == 4,
              f"3 mensajes + el END que cierra (llegaron {len(datos['mensajes'])})")
    comprobar(datos["chat"].get("fecha_termino") is not None,
              "el chat queda cerrado")

    _, guardados = pedir(f"{base}/chats/{datos['chat']['id']}/mensajes/", token=token)
    con_opcion = [m for m in guardados if m.get("opcion_banco_id")]
    comprobar(bool(con_opcion),
              "el `opcion_banco_id` se conserva, que es lo que usa el riesgo por zona")

    malo = json.loads(json.dumps(conversacion))
    malo["mensajes"][1]["tipo"] = "saludo_inventado"
    npcs_antes = len(pedir(f"{base}/partidas/{partida_id}/npcs/", token=token)[1])
    codigo, error = pedir(ruta, "POST", malo, token)
    npcs_despues = len(pedir(f"{base}/partidas/{partida_id}/npcs/", token=token)[1])
    comprobar(codigo == 400, f"un mensaje inválido responde 400 (dio {codigo})")
    comprobar(isinstance(error, dict) and "1" in (error.get("mensajes") or {}),
              "el error dice CUÁL de los mensajes viene mal")
    comprobar(npcs_antes == npcs_despues,
              "y no deja nada a medias: no se creó ni el NPC")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--url", default=None,
                    help="API ya corriendo, ej. http://127.0.0.1:8000/api. "
                         "Por defecto levanta una local contra SQLite.")
    ap.add_argument("--puerto", type=int, default=8123)
    args = ap.parse_args()

    print("=" * 78)
    print("  Endpoints nuevos · REQUISITOS_BD partes A.3, B.2 y B.3")
    print("=" * 78)

    proc = None
    if args.url:
        base = args.url.rstrip("/")
        print(f"Contra {base} (servidor ya corriendo)")
    else:
        py = python_del_venv()
        preparar_base_local(py)
        proc, base = levantar_servidor(py, args.puerto)
        print(f"Servidor local en {base} (SQLite, no toca Supabase)")

    try:
        token, partida_id = cuenta_de_prueba(base)
        probar_catalogo(base)
        probar_objetivos(base, token, partida_id)
        probar_chat_completo(base, token, partida_id)
    finally:
        if proc:
            proc.terminate()

    print("\n" + "=" * 78)
    print(f"  {ok_total} ok, {fallas_total} fallas")
    print("=" * 78)
    return 1 if fallas_total else 0


if __name__ == "__main__":
    sys.exit(main())
