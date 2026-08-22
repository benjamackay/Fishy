r"""Smoke test end-to-end del backend contra Supabase.

Recorre el flujo completo de control parental: health, registro y login del
adulto responsable, creación de perfiles de menores, partida, NPC, chat,
mensajes, banco de preguntas, y comprueba que un adulto no pueda ver ni tocar
los datos de otro. Al final borra los datos de prueba que creó.

Uso (con el servidor corriendo en otra terminal):
    .\.venv\Scripts\python .\scripts\smoke_test.py
    .\.venv\Scripts\python .\scripts\smoke_test.py --base http://127.0.0.1:8000/api
    .\.venv\Scripts\python .\scripts\smoke_test.py --no-limpiar   (deja los datos)

Sale con código 1 si alguna comprobación falla.
"""
import argparse
import json
import sys
import time
import urllib.error
import urllib.request

PWD = "clave-de-prueba-123"

ok_count = 0
fail_count = 0
base_url = ""


def req(metodo, ruta, body=None, token=None, espera=200):
    """Hace una request y comprueba el código de estado. Devuelve el JSON."""
    global ok_count, fail_count
    data = json.dumps(body).encode() if body is not None else None
    r = urllib.request.Request(base_url + ruta, data=data, method=metodo)
    r.add_header("Content-Type", "application/json")
    if token:
        r.add_header("Authorization", f"Token {token}")

    t0 = time.time()
    try:
        with urllib.request.urlopen(r, timeout=30) as resp:
            code, raw = resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        code, raw = e.code, e.read().decode()
    except urllib.error.URLError as e:
        print(f"\n  No se pudo conectar a {base_url}: {e.reason}")
        print("  ¿Está corriendo el servidor? -> manage.py runserver")
        sys.exit(1)
    ms = (time.time() - t0) * 1000

    try:
        payload = json.loads(raw) if raw else None
    except json.JSONDecodeError:
        payload = raw[:200]

    if code == espera:
        ok_count += 1
        marca = "OK   "
    else:
        fail_count += 1
        marca = "FALLA"
    print(f"  [{marca}] {metodo:6s} {ruta:45s} -> {code} (esperado {espera}) {ms:6.0f} ms")
    if code != espera:
        print(f"          respuesta: {str(payload)[:300]}")
    return payload


