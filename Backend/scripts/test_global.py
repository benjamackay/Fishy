r"""Test global del sistema Fishy!: una corrida, todas las capas.

No reemplaza a los tests que ya existen — los orquesta y tapa los hoyos que
ninguno cubría. La idea es que "¿está todo bien?" sea un solo comando en vez de
acordarse de tres invocaciones distintas y leer tres resúmenes sueltos.

Las fases, en orden y qué prueba cada una:

  1  preflight   .env, `manage.py check`, drift de migraciones y conexión real a
                 Supabase (con latencia). Si esta falla, el resto es ruido.
  2  unitarios   la suite de `api/tests/` sobre SQLite. Lógica interna, sin red.
  3  api         `smoke_test.py` por HTTP: auth, perfiles, partidas, NPC, chat,
                 banco, riesgo por zona y aislamiento entre adultos.
  4  banco       `verificar_carga_banco.py`: el JSON del banco contra Supabase por
                 ORM, en las dos direcciones. Alcanza a misiones, diálogos y
                 recompensas de álbum, que no tienen endpoint.
  5  detective   Modo Detective y niveles-riesgo por HTTP —que no probaba nadie—
                 y, sobre todo, el **cruce**: escribe por la API y después va a
                 mirar la fila en Supabase por ORM. Sin esto, una vista que
                 guardara en la tabla equivocada mentiría igual al leer y al
                 escribir, porque ambas puntas serían la misma vista.
  6  contrato    los DTO de `ApiManager.cs` contra lo que la API responde de
                 verdad. Newtonsoft NO lanza error si un campo del DTO no llega:
                 deja el int en 0 y sigue. Ese bug compila, no tira excepción y
                 el juego se comporta como si el id fuera 0. Solo se caza así.
  7  unity       `Fishy!/verificar_compilacion.ps1` (opcional, con --con-unity).

Uso (desde Backend/, el servidor lo levanta y lo baja este script solo):
    .\.venv\Scripts\python .\scripts\test_global.py
    .\.venv\Scripts\python .\scripts\test_global.py --fase 5
    .\.venv\Scripts\python .\scripts\test_global.py --fase 5 --fase 6
    .\.venv\Scripts\python .\scripts\test_global.py --con-unity
    .\.venv\Scripts\python .\scripts\test_global.py --base http://127.0.0.1:8000/api

Sale con código 1 si alguna comprobación falla.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # Backend/
REPO = os.path.dirname(RAIZ)                                         # raíz del repo
PWD_TEST = "clave-de-prueba-123"

# Puerto propio, distinto del 8000 de siempre: así una corrida no le pisa el
# servidor al que lo tenga levantado trabajando en otra terminal.
PUERTO_DEFECTO = 8011

base_url = ""
ok_count = 0
fail_count = 0
avisos = []
grafo_roto = False      # lo fija la fase 1; con esto las demás explican por qué no corren


# ── Salida ────────────────────────────────────────────────────────────────────

def ok(msg):
    global ok_count
    ok_count += 1
    print(f"  [OK   ] {msg}")


def falla(msg, detalle=""):
    global fail_count
    fail_count += 1
    print(f"  [FALLA] {msg}")
    if detalle:
        for linea in str(detalle).splitlines():
            print(f"          {linea}")


def aviso(msg):
    avisos.append(msg)
    print(f"  [AVISO] {msg}")


def salta(msg):
    print(f"  [SALTA] {msg}")


def titulo(texto):
    print("\n" + "=" * 78)
    print(f"  {texto}")
    print("=" * 78)


def subtitulo(texto):
    print(f"\n-- {texto} --")


# ── Entorno ───────────────────────────────────────────────────────────────────

def cargar_env(ruta):
    """Carga Backend/.env en os.environ sin pisar lo que ya venga del entorno.

    Mismo criterio que smoke_test.py: sin esto Django intenta conectarse al
    Postgres local que ya no existe en vez de a Supabase.
    """
    if not os.path.isfile(ruta):
        return False
    with open(ruta, encoding="utf-8") as f:
        for linea in f:
            linea = linea.strip()
            if not linea or linea.startswith("#") or "=" not in linea:
                continue
            clave, valor = linea.split("=", 1)
            os.environ.setdefault(clave.strip(), valor.strip().strip('"').strip("'"))
    return True


_django_listo = False


def django_setup():
    """Prepara el ORM contra Supabase. Idempotente: django.setup() una sola vez."""
    global _django_listo
    if _django_listo:
        return
    cargar_env(os.path.join(RAIZ, ".env"))
    ruta_backend = os.path.join(RAIZ, "backend")
    if ruta_backend not in sys.path:
        sys.path.insert(0, ruta_backend)
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "juego_backend.settings")
    import django

    django.setup()
    _django_listo = True


def correr(argv, descripcion, cwd=RAIZ):
    """Corre un subproceso mostrando su salida tal cual. Devuelve el exit code."""
    print(f"  $ {' '.join(str(a) for a in argv)}\n")
    proc = subprocess.run(argv, cwd=cwd, env=os.environ.copy())
    print()
    if proc.returncode == 0:
        ok(f"{descripcion} — sin errores")
    else:
        falla(f"{descripcion} — salió con código {proc.returncode}")
    return proc.returncode


# ── HTTP ──────────────────────────────────────────────────────────────────────

def req(metodo, ruta, body=None, token=None, espera=200, mostrar=True):
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
        falla(f"no se pudo conectar a {base_url}{ruta}: {e.reason}")
        return None
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
    if mostrar or code != espera:
        print(f"  [{marca}] {metodo:6s} {ruta:45s} -> {code} (esperado {espera}) {ms:6.0f} ms")
    if code != espera:
        print(f"          respuesta: {str(payload)[:300]}")
    return payload


class Servidor:
    """Levanta `manage.py runserver` y lo baja al salir.

    --noreload a propósito: con el autoreload hay dos procesos y terminate() mata
    al padre dejando al hijo escuchando el puerto, así que la corrida siguiente
    choca contra un servidor viejo (con el código viejo, que es peor).
    """

    def __init__(self, puerto):
        self.puerto = puerto
        self.proc = None
        self.log = None

    def __enter__(self):
        cargar_env(os.path.join(RAIZ, ".env"))
        self.log = tempfile.NamedTemporaryFile(
            prefix="fishy_server_", suffix=".log", delete=False, mode="w+", encoding="utf-8"
        )
        self.proc = subprocess.Popen(
            [sys.executable, os.path.join(RAIZ, "backend", "manage.py"),
             "runserver", f"127.0.0.1:{self.puerto}", "--noreload"],
            cwd=RAIZ, env=os.environ.copy(),
            stdout=self.log, stderr=subprocess.STDOUT,
        )
        salud = f"http://127.0.0.1:{self.puerto}/api/health/"
        t0 = time.time()
        while time.time() - t0 < 45:
            if self.proc.poll() is not None:
                raise RuntimeError(
                    f"el servidor murió al arrancar (código {self.proc.returncode}): "
                    f"{self._causa()}\n          log completo: {self.log.name}"
                )
            try:
                with urllib.request.urlopen(salud, timeout=3) as resp:
                    if resp.status == 200:
                        print(f"  Servidor arriba en 127.0.0.1:{self.puerto} "
                              f"({time.time() - t0:.1f}s)")
                        return self
            except Exception:
                time.sleep(0.5)
        raise RuntimeError(f"el servidor no respondió en {salud} tras 45s: "
                           f"{self._causa()}\n          log completo: {self.log.name}")

    def _causa(self):
        """La última línea útil del log, no las 25 del traceback.

        Un muro de traceback de Django esconde el mensaje que importa; el archivo
        completo queda en disco por si hace falta.
        """
        self.log.flush()
        with open(self.log.name, encoding="utf-8", errors="replace") as f:
            lineas = [l.rstrip() for l in f if l.strip()]
        if not lineas:
            return "(el servidor no alcanzó a escribir nada en el log)"
        # La línea de la excepción es la última que no es parte del stack.
        for linea in reversed(lineas):
            if not linea.startswith(("  File", "    ", "Traceback")):
                return linea.strip()
        return lineas[-1].strip()

    def __exit__(self, *exc):
        if self.proc and self.proc.poll() is None:
            self.proc.terminate()
            try:
                self.proc.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self.proc.kill()
        if self.log:
            self.log.close()
        return False


# ── Fixture compartido por las fases 5 y 6 ────────────────────────────────────

def crear_fixture(sufijo):
    """Adulto + perfil de menor + partida + NPC, todo por HTTP.

    Cada fase arma el suyo y lo borra: así `--fase 6` a secas funciona igual que
    la corrida completa, sin depender de que otra fase haya corrido antes.
    """
    nombre = f"tg_{sufijo}"
    reg = req("POST", "/auth/registro/",
              {"nombre": nombre, "email": f"{nombre}@test.cl", "password": PWD_TEST},
              espera=201, mostrar=False)
    if not reg or "token" not in reg:
        falla("no se pudo crear el adulto de prueba; se aborta la fase")
        return None
    tok = reg["token"]
    jid = req("POST", "/jugadores/", {"nombre": "Tester", "edad": 10},
              token=tok, espera=201, mostrar=False)["id"]
    pid = req("POST", "/partidas/", {"usuario_jugador_id": jid, "progreso": 0},
              token=tok, espera=201, mostrar=False)["id"]
    nid = req("POST", f"/partidas/{pid}/npcs/",
              {"nombre": "Alex", "area": "plaza", "tipo": "neutral", "confianza": 0},
              token=tok, espera=201, mostrar=False)["id"]
    print(f"  Fixture: adulto={nombre} perfil={jid} partida={pid} npc={nid}")
    return {"nombre": nombre, "token": tok, "adulto_id": reg["adulto_id"],
            "jugador_id": jid, "partida_id": pid, "npc_id": nid}


def borrar_fixture(nombres):
    """Borra los adultos de prueba. El cascade se lleva perfiles, partidas,
    NPCs, chats, mensajes y progreso de casos colgando de ellos."""
    django_setup()
    from api.models import AdultoResponsable

    borrados = AdultoResponsable.objects.filter(nombre__in=nombres).delete()
    print(f"  Datos de prueba borrados: {borrados[0]} filas")


# ── Fase 1: preflight ─────────────────────────────────────────────────────────

def fase_1_preflight():
    titulo("FASE 1 · Preflight: configuración, migraciones y Supabase")

    subtitulo("Archivos y entorno")
    ruta_env = os.path.join(RAIZ, ".env")
    if cargar_env(ruta_env):
        ok(".env encontrado y cargado")
    else:
        falla(f"no existe {ruta_env}", "sin credenciales de Supabase no corre nada")
        return
    faltantes = [c for c in ("DB_NAME", "DB_USER", "DB_PASSWORD", "DB_HOST", "DB_PORT",
                             "DJANGO_SECRET_KEY") if not os.environ.get(c)]
    if faltantes:
        falla(f"faltan claves en el .env: {', '.join(faltantes)}")
    else:
        ok("las 6 claves obligatorias del .env están presentes")

    if os.path.isdir(os.path.join(RAIZ, ".venv")):
        ok(f"venv presente · python en uso: {sys.executable}")
    else:
        aviso("no encontré Backend\\.venv; se usa el python del PATH")

    subtitulo("Django")
    correr([sys.executable, os.path.join(RAIZ, "backend", "manage.py"), "check"],
           "manage.py check")

    # El grafo se valida antes que nada y sin tocar la base, porque si está roto
    # NO arranca runserver, no corre `manage.py test`, ni migrate, ni
    # makemigrations: todos llaman a build_graph() antes de hacer su trabajo. Sin
    # este chequeo, el síntoma aparece cuatro veces como cuatro fallas distintas.
    subtitulo("Grafo de migraciones")
    global grafo_roto
    try:
        from django.db.migrations.exceptions import NodeNotFoundError
        from django.db.migrations.loader import MigrationLoader

        django_setup()
        MigrationLoader(None, ignore_no_migrations=True)
        ok("el grafo de migraciones es consistente")
    except NodeNotFoundError as e:
        grafo_roto = True
        origen = getattr(e, "origin", None)
        falta = getattr(e, "node", None)
        falla(
            "el grafo de migraciones está ROTO — el backend no arranca",
            f"la migración {origen} depende de {falta}, que no existe en este repo.\n"
            "Eso tumba runserver, manage.py test, migrate y makemigrations de una:\n"
            "los cuatro validan el grafo antes de hacer nada.\n"
            "Suele pasar cuando llega una migración de otra rama sin su padre.\n"
            "Compara `django_migrations` en la base con los archivos de migrations/."
        )
    except Exception as e:
        grafo_roto = True
        falla("no se pudo cargar el grafo de migraciones", repr(e))

    if not grafo_roto:
        correr([sys.executable, os.path.join(RAIZ, "backend", "manage.py"),
                "makemigrations", "--check", "--dry-run"],
               "sin drift entre modelos y migraciones")
    else:
        salta("drift de migraciones: no se puede revisar con el grafo roto")

    subtitulo("Conexión a Supabase")
    try:
        django_setup()
        from django.db import connection

        t0 = time.time()
        connection.ensure_connection()
        ms_conexion = (time.time() - t0) * 1000
        with connection.cursor() as cur:
            t0 = time.time()
            cur.execute("SELECT 1")
            cur.fetchone()
            ms_query = (time.time() - t0) * 1000
            cur.execute("SELECT version()")
            version = cur.fetchone()[0]
        ok(f"conectado a {os.environ.get('DB_HOST')} "
           f"(handshake {ms_conexion:.0f} ms · query {ms_query:.0f} ms)")
        print(f"          {version[:80]}")
    except Exception as e:
        falla("no se pudo conectar a Supabase", e)
        return

    subtitulo("Migraciones aplicadas en la base")
    if grafo_roto:
        salta("estado de las migraciones: no se puede revisar con el grafo roto")
    else:
        try:
            from django.db import connection
            from django.db.migrations.executor import MigrationExecutor

            executor = MigrationExecutor(connection)
            pendientes = executor.migration_plan(executor.loader.graph.leaf_nodes())
            if pendientes:
                falla(f"{len(pendientes)} migración(es) sin aplicar en Supabase",
                      "\n".join(f"{m.app_label}.{m.name}" for m, _ in pendientes)
                      + "\ncorre: manage.py migrate")
            else:
                ok("la base está al día con todas las migraciones")
        except Exception as e:
            falla("no se pudo revisar el estado de las migraciones", e)

    subtitulo("Datos de catálogo cargados")
    from api.models import (PreguntaBanco, OpcionBanco, CasoDetective, MensajeDetective,
                            Mision, DialogoNPC, RecompensaAlbum, NivelRiesgo)

    conteos = [
        ("preguntas del banco", PreguntaBanco, "manage.py cargar_banco"),
        ("opciones del banco", OpcionBanco, "manage.py cargar_banco"),
        ("casos Detective", CasoDetective, "manage.py cargar_detective"),
        ("mensajes Detective", MensajeDetective, "manage.py cargar_detective"),
        ("misiones", Mision, "manage.py cargar_banco"),
        ("diálogos de NPC", DialogoNPC, "manage.py cargar_banco"),
        ("recompensas de álbum", RecompensaAlbum, "manage.py cargar_banco"),
        ("niveles de riesgo", NivelRiesgo, "fixture / admin"),
    ]
    for nombre, modelo, comando in conteos:
        n = modelo.objects.count()
        if n:
            ok(f"{n} {nombre}")
        else:
            aviso(f"0 {nombre} en la base — ¿falta correr `{comando}`?")


# ── Fase 2: suite unitaria ────────────────────────────────────────────────────

def fase_2_unitarios():
    titulo("FASE 2 · Suite unitaria (SQLite, sin red)")
    print("  Corre api/tests/ contra SQLite a propósito: no toca Supabase y no")
    print("  necesita conexión. Lo que NO cubre es lo propio de Postgres (jsonb,")
    print("  constraints bajo concurrencia) — de eso se encarga la fase 5.\n")
    correr([sys.executable, os.path.join(RAIZ, "backend", "manage.py"),
            "test", "api", "--settings=juego_backend.settings_test"],
           "suite unitaria")


# ── Fase 3: API por HTTP ──────────────────────────────────────────────────────

def fase_3_api():
    titulo("FASE 3 · API por HTTP contra Supabase (smoke_test.py)")
    correr([sys.executable, os.path.join(RAIZ, "scripts", "smoke_test.py"),
            "--base", base_url],
           "smoke test end-to-end")


# ── Fase 4: banco contra la base ──────────────────────────────────────────────

def fase_4_banco():
    titulo("FASE 4 · El banco del JSON contra Supabase (por ORM)")
    correr([sys.executable, os.path.join(RAIZ, "scripts", "verificar_carga_banco.py")],
           "verificación del banco cargado")


# ── Fase 5: Detective, niveles-riesgo y cruce HTTP -> ORM ─────────────────────

def fase_5_detective():
    titulo("FASE 5 · Modo Detective, niveles-riesgo y cruce HTTP → Supabase")
    django_setup()
    from api.models import (CasoDetective, CasoDetectiveProgreso, Mensaje,
                            NivelRiesgo, Partida)

    subtitulo("Catálogo de niveles de riesgo")
    fixture = crear_fixture(f"det_{int(time.time())}")
    if not fixture:
        return
    tok = fixture["token"]
    pid = fixture["partida_id"]

    try:
        niveles = req("GET", "/niveles-riesgo/", token=tok, espera=200)
        if niveles is not None:
            en_bd = NivelRiesgo.objects.count()
            if len(niveles) == en_bd:
                ok(f"la API devuelve los {en_bd} niveles que hay en la base")
            else:
                falla(f"la API devuelve {len(niveles)} niveles y en la base hay {en_bd}")

        subtitulo("Casos Detective (lectura)")
        casos = req("GET", "/casos-detective/", token=tok, espera=200)
        if not casos:
            salta("no hay casos cargados; corre `manage.py cargar_detective`. "
                  "Se omite el resto de la fase 5.")
            return

        casos_bd = CasoDetective.objects.count()
        if len(casos) == casos_bd:
            ok(f"la API devuelve los {casos_bd} casos que hay en la base")
        else:
            falla(f"la API devuelve {len(casos)} casos y en la base hay {casos_bd}")

        # Los mensajes vienen anidados, igual que las opciones en /banco/preguntas/.
        caso = casos[0]
        cid_texto = caso["caso_id"]
        msgs_bd = CasoDetective.objects.get(caso_id=cid_texto).mensajes.count()
        if len(caso["mensajes"]) == msgs_bd:
            ok(f"caso '{cid_texto}': los {msgs_bd} mensajes anidados cuadran con la base")
        else:
            falla(f"caso '{cid_texto}': la API anida {len(caso['mensajes'])} mensajes "
                  f"y en la base hay {msgs_bd}")

        detalle = req("GET", f"/casos-detective/{cid_texto}/", token=tok, espera=200)
        if detalle and detalle.get("caso_id") == cid_texto:
            ok(f"el detalle de '{cid_texto}' devuelve el caso correcto")
        req("GET", "/casos-detective/caso-que-no-existe/", token=tok, espera=404)

        zona = caso.get("zona")
        if zona:
            por_zona = req("GET", f"/casos-detective/?zona={zona}", token=tok, espera=200)
            esperado = CasoDetective.objects.filter(zona=zona).count()
            if por_zona is not None and len(por_zona) == esperado:
                ok(f"?zona={zona}: {esperado} casos, coincide con la base")
            else:
                falla(f"?zona={zona}: la API devolvió "
                      f"{len(por_zona) if por_zona is not None else 'nada'}, "
                      f"se esperaban {esperado}")

        # ── Lo que ningún test hacía: escribir por HTTP y verificar en Supabase ──
        subtitulo("Escritura de progreso y verificación directa en Supabase")
        marcados = [m["mensaje_id"] for m in caso["mensajes"] if m["es_senal_riesgo"]]
        total_riesgo = len(marcados)
        cuerpo = {
            "partida_id": pid,
            "mensajes_marcados": marcados,
            "aciertos": total_riesgo,
            "total_riesgo": total_riesgo,
            "porcentaje": 100.0 if total_riesgo else 0.0,
        }
        req("POST", f"/casos-detective/{cid_texto}/progreso/", cuerpo,
            token=tok, espera=201)

        # La API dice que guardó. Ahora vamos a mirar la fila nosotros mismos, por
        # ORM, con otra conexión: si la vista guardara en otra tabla o el jsonb no
        # hiciera round-trip, la API igual devolvería 201 y nadie se enteraría.
        filas = CasoDetectiveProgreso.objects.filter(
            partida_id=pid, caso__caso_id=cid_texto
        )
        if filas.count() != 1:
            falla(f"en Supabase hay {filas.count()} filas de progreso, se esperaba 1")
        else:
            fila = filas.first()
            ok("la fila de progreso existe en Supabase (leída por ORM, no por la API)")
            if fila.mensajes_marcados == marcados:
                ok(f"el jsonb `mensajes_marcados` hizo round-trip exacto "
                   f"({len(marcados)} ids)")
            else:
                falla("el jsonb `mensajes_marcados` no volvió igual",
                      f"enviado: {marcados}\nen BD:   {fila.mensajes_marcados}")
            if fila.aciertos == total_riesgo and fila.total_riesgo == total_riesgo:
                ok(f"aciertos y total_riesgo persistidos ({total_riesgo})")
            else:
                falla("aciertos/total_riesgo no cuadran en la base",
                      f"BD: aciertos={fila.aciertos} total_riesgo={fila.total_riesgo}")
            if abs(fila.porcentaje - cuerpo["porcentaje"]) < 0.01:
                ok(f"porcentaje persistido ({fila.porcentaje})")
            else:
                falla(f"porcentaje: enviado {cuerpo['porcentaje']}, en BD {fila.porcentaje}")
            if fila.intentos == 1:
                ok("intentos = 1 en el primer registro")
            else:
                falla(f"intentos debería ser 1 y es {fila.intentos}")
            if fila.fecha_termino is not None:
                ok("fecha_termino quedó grabada")
            else:
                falla("fecha_termino quedó en null")

        # Reintento: mismo (partida, caso). No debe crear fila nueva.
        subtitulo("Reintento idempotente (constraint de Postgres)")
        cuerpo2 = dict(cuerpo, mensajes_marcados=marcados[:1],
                       aciertos=min(1, total_riesgo), porcentaje=50.0)
        req("POST", f"/casos-detective/{cid_texto}/progreso/", cuerpo2,
            token=tok, espera=200)
        filas = CasoDetectiveProgreso.objects.filter(
            partida_id=pid, caso__caso_id=cid_texto
        )
        if filas.count() == 1:
            ok("el reintento no creó una fila nueva (sigue habiendo 1)")
            fila = filas.first()
            if fila.intentos == 2:
                ok("intentos subió a 2")
            else:
                falla(f"intentos debería ser 2 y es {fila.intentos}")
            if fila.mensajes_marcados == marcados[:1] and abs(fila.porcentaje - 50.0) < 0.01:
                ok("el resultado se sobrescribió con el del último intento")
            else:
                falla("el reintento no sobrescribió el resultado",
                      f"BD: {fila.mensajes_marcados} / {fila.porcentaje}")
        else:
            falla(f"el reintento dejó {filas.count()} filas; el constraint "
                  "`progreso_unico_por_partida_caso` no se está respetando")

        subtitulo("Lectura del progreso de la partida")
        progresos = req("GET", f"/partidas/{pid}/casos-detective/", token=tok, espera=200)
        if progresos is not None:
            if len(progresos) == 1 and progresos[0]["intentos"] == 2:
                ok("la API devuelve el progreso con los datos del último intento")
            else:
                falla(f"se esperaba 1 progreso con intentos=2, llegó {progresos}")
        req("POST", "/casos-detective/caso-que-no-existe/progreso/", cuerpo,
            token=tok, espera=404)
        req("POST", f"/casos-detective/{cid_texto}/progreso/",
            dict(cuerpo, partida_id=999999999), token=tok, espera=404)

        # ── Cruce del flujo normal: el opcion_banco_id del que depende el riesgo ──
        subtitulo("Cruce del flujo de chat: lo que la API escribe queda en la base")
        chat_id = req("POST", "/chats/",
                      {"partida_id": pid, "npc_id": fixture["npc_id"],
                       "categoria_riesgo": "grooming"},
                      token=tok, espera=201)["id"]
        req("POST", f"/chats/{chat_id}/mensajes/registrar/",
            {"tipo": "request", "respuesta": "No le doy mis datos",
             "calidad_respuesta": "buena",
             "pregunta_banco_id": "TG_PREGUNTA_01",
             "opcion_banco_id": "TG_PREGUNTA_01_R2"},
            token=tok, espera=201)
        m = Mensaje.objects.filter(chat_id=chat_id).order_by("-id").first()
        if m is None:
            falla("el mensaje no llegó a la tabla `Mensaje` de Supabase")
        elif m.opcion_banco_id == "TG_PREGUNTA_01_R2" and m.pregunta_banco_id == "TG_PREGUNTA_01":
            ok("pregunta_banco_id y opcion_banco_id persistidos "
               "(es de lo que depende riesgo-por-zona)")
        else:
            falla("los ids del banco no se guardaron bien",
                  f"BD: pregunta={m.pregunta_banco_id!r} opcion={m.opcion_banco_id!r}")

        p = Partida.objects.filter(pk=pid).first()
        if p and p.usuario_jugador_id == fixture["jugador_id"]:
            ok("la partida cuelga del perfil correcto en la base")
        else:
            falla("la partida no quedó asociada al perfil que la creó")

        subtitulo("Aislamiento: otro adulto no ve este progreso")
        otro = crear_fixture(f"det_b_{int(time.time())}")
        if otro:
            req("GET", f"/partidas/{pid}/casos-detective/", token=otro["token"], espera=404)
            req("POST", f"/casos-detective/{cid_texto}/progreso/", cuerpo,
                token=otro["token"], espera=404)
            borrar_fixture([otro["nombre"]])
    finally:
        borrar_fixture([fixture["nombre"]])


# ── Fase 6: contrato de los DTO de Unity ──────────────────────────────────────

RUTA_APIMANAGER = os.path.join(REPO, "Fishy!", "Assets", "Scripts", "ApiManager.cs")

RE_CLASE = re.compile(r"^\s*public class (\w+)")
RE_CAMPO = re.compile(r"^\s*public\s+([\w\.]+(?:<[^>]+>)?\??)\s+(\w+)\s*(?:=[^;]*)?;\s*$")


def parsear_dtos(ruta):
    """Saca {clase: [(campo, tipo C#)]} de los [Serializable] de ApiManager.cs.

    Se salta las propiedades (`=>` y `{ get;`): no se serializan, así que
    compararlas contra el JSON daría falsos positivos.
    """
    dtos = {}
    actual = None
    with open(ruta, encoding="utf-8") as f:
        for linea in f:
            m = RE_CLASE.match(linea)
            if m:
                actual = m.group(1)
                dtos[actual] = []
                continue
            if actual is None:
                continue
            if re.match(r"^\s{0,4}\}", linea):      # cierre de la clase
                actual = None
                continue
            if "=>" in linea or "{ get" in linea:   # propiedad, no campo
                continue
            # Sin esto se pierden los campos con comentario al final de línea
            # (`public string tipo;   // aliado | neutral | enemigo`), que en este
            # archivo son varios: RiesgoZonaDto entero desaparecía sin ruido.
            linea = re.sub(r"//.*$", "", linea)
            m = RE_CAMPO.match(linea)
            if m:
                dtos[actual].append((m.group(2), m.group(1)))

    vacias = [k for k, v in dtos.items() if not v and k != "ApiManager"]
    if vacias:
        aviso(f"clases de ApiManager.cs sin campos reconocidos: {', '.join(vacias)}")
    return {k: v for k, v in dtos.items() if v}


def tipo_calza(tipo_cs, valor):
    """¿Un valor JSON entra en ese tipo de C# sin perder información?

    La regla clave: un `int` (no nullable) que recibe null queda en 0 en silencio.
    Eso es un bug, no una advertencia.
    """
    nullable = tipo_cs.endswith("?")
    base = tipo_cs.rstrip("?")
    if valor is None:
        return nullable or base in ("string", "object") or base.startswith("List<")
    if base.startswith("List<"):
        return isinstance(valor, list)
    if base in ("int", "long"):
        return isinstance(valor, int) and not isinstance(valor, bool)
    if base in ("float", "double"):
        return isinstance(valor, (int, float)) and not isinstance(valor, bool)
    if base == "bool":
        return isinstance(valor, bool)
    if base == "string":
        return isinstance(valor, str)
    return True     # tipo compuesto: se revisa por su propio DTO


def comparar_dto(clase, campos, muestra):
    nombres = {c for c, _ in campos}
    faltan = [c for c in nombres if c not in muestra]
    sobran = [k for k in muestra if k not in nombres]

    if faltan:
        falla(f"{clase}: {len(faltan)} campo(s) que el DTO espera y la API NO manda: "
              f"{', '.join(sorted(faltan))}",
              "Newtonsoft no lanza error: deja el int en 0 / el string en null y sigue.")
    else:
        ok(f"{clase}: los {len(nombres)} campos del DTO llegan en la respuesta")

    if sobran:
        aviso(f"{clase}: la API manda {len(sobran)} campo(s) que el DTO ignora: "
              f"{', '.join(sorted(sobran))}")

    malos = []
    for campo, tipo in campos:
        if campo in muestra and not tipo_calza(tipo, muestra[campo]):
            malos.append(f"{campo}: DTO `{tipo}` vs API {json.dumps(muestra[campo])[:40]}")
    if malos:
        falla(f"{clase}: {len(malos)} campo(s) con tipo incompatible",
              "\n".join(malos))
    elif not faltan:
        ok(f"{clase}: los tipos calzan con los valores reales")


def fase_6_contrato():
    titulo("FASE 6 · Contrato: los DTO de ApiManager.cs vs la API real")
    print("  Newtonsoft NO falla si un campo del DTO no llega en el JSON: deja el")
    print("  int en 0 y sigue. Un backend que renombre `usuario_id` a `adulto_id`")
    print("  deja a Unity compilando perfecto y jugando con el id 0. Esto lo caza.\n")

    if not os.path.isfile(RUTA_APIMANAGER):
        falla(f"no encontré {RUTA_APIMANAGER}")
        return
    dtos = parsear_dtos(RUTA_APIMANAGER)
    print(f"  {len(dtos)} clases con campos serializables en ApiManager.cs")

    fixture = crear_fixture(f"con_{int(time.time())}")
    if not fixture:
        return
    tok, pid, nid = fixture["token"], fixture["partida_id"], fixture["npc_id"]

    try:
        subtitulo("Capturando respuestas reales de la API")
        muestras = {}

        muestras["AuthResponse"] = req("POST", "/auth/login/",
                                       {"nombre": fixture["nombre"], "password": PWD_TEST},
                                       espera=200, mostrar=False)
        muestras["AdultoResponsableDto"] = req("GET", "/auth/perfil/", token=tok,
                                               espera=200, mostrar=False)
        jugadores = req("GET", "/jugadores/", token=tok, espera=200, mostrar=False)
        muestras["UsuarioJugadorDto"] = jugadores[0] if jugadores else None
        muestras["PartidaDto"] = req("GET", f"/partidas/{pid}/", token=tok,
                                     espera=200, mostrar=False)
        npcs = req("GET", f"/partidas/{pid}/npcs/", token=tok, espera=200, mostrar=False)
        muestras["NpcDto"] = npcs[0] if npcs else None

        chat = req("POST", "/chats/",
                   {"partida_id": pid, "npc_id": nid, "categoria_riesgo": "grooming"},
                   token=tok, espera=201, mostrar=False)
        muestras["ChatDto"] = chat

        # La opción tiene que ser una REAL del banco: riesgo-por-zona solo agrupa
        # en `zonas` lo que puede resolver contra el banco. Con un id inventado la
        # respuesta cae en `sin_clasificar`, `zonas` viene vacío y RiesgoZonaDto se
        # quedaría sin muestra que comparar — justo el DTO donde un campo que no
        # llega deja el riesgo en 0 sin que nadie lo note.
        banco = req("GET", "/banco/preguntas/", token=tok, espera=200, mostrar=False)
        pregunta = next((p for p in (banco or []) if p.get("opciones")), None)
        if pregunta:
            opcion = pregunta["opciones"][0]
            ids_banco = {"pregunta_banco_id": pregunta["pregunta_id"],
                         "opcion_banco_id": opcion["opcion_id"]}
        else:
            aviso("el banco no trae preguntas con opciones: RiesgoZonaDto se "
                  "queda sin muestra")
            ids_banco = {"pregunta_banco_id": "TG_Q1", "opcion_banco_id": "TG_Q1_R2"}

        req("POST", f"/chats/{chat['id']}/mensajes/registrar/",
            {"tipo": "request", "respuesta": "No le doy mis datos",
             "calidad_respuesta": "buena",
             **ids_banco,
             "posibles_respuestas": [
                 {"texto": "Le doy mi direccion", "orden": 0, "calidad_respuesta": "mala"},
                 {"texto": "No le doy mis datos", "orden": 1, "calidad_respuesta": "buena"},
             ]},
            token=tok, espera=201, mostrar=False)
        mensajes = req("GET", f"/chats/{chat['id']}/mensajes/", token=tok,
                       espera=200, mostrar=False)
        if mensajes:
            muestras["MensajeDto"] = mensajes[-1]
            if mensajes[-1].get("posibles_respuestas"):
                muestras["PosibleRespuestaDto"] = mensajes[-1]["posibles_respuestas"][0]

        riesgo = req("GET", f"/partidas/{pid}/riesgo-por-zona/", token=tok,
                     espera=200, mostrar=False)
        muestras["RiesgoPorZonaDto"] = riesgo
        if riesgo and riesgo.get("zonas"):
            muestras["RiesgoZonaDto"] = riesgo["zonas"][0]

        casos = req("GET", "/casos-detective/", token=tok, espera=200, mostrar=False)
        if casos:
            muestras["CasoDetectiveDto"] = casos[0]
            if casos[0].get("mensajes"):
                muestras["MensajeDetectiveDto"] = casos[0]["mensajes"][0]
            muestras["ProgresoDetectiveDto"] = req(
                "POST", f"/casos-detective/{casos[0]['caso_id']}/progreso/",
                {"partida_id": pid, "mensajes_marcados": [], "aciertos": 0,
                 "total_riesgo": 0, "porcentaje": 0.0},
                token=tok, espera=201, mostrar=False)

        subtitulo("Comparando campo por campo")
        for clase, campos in sorted(dtos.items()):
            if clase not in muestras:
                continue
            muestra = muestras[clase]
            if not isinstance(muestra, dict):
                salta(f"{clase}: no se pudo capturar una muestra de la API")
                continue
            comparar_dto(clase, campos, muestra)

        sin_probar = sorted(set(dtos) - set(muestras) - {"ApiManager"})
        if sin_probar:
            print(f"\n  Sin muestra de la API (no salen de un endpoint): "
                  f"{', '.join(sin_probar)}")
    finally:
        borrar_fixture([fixture["nombre"]])


# ── Fase 7: compilación de Unity (opcional) ───────────────────────────────────

def fase_7_unity():
    titulo("FASE 7 · Compilación de los scripts de Unity (opcional)")
    script = os.path.join(REPO, "Fishy!", "verificar_compilacion.ps1")
    if not os.path.isfile(script):
        falla(f"no encontré {script}")
        return
    print("  Compila con el Roslyn de Unity, sin abrir el editor ni tocar assets.")
    print("  No cubre referencias de escena, prefabs ni nada de runtime/UI.\n")

    proc = subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script],
        cwd=os.path.join(REPO, "Fishy!"), env=os.environ.copy(),
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    salida = (proc.stdout or "") + (proc.stderr or "")
    print(salida)

    if proc.returncode == 0:
        ok("los scripts de Unity compilan")
        return

    # El .csproj lo regenera Unity al abrir el proyecto. Si alguien agregó
    # scripts y no ha abierto el editor, Roslyn compila sin ellos y todo lo que
    # los usa revienta con CS0234/CS0246. Esos errores son del .csproj viejo, no
    # del código: darlos por "no compila" sería mentir en la dirección peligrosa.
    if "no estan en el .csproj" in salida:
        faltantes = re.findall(r"^\s+(Assets\\[^\n]+\.cs)\s*$", salida, re.MULTILINE)
        aviso("compilación NO CONCLUYENTE: el .csproj está viejo y no lista "
              f"{len(set(faltantes))} script(s), así que no se compilaron. "
              "Los CS0234/CS0246 de arriba son consecuencia de eso, no errores "
              "del código. Abre Unity una vez para que regenere el .csproj y "
              "vuelve a correr esta fase.")
        return

    falla("los scripts de Unity NO compilan — errores reales de código")


# ── Orquestación ──────────────────────────────────────────────────────────────

FASES = {
    1: ("preflight", fase_1_preflight, False),
    2: ("unitarios", fase_2_unitarios, False),
    3: ("api",       fase_3_api,       True),
    4: ("banco",     fase_4_banco,     False),
    5: ("detective", fase_5_detective, True),
    6: ("contrato",  fase_6_contrato,  True),
    7: ("unity",     fase_7_unity,     False),
}


def main():
    global base_url
    # Sin esto, al redirigir la salida a un archivo el print() del padre queda
    # buffereado y el de los subprocesos sale directo: el informe aparece
    # desordenado, con la salida de smoke_test.py antes de su propio título.
    try:
        sys.stdout.reconfigure(line_buffering=True)
    except AttributeError:
        pass

    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--fase", type=int, action="append", choices=sorted(FASES),
                        help="corre solo esta fase (se puede repetir)")
    parser.add_argument("--base", default=None,
                        help="usar un servidor ya levantado en vez de arrancar uno")
    parser.add_argument("--puerto", type=int, default=PUERTO_DEFECTO,
                        help=f"puerto para el servidor propio (por defecto {PUERTO_DEFECTO})")
    parser.add_argument("--con-unity", action="store_true",
                        help="incluir la fase 7 (compilación de Unity)")
    args = parser.parse_args()

    if args.fase:
        pedidas = sorted(set(args.fase))
    else:
        pedidas = [1, 2, 3, 4, 5, 6] + ([7] if args.con_unity else [])

    necesita_servidor = any(FASES[f][2] for f in pedidas) and not args.base

    print("=" * 78)
    print("  TEST GLOBAL — Fishy!")
    print("=" * 78)
    print(f"  Fases: {', '.join(f'{f} ({FASES[f][0]})' for f in pedidas)}")
    if args.base:
        base_url = args.base.rstrip("/")
        print(f"  Servidor: externo, en {base_url}")
    elif necesita_servidor:
        base_url = f"http://127.0.0.1:{args.puerto}/api"
        print(f"  Servidor: lo levanta este script en el puerto {args.puerto}")
    t_inicio = time.time()

    def correr_fases(bloqueadas=()):
        resultados = []
        for f in pedidas:
            nombre, funcion, _ = FASES[f]
            if f in bloqueadas:
                titulo(f"FASE {f} · {nombre} — BLOQUEADA")
                salta("necesita el servidor, y el servidor no arrancó (ver arriba)")
                resultados.append((f, nombre, 0, 0, 0, True))
                continue
            antes_ok, antes_fail, antes_avisos = ok_count, fail_count, len(avisos)
            try:
                funcion()
            except Exception as e:
                falla(f"la fase {f} ({nombre}) se cayó con una excepción", repr(e))
            resultados.append((f, nombre, ok_count - antes_ok,
                               fail_count - antes_fail, len(avisos) - antes_avisos,
                               False))
        return resultados

    if necesita_servidor:
        try:
            with Servidor(args.puerto):
                resultados = correr_fases()
        except RuntimeError as e:
            falla(str(e))
            # Que el servidor no arranque no es razón para no correr lo que sí se
            # puede: el preflight, los unitarios, el banco y Unity no lo necesitan.
            resultados = correr_fases(
                bloqueadas=[f for f in pedidas if FASES[f][2]])
    else:
        resultados = correr_fases()

    titulo("RESUMEN")
    for f, nombre, n_ok, n_fail, n_avisos, bloqueada in resultados:
        if bloqueada:
            marca, detalle = "BLOQ ", "no corrió"
        elif n_fail:
            marca, detalle = "FALLA", f"{n_ok:4d} ok, {n_fail:3d} fallas"
        elif n_ok == 0 and n_avisos:
            # Ni pasó ni falló: no se pudo concluir (típico de la fase 7 con el
            # .csproj viejo). Cantarla como OK sería el error peligroso.
            marca, detalle = "AVISO", "no concluyente"
        else:
            marca, detalle = "OK   ", f"{n_ok:4d} ok, {n_fail:3d} fallas"
        print(f"  [{marca}] fase {f} · {nombre:10s} {detalle}")
    if avisos:
        print(f"\n  {len(avisos)} aviso(s) — no rompen nada, pero míralos:")
        for a in avisos:
            print(f"    · {a}")
    print(f"\n  Total: {ok_count} ok, {fail_count} fallas "
          f"· {time.time() - t_inicio:.1f}s")
    if fail_count:
        print("\n  HAY FALLAS. Revisa arriba.")
    else:
        print("\n  Todo en verde.")
    return 1 if fail_count else 0


if __name__ == "__main__":
    sys.exit(main())
