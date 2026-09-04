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
    probar_endpoints_de_zona(todas, tok_a)

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

    print("\n-- Progreso de misiones y zonas (HDU-1 CA4/CA5, HDU-3 CA5, HDU-4 CA5) --")
    misiones_api = req("GET", f"/partidas/{pid}/misiones/", token=tok_a, espera=200)
    if misiones_api:
        print(f"  [FALLA] la partida recien creada trae {len(misiones_api)} misiones")

    # La zona 1 viene abierta de fabrica: Otto empieza ahi y nunca esta oscurecida.
    zonas_al_crear = req("GET", f"/partidas/{pid}/zonas/", token=tok_a, espera=200)
    if [z["zona"] for z in zonas_al_crear] != ["desconocidos"]:
        print(f"  [FALLA] la partida deberia nacer solo con 'desconocidos' abierta, "
              f"trae {[z['zona'] for z in zonas_al_crear]}")
    elif zonas_al_crear[0]["completada"]:
        print("  [FALLA] la zona inicial nace completada; deberia estar solo desbloqueada")

    # Desbloquear deja la mision disponible, sin fecha de completada.
    m = req("POST", f"/partidas/{pid}/misiones/",
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL"}, token=tok_a, espera=201)
    if m["estado"] != "disponible" or m["fecha_completada"] is not None:
        print(f"  [FALLA] recien desbloqueada deberia estar disponible y sin fecha: {m}")
    if not m["en_catalogo"]:
        print("  [AVISO] MISION_SEC_MOCHILA_HUEMUL no esta en el banco cargado "
              "(¿falta correr cargar_banco?)")

    # Completarla le pone fecha; repetirlo no la mueve ni duplica la fila.
    m = req("POST", f"/partidas/{pid}/misiones/",
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "completada"},
            token=tok_a, espera=200)
    if m["estado"] != "completada" or not m["fecha_completada"]:
        print(f"  [FALLA] completada deberia traer fecha_completada: {m}")
    fecha = m["fecha_completada"]
    m = req("POST", f"/partidas/{pid}/misiones/",
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "completada"},
            token=tok_a, espera=200)
    if m["fecha_completada"] != fecha:
        print("  [FALLA] repetir el POST movio fecha_completada")

    # Completar es un camino de ida: no vuelve a disponible.
    m = req("POST", f"/partidas/{pid}/misiones/",
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "disponible"},
            token=tok_a, espera=200)
    if m["estado"] != "completada":
        print(f"  [FALLA] una mision completada volvio a '{m['estado']}'")

    # El id que usa Unity hoy no esta en el banco: se guarda igual y se marca.
    m = req("POST", f"/partidas/{pid}/misiones/",
            {"mision_id": "MISION_NPC_01"}, token=tok_a, espera=201)
    if m["en_catalogo"]:
        print("  [AVISO] MISION_NPC_01 ahora si esta en el banco: los ids de Unity "
              "y del banco quedaron alineados, se puede sacar este aviso")

    req("POST", f"/partidas/{pid}/misiones/", {"estado": "completada"}, token=tok_a, espera=400)
    req("POST", f"/partidas/{pid}/misiones/",
        {"mision_id": "MISION_NPC_01", "estado": "abandonada"}, token=tok_a, espera=400)

    misiones_api = req("GET", f"/partidas/{pid}/misiones/", token=tok_a, espera=200)
    estados = {x["mision_id"]: x["estado"] for x in misiones_api}
    if estados != {"MISION_SEC_MOCHILA_HUEMUL": "completada", "MISION_NPC_01": "disponible"}:
        print(f"  [FALLA] el listado de misiones no calza: {estados}")
    else:
        print(f"          -> {len(misiones_api)} misiones, 1 completada")

    # Zonas: la fila existe = desbloqueada; completar tambien es de ida.
    z = req("POST", f"/partidas/{pid}/zonas/", {"zona": "ciberacoso"}, token=tok_a, espera=201)
    if not z["desbloqueada"] or z["completada"]:
        print(f"  [FALLA] zona recien abierta deberia estar desbloqueada sin completar: {z}")
    z = req("POST", f"/partidas/{pid}/zonas/",
            {"zona": "ciberacoso", "completada": True}, token=tok_a, espera=200)
    if not z["completada"]:
        print("  [FALLA] la zona no quedo completada")
    z = req("POST", f"/partidas/{pid}/zonas/",
            {"zona": "ciberacoso", "completada": False}, token=tok_a, espera=200)
    if not z["completada"]:
        print("  [FALLA] una zona completada se reabrio")

    req("POST", f"/partidas/{pid}/zonas/", {"zona": "reto_viral"}, token=tok_a, espera=201)
    req("POST", f"/partidas/{pid}/zonas/", {}, token=tok_a, espera=400)
    zonas_api = req("GET", f"/partidas/{pid}/zonas/", token=tok_a, espera=200)
    print(f"          -> {len(zonas_api)} zonas desbloqueadas, "
          f"{sum(1 for x in zonas_api if x['completada'])} completada(s)")

    # El progreso es de la partida, no del perfil: otra partida del mismo menor
    # empieza con el mapa cerrado. Es la razon de no ponerlo en UsuarioJugador.
    pid_otra = req("POST", "/partidas/", {"usuario_jugador_id": jid, "progreso": 0},
                   token=tok_a, espera=201)["id"]
    zonas_otra = req("GET", f"/partidas/{pid_otra}/zonas/", token=tok_a, espera=200)
    if [z["zona"] for z in zonas_otra] != ["desconocidos"]:
        print(f"  [FALLA] una partida nueva deberia traer solo la zona inicial, "
              f"trae {[z['zona'] for z in zonas_otra]}")
    elif any(z["completada"] for z in zonas_otra):
        print("  [FALLA] una partida nueva hereda una zona completada de la anterior")
    if req("GET", f"/partidas/{pid_otra}/misiones/", token=tok_a, espera=200):
        print("  [FALLA] una partida nueva del mismo menor hereda misiones")

    req("GET", "/partidas/999999999/misiones/", token=tok_a, espera=404)
    req("GET", "/partidas/999999999/zonas/", token=tok_a, espera=404)

    print("\n-- Modo Detective: el servidor corrige, no copia (HDU-10 CA4/CA5) --")
    casos = req("GET", "/casos-detective/", token=tok_a, espera=200)
    if not casos:
        print("  [AVISO] no hay casos Detective cargados (¿falta cargar_detective?)")
    else:
        caso = req("GET", f"/casos-detective/{casos[0]['caso_id']}/", token=tok_a, espera=200)
        mensajes = caso["mensajes"]
        riesgo_real = [m["mensaje_id"] for m in mensajes
                       if m["es_senal_riesgo"] and not m["es_ambiguo"]]
        ambiguos = [m["mensaje_id"] for m in mensajes if m["es_ambiguo"]]
        print(f"          -> {caso['caso_id']}: {len(mensajes)} mensajes, "
              f"{len(riesgo_real)} señales reales, {len(ambiguos)} ambiguo(s)")

        # Se marca una sola señal real y se mienten los numeros a proposito: lo que
        # queda guardado tiene que ser lo que da el caso, no lo que mando el cliente.
        r = req("POST", f"/casos-detective/{caso['caso_id']}/progreso/",
                {"partida_id": pid, "mensajes_marcados": riesgo_real[:1],
                 "aciertos": 999, "total_riesgo": 999, "porcentaje": 1.0},
                token=tok_a, espera=201)
        if r["aciertos"] != min(1, len(riesgo_real)) or r["total_riesgo"] != len(riesgo_real):
            print(f"  [FALLA] el servidor no corrigio: guardo aciertos={r['aciertos']}, "
                  f"total={r['total_riesgo']} y el caso da {min(1, len(riesgo_real))}/{len(riesgo_real)}")
        else:
            print(f"          -> el cliente dijo 999 aciertos y quedo {r['aciertos']}/{r['total_riesgo']}")

        # CA5: sumar un ambiguo a las marcas no cambia el resultado.
        if ambiguos:
            r2 = req("POST", f"/casos-detective/{caso['caso_id']}/progreso/",
                     {"partida_id": pid, "mensajes_marcados": riesgo_real[:1] + ambiguos[:1]},
                     token=tok_a, espera=200)
            if r2["aciertos"] != r["aciertos"] or r2["porcentaje"] != r["porcentaje"]:
                print("  [FALLA] marcar un mensaje ambiguo cambio el puntaje")
        else:
            print(f"  [AVISO] {caso['caso_id']} no tiene mensajes ambiguos: CA5 sin verificar aca")

        # Marcarlas todas: 100%.
        r3 = req("POST", f"/casos-detective/{caso['caso_id']}/progreso/",
                 {"partida_id": pid, "mensajes_marcados": riesgo_real},
                 token=tok_a, espera=200)
        if r3["porcentaje"] != 1.0:
            print(f"  [FALLA] marcando todas las señales el porcentaje deberia ser 1.0, es {r3['porcentaje']}")

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
    req("GET", f"/partidas/{pid}/misiones/", token=tok_b, espera=404)
    req("POST", f"/partidas/{pid}/misiones/", {"mision_id": "MISION_NPC_01"},
        token=tok_b, espera=404)
    req("GET", f"/partidas/{pid}/zonas/", token=tok_b, espera=404)
    req("POST", f"/partidas/{pid}/zonas/", {"zona": "ciberacoso"}, token=tok_b, espera=404)

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
    ruta_backend = os.path.join(raiz, "banco_preguntas", "banco_preguntas.json")
    print("\n-- Banco de la base vs. banco de Unity --")
    if not os.path.isfile(ruta):
        print(f"  [SALTA] no encontré el banco de Unity en {ruta}")
        return

    with open(ruta, encoding="utf-8") as f:
        banco_unity = json.load(f)
    preguntas_unity = banco_unity.get("preguntas", [])

    # Primero lo barato: la versión declarada. Es la señal más temprana de que las
    # dos copias se separaron, y salta aunque el banco todavía no se haya cargado
    # a la base (o sea, antes de que la comparación de opciones pueda ver nada).
    if os.path.isfile(ruta_backend):
        with open(ruta_backend, encoding="utf-8") as f:
            v_backend = json.load(f).get("version")
        v_unity = banco_unity.get("version")
        if v_backend != v_unity:
            fail_count += 1
            print(f"  [FALLA] los dos JSON declaran versiones distintas: "
                  f"backend v{v_backend} vs Unity v{v_unity}. "
                  f"Copia banco_preguntas/banco_preguntas.json a Fishy!/Assets/Resources/")
        else:
            ok_count += 1
            print(f"  [OK   ] ambos JSON declaran la misma versión (v{v_unity})")

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

    # La dirección contraria: opciones cargadas en la base que Unity no tiene. No
    # rompen el puntaje, pero son contenido que el juego nunca va a mostrar — señal
    # de que a Unity le falta una recarga del JSON.
    inalcanzables = sorted(set(en_base) - set(en_unity))
    if inalcanzables:
        fail_count += 1
        print(f"  [FALLA] {len(inalcanzables)} opción(es) están en la base pero no en Unity "
              f"(contenido inalcanzable): {', '.join(inalcanzables[:3])}")
    else:
        ok_count += 1
        print(f"  [OK   ] las {len(en_base)} opciones de la base son alcanzables desde Unity")