def main():
    global base_url
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", default="http://127.0.0.1:8000/api",
                        help="URL base de la API")
    parser.add_argument("--no-limpiar", action="store_true",
                        help="no borrar los datos de prueba al terminar")
    args = parser.parse_args()
    base_url = args.base.rstrip("/")

    sufijo = str(int(time.time()))
    user_a, user_b = f"smoke_a_{sufijo}", f"smoke_b_{sufijo}"

    print("=" * 78)
    print(f"SMOKE TEST END-TO-END  ·  {base_url}")
    print("=" * 78)

    print("\n-- Salud y seguridad --")
    req("GET", "/health/", espera=200)
    req("GET", "/partidas/1/", espera=401)                      # sin token
    req("GET", "/banco/preguntas/", token="token-basura", espera=401)

    print("\n-- Auth (adulto responsable) --")
    reg = req("POST", "/auth/registro/",
              {"nombre": user_a, "email": f"{user_a}@ejemplo.cl", "password": PWD,
               "apellido": "Prueba", "edad": 38},
              espera=201)
    tok_a = reg["token"]
    login = req("POST", "/auth/login/", {"nombre": user_a, "password": PWD}, espera=200)
    if login["token"] != tok_a:
        print("  [FALLA] el token de login no coincide con el de registro")
    req("POST", "/auth/login/", {"nombre": user_a, "password": "clave-mala"}, espera=401)
    req("GET", "/auth/perfil/", token=tok_a, espera=200)
    # El email es único: no se puede registrar dos veces
    req("POST", "/auth/registro/",
        {"nombre": f"{user_a}_dup", "email": f"{user_a}@ejemplo.cl", "password": PWD},
        espera=400)

    print("\n-- Perfiles de menores (control parental) --")
    jid = req("POST", "/jugadores/", {"nombre": "Benja", "edad": 9},
              token=tok_a, espera=201)["id"]
    jid2 = req("POST", "/jugadores/", {"nombre": "Sofi", "edad": 11},
               token=tok_a, espera=201)["id"]
    lista = req("GET", "/jugadores/", token=tok_a, espera=200)
    print(f"          -> {len(lista)} perfiles bajo el adulto A")
    if len(lista) != 2:
        print("  [FALLA] se esperaban 2 perfiles")
    # Dos perfiles con el mismo nombre bajo el mismo adulto: rechazado
    req("POST", "/jugadores/", {"nombre": "Benja", "edad": 9}, token=tok_a, espera=400)
    req("PATCH", f"/jugadores/{jid}/", {"edad": 10}, token=tok_a, espera=200)

    print("\n-- Partida (HDU-2) --")
    pid = req("POST", "/partidas/",
              {"usuario_jugador_id": jid, "progreso": 0}, token=tok_a, espera=201)["id"]
    # Una partida sin perfil, o con un perfil inexistente, no se puede crear
    req("POST", "/partidas/", {"progreso": 0}, token=tok_a, espera=404)
    req("POST", "/partidas/", {"usuario_jugador_id": 999999999, "progreso": 0},
        token=tok_a, espera=404)

    print("\n-- Avance independiente por perfil --")
    # El perfil que jugó recupera su partida: es lo que permite retomar.
    del_jugador = req("GET", f"/jugadores/{jid}/partidas/", token=tok_a, espera=200)
    if [p["id"] for p in del_jugador] != [pid]:
        print(f"  [FALLA] se esperaba solo la partida {pid}, llegó {[p['id'] for p in del_jugador]}")
    # El hermano NO hereda la partida del otro.
    del_hermano = req("GET", f"/jugadores/{jid2}/partidas/", token=tok_a, espera=200)
    if del_hermano:
        print(f"  [FALLA] el perfil sin jugar trae {len(del_hermano)} partidas ajenas")
    req("GET", "/jugadores/999999999/partidas/", token=tok_a, espera=404)
    req("GET", f"/partidas/{pid}/", token=tok_a, espera=200)
    req("PATCH", f"/partidas/{pid}/", {"progreso": 42.5}, token=tok_a, espera=200)

    print("\n-- NPC (HDU-2) --")
    nid = req("POST", f"/partidas/{pid}/npcs/",
              {"nombre": "Alex", "area": "plaza", "tipo": "neutral", "confianza": 0},
              token=tok_a, espera=201)["id"]
    req("GET", f"/partidas/{pid}/npcs/", token=tok_a, espera=200)
    req("PATCH", f"/npcs/{nid}/", {"confianza": 3}, token=tok_a, espera=200)

    print("\n-- Chat (HDU-8) --")
    cid = req("POST", "/chats/",
              {"partida_id": pid, "npc_id": nid, "categoria_riesgo": "grooming"},
              token=tok_a, espera=201)["id"]
    req("POST", f"/chats/{cid}/mensajes/registrar/",
        {"tipo": "start", "respuesta": "Hola!"}, token=tok_a, espera=201)
    req("POST", f"/chats/{cid}/mensajes/registrar/",
        {"tipo": "request", "respuesta": "No le doy mis datos",
         "calidad_respuesta": "buena", "pregunta_banco_id": "HDU2_NPC01_F2_Q01",
         "posibles_respuestas": [
             {"texto": "Le doy mi direccion", "orden": 0, "calidad_respuesta": "mala"},
             {"texto": "No le doy mis datos", "orden": 1, "calidad_respuesta": "buena"},
         ]},
        token=tok_a, espera=201)
    msgs = req("GET", f"/chats/{cid}/mensajes/", token=tok_a, espera=200)
    print(f"          -> {len(msgs)} mensajes, "
          f"{sum(len(m['posibles_respuestas']) for m in msgs)} posibles respuestas")
    req("POST", f"/chats/{cid}/finalizar/", {"respuesta": "fin"}, token=tok_a, espera=200)
    # Un chat cerrado no debe aceptar más mensajes
    req("POST", f"/chats/{cid}/mensajes/registrar/",
        {"tipo": "start", "respuesta": "tarde"}, token=tok_a, espera=400)

    print("\n-- Banco de preguntas --")
    todas = req("GET", "/banco/preguntas/", token=tok_a, espera=200)
    print(f"          -> {len(todas)} preguntas, "
          f"{sum(len(p['opciones']) for p in todas)} opciones")
    req("GET", "/banco/preguntas/?zona=desconocidos", token=tok_a, espera=200)
    req("GET", "/banco/preguntas/?solo_riesgo=true", token=tok_a, espera=200)
    if todas:
        req("GET", f"/banco/preguntas/{todas[0]['pregunta_id']}/", token=tok_a, espera=200)

    comparar_banco_con_unity(todas)

    print("\n-- Riesgo acumulado por zona --")
    # Elige respuestas reales del banco y comprueba que el endpoint suma exactamente
    # el impacto_puntuacion de esas opciones, agrupado por la zona de su pregunta.
    # Los valores esperados se derivan del banco, no se hardcodean: si el banco
    # cambia, la prueba sigue siendo válida.
    elecciones = elegir_respuestas(todas)
    if not elecciones:
        print("  [FALLA] el banco no trae preguntas con opciones; no se puede probar")
    else:
        cid2 = req("POST", "/chats/",
                   {"partida_id": pid, "npc_id": nid, "categoria_riesgo": "grooming"},
                   token=tok_a, espera=201)["id"]

        # Partida recién estrenada en esta zona: todo en cero.
        vacio = req("GET", f"/partidas/{pid}/riesgo-por-zona/", token=tok_a, espera=200)
        if vacio["total"] != 0 or vacio["zonas"]:
            print(f"  [FALLA] sin respuestas registradas debería venir vacío, llegó {vacio}")

        for pregunta, opcion in elecciones:
            req("POST", f"/chats/{cid2}/mensajes/registrar/",
                {"tipo": "chain", "respuesta": opcion["texto"],
                 "calidad_respuesta": "buena" if opcion["impacto_puntuacion"] > 0 else "mala",
                 "pregunta_banco_id": pregunta["pregunta_id"],
                 "opcion_banco_id": opcion["opcion_id"]},
                token=tok_a, espera=201)

        # Un id que no existe en el banco no debe romper nada ni sumar: cae en
        # "sin_clasificar". Es el caso del contenido viejo que no reporta opción.
        req("POST", f"/chats/{cid2}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": "opcion inventada",
             "opcion_banco_id": "NO_EXISTE_EN_EL_BANCO_R9"},
            token=tok_a, espera=201)

        esperado = {}
        for pregunta, opcion in elecciones:
            z = esperado.setdefault(pregunta["zona"], {"suma": 0, "n": 0, "min": 0, "max": 0})
            impactos = [o["impacto_puntuacion"] for o in pregunta["opciones"]]
            z["suma"] += opcion["impacto_puntuacion"]
            z["n"]    += 1
            z["min"]  += min(impactos)
            z["max"]  += max(impactos)

        riesgo = req("GET", f"/partidas/{pid}/riesgo-por-zona/", token=tok_a, espera=200)
        comparar_riesgo(riesgo, esperado)
        req("POST", f"/chats/{cid2}/finalizar/", {"respuesta": "fin"}, token=tok_a, espera=200)
        req("GET", "/partidas/999999999/riesgo-por-zona/", token=tok_a, espera=404)

    print("\n-- Aislamiento entre adultos (B no debe ver nada de A) --")
    tok_b = req("POST", "/auth/registro/",
                {"nombre": user_b, "email": f"{user_b}@ejemplo.cl", "password": PWD},
                espera=201)["token"]
    jugadores_b = req("GET", "/jugadores/", token=tok_b, espera=200)
    if jugadores_b:
        print(f"  [FALLA] B ve {len(jugadores_b)} perfiles ajenos")
    req("GET", f"/jugadores/{jid}/", token=tok_b, espera=404)
    req("PATCH", f"/jugadores/{jid}/", {"nombre": "hackeado"}, token=tok_b, espera=404)
    req("DELETE", f"/jugadores/{jid}/", token=tok_b, espera=404)
    req("GET", f"/jugadores/{jid}/partidas/", token=tok_b, espera=404)
    # B no puede colgar una partida del perfil de A
    req("POST", "/partidas/", {"usuario_jugador_id": jid}, token=tok_b, espera=404)
    req("GET", f"/partidas/{pid}/", token=tok_b, espera=404)
    req("GET", f"/partidas/{pid}/npcs/", token=tok_b, espera=404)
    req("PATCH", f"/npcs/{nid}/", {"confianza": 99}, token=tok_b, espera=404)
    req("GET", f"/chats/{cid}/mensajes/", token=tok_b, espera=404)
    req("POST", f"/chats/{cid}/mensajes/registrar/",
        {"tipo": "start", "respuesta": "x"}, token=tok_b, espera=404)
    req("GET", f"/partidas/{pid}/riesgo-por-zona/", token=tok_b, espera=404)

    if not args.no_limpiar:
        print("\n-- Limpieza --")
        limpiar([user_a, user_b])
    else:
        print(f"\n  Datos de prueba conservados: usuarios {user_a}, {user_b}")

    print("\n" + "=" * 78)
    print(f"RESULTADO: {ok_count} OK, {fail_count} fallas")
    print("=" * 78)
    return 1 if fail_count else 0


