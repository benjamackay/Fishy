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

    if not args.no_limpiar:
        print("\n-- Limpieza --")
        limpiar([user_a, user_b])
    else:
        print(f"\n  Datos de prueba conservados: usuarios {user_a}, {user_b}")

    print("\n" + "=" * 78)
    print(f"RESULTADO: {ok_count} OK, {fail_count} fallas")
    print("=" * 78)
    return 1 if fail_count else 0


def limpiar(usuarios):
    """Borra los adultos de prueba. El cascade se lleva sus perfiles de menores
    y, colgando de esos, partidas, NPCs, chats y mensajes."""
    import os

    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "backend"))
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "juego_backend.settings")
    import django

    django.setup()
    from api.models import AdultoResponsable

    borrados = AdultoResponsable.objects.filter(nombre__in=usuarios).delete()
    print(f"  Datos de prueba borrados: {borrados[0]} filas")


if __name__ == "__main__":
    sys.exit(main())