def probar_endpoints_de_zona(preguntas_api, token):
    """Comprueba el catálogo de zonas y las rutas por zona del banco.

    Lo esperado se deriva del propio banco y no se escribe a mano: cuando entre
    una zona nueva, esta prueba la cubre sola, que es justamente lo que se le
    pide a estos endpoints.
    """
    global ok_count, fail_count
    print("\n-- Endpoints por zona --")

    esperado = {}
    for p in preguntas_api:
        esperado[p["zona"]] = esperado.get(p["zona"], 0) + 1

    zonas = req("GET", "/banco/zonas/", token=token, espera=200)
    catalogo = {z["zona"]: z["preguntas"] for z in zonas}
    if catalogo == esperado:
        ok_count += 1
        print(f"          -> {len(catalogo)} zonas: "
              + ", ".join(f"{z} ({n})" for z, n in sorted(catalogo.items())))
        print("  [OK   ] el catálogo cuadra con las preguntas del banco")
    else:
        fail_count += 1
        print(f"  [FALLA] el catálogo no cuadra: {catalogo} vs {esperado}")

    for zona, n in sorted(esperado.items()):
        ps = req("GET", f"/banco/zonas/{zona}/preguntas/", token=token, espera=200)
        ajenas = [p["pregunta_id"] for p in ps if p["zona"] != zona]
        if len(ps) == n and not ajenas:
            ok_count += 1
            print(f"  [OK   ] zona '{zona}': {n} preguntas y ninguna de otra zona")
        else:
            fail_count += 1
            print(f"  [FALLA] zona '{zona}': llegaron {len(ps)}, se esperaban {n}"
                  + (f"; {len(ajenas)} son de otra zona" if ajenas else ""))

    # Una zona inexistente debe dar 404 y no una lista vacía: si devolviera [],
    # sería indistinguible de una zona real que aún no tiene preguntas cargadas.
    req("GET", "/banco/zonas/zona-que-no-existe/preguntas/", token=token, espera=404)

    # Los filtros de /banco/preguntas/ deben seguir valiendo dentro de la ruta.
    # Se usa la zona con más preguntas: en una zona donde todas son de riesgo,
    # el filtro no distinguiría nada y la prueba pasaría sin demostrar nada.
    alguna = max(sorted(esperado), key=lambda z: esperado[z])
    todas_z = req("GET", f"/banco/zonas/{alguna}/preguntas/", token=token, espera=200)
    riesgo = req("GET", f"/banco/zonas/{alguna}/preguntas/?solo_riesgo=true",
                 token=token, espera=200)
    if len(riesgo) <= len(todas_z) and all(p["es_mensaje_riesgo"] for p in riesgo):
        ok_count += 1
        print(f"  [OK   ] ?solo_riesgo=true dentro de la zona: {len(riesgo)} de {len(todas_z)}")
    else:
        fail_count += 1
        print("  [FALLA] el filtro solo_riesgo no se aplicó dentro de la zona")

    # La ruta nueva y el filtro de siempre tienen que coincidir.
    por_query = req("GET", f"/banco/preguntas/?zona={alguna}", token=token, espera=200)
    if len(por_query) == len(todas_z):
        ok_count += 1
        print(f"  [OK   ] ?zona={alguna} y la ruta por zona devuelven lo mismo")
    else:
        fail_count += 1
        print(f"  [FALLA] ?zona={alguna} devolvió {len(por_query)} y la ruta "
              f"por zona {len(todas_z)}")


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