def comparar_banco_con_unity(preguntas_api):
    """Comprueba que las opciones del banco cargado en la base sean las mismas que
    las del banco que Unity lee de Resources.

    Son dos copias del mismo JSON en carpetas distintas. Si se desincronizan, el
    juego manda `opcion_banco_id` que la base no conoce: las respuestas se guardan
    igual, pero el riesgo por zona las descarta y queda en cero sin que nada falle
    de forma visible. Por eso se comprueba acá y no solo en Unity.
    """
    global ok_count, fail_count
    import os

    raiz = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
    ruta = os.path.join(raiz, "Fishy!", "Assets", "Resources", "banco_preguntas.json")
    print("\n-- Banco de la base vs. banco de Unity --")
    if not os.path.isfile(ruta):
        print(f"  [SALTA] no encontré el banco de Unity en {ruta}")
        return

    with open(ruta, encoding="utf-8") as f:
        preguntas_unity = json.load(f).get("preguntas", [])

    def indexar_unity(preguntas):
        return {o["id"]: o.get("impacto_puntuacion", 0)
                for p in preguntas for o in (p.get("opciones_respuesta") or [])}

    def indexar_api(preguntas):
        return {o["opcion_id"]: o["impacto_puntuacion"]
                for p in preguntas for o in p.get("opciones", [])}

    en_unity, en_base = indexar_unity(preguntas_unity), indexar_api(preguntas_api)

    huerfanas = sorted(set(en_unity) - set(en_base))
    distintas = sorted(k for k in set(en_unity) & set(en_base) if en_unity[k] != en_base[k])

    if huerfanas:
        fail_count += 1
        print(f"  [FALLA] {len(huerfanas)} opción(es) que Unity manda no están en la base "
              f"(no puntuarían): {', '.join(huerfanas[:3])}")
    else:
        ok_count += 1
        print(f"  [OK   ] las {len(en_unity)} opciones de Unity existen en la base")

    if distintas:
        fail_count += 1
        print(f"  [FALLA] {len(distintas)} opción(es) con impacto distinto entre Unity y la base: "
              f"{', '.join(distintas[:3])}")
    else:
        ok_count += 1
        print("  [OK   ] los impactos coinciden en ambos bancos")


def elegir_respuestas(preguntas, por_zona=2):
    """Elige respuestas de prueba del banco: hasta `por_zona` preguntas de cada
    zona, alternando la peor opción y la mejor para que la suma no sea trivial.

    Devuelve [(pregunta, opcion), ...].
    """
    con_opciones = [p for p in preguntas if p.get("opciones")]
    vistas, elegidas = {}, []
    for i, pregunta in enumerate(con_opciones):
        zona = pregunta["zona"]
        if vistas.get(zona, 0) >= por_zona:
            continue
        vistas[zona] = vistas.get(zona, 0) + 1
        opciones = sorted(pregunta["opciones"], key=lambda o: o["impacto_puntuacion"])
        # Alterna: primero la más insegura, después la más segura.
        elegidas.append((pregunta, opciones[0] if len(elegidas) % 2 == 0 else opciones[-1]))
    return elegidas


def comparar_riesgo(riesgo, esperado):
    """Compara la respuesta de /riesgo-por-zona/ con las sumas calculadas a mano."""
    global ok_count, fail_count
    recibido = {z["zona"]: z for z in riesgo["zonas"]}

    if set(recibido) != set(esperado):
        fail_count += 1
        print(f"  [FALLA] zonas: se esperaba {sorted(esperado)}, llegó {sorted(recibido)}")
        return

    for zona, esp in sorted(esperado.items()):
        got = recibido[zona]
        campos = [
            ("riesgo_acumulado", esp["suma"]), ("respuestas", esp["n"]),
            ("minimo_posible", esp["min"]),    ("maximo_posible", esp["max"]),
        ]
        malos = [(c, e, got[c]) for c, e in campos if got[c] != e]
        if malos:
            fail_count += 1
            print(f"  [FALLA] zona '{zona}': " +
                  ", ".join(f"{c} esperado {e}, llegó {g}" for c, e, g in malos))
        else:
            ok_count += 1
            print(f"  [OK   ] zona '{zona}': {got['riesgo_acumulado']:+d} "
                  f"en {got['respuestas']} respuestas "
                  f"(escala {got['minimo_posible']:+d} a {got['maximo_posible']:+d})")

    total_esperado = sum(z["suma"] for z in esperado.values())
    if riesgo["total"] != total_esperado:
        fail_count += 1
        print(f"  [FALLA] total: esperado {total_esperado}, llegó {riesgo['total']}")
    else:
        ok_count += 1
        print(f"  [OK   ] total {riesgo['total']:+d} (más alto = más seguro)")

    # El id inventado que mandamos no debe sumar, pero sí contarse aparte.
    if riesgo["sin_clasificar"] != 1:
        fail_count += 1
        print(f"  [FALLA] sin_clasificar: esperado 1, llegó {riesgo['sin_clasificar']}")
    else:
        ok_count += 1
        print("  [OK   ] la opción inexistente en el banco no suma (sin_clasificar=1)")


def cargar_env(ruta):
    """Carga Backend/.env en os.environ (sin pisar lo que ya venga del entorno).

    La limpieza habla con la base directamente, no por HTTP, así que necesita las
    credenciales de Supabase. run.ps1/run.sh ya las exportan, pero este script
    también se puede correr a mano — sin esto, Django se cae intentando conectar
    al Postgres local que ya no existe y los datos de prueba quedan en Supabase.
    """
    import os

    if not os.path.isfile(ruta):
        return
    with open(ruta, encoding="utf-8") as f:
        for linea in f:
            linea = linea.strip()
            if not linea or linea.startswith("#") or "=" not in linea:
                continue
            clave, valor = linea.split("=", 1)
            os.environ.setdefault(clave.strip(), valor.strip().strip('"').strip("'"))


def limpiar(usuarios):
    """Borra los adultos de prueba. El cascade se lleva sus perfiles de menores
    y, colgando de esos, partidas, NPCs, chats y mensajes."""
    import os

    raiz = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    cargar_env(os.path.join(raiz, ".env"))
    sys.path.insert(0, os.path.join(raiz, "backend"))
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "juego_backend.settings")
    import django

    django.setup()
    from api.models import AdultoResponsable

    borrados = AdultoResponsable.objects.filter(nombre__in=usuarios).delete()
    print(f"  Datos de prueba borrados: {borrados[0]} filas")


if __name__ == "__main__":
    sys.exit(main())
