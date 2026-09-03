r"""Verifica que el banco cargado en Supabase sea exactamente el del JSON.

Complementa a smoke_test.py, que prueba el backend por HTTP con el servidor
corriendo. Este habla con la base directamente por el ORM, así que:
  - no necesita servidor levantado (sirve en CI),
  - prueba de verdad el cargador (`manage.py cargar_banco`), no las vistas,
  - alcanza a los modelos que todavía no tienen endpoint — diálogos, misiones
    y recompensas de álbum, que hoy no se pueden mirar de ninguna otra forma.

Comprueba las dos direcciones:
  JSON -> BD   todo lo que el banco declara llegó, y con el mismo contenido.
  BD -> JSON   no quedaron filas huérfanas de una carga anterior. Esa dirección
               importa porque el cargador hace update_or_create: nunca borra, y
               una pregunta retirada del banco se quedaría viva en Supabase sin
               que nada avise.

Uso (desde Backend/):
    .\.venv\Scripts\python .\scripts\verificar_carga_banco.py
    .\.venv\Scripts\python .\scripts\verificar_carga_banco.py --archivo <ruta.json>

Sale con código 1 si alguna comprobación falla.
"""
import argparse
import json
import os
import sys

ok_count = 0
fail_count = 0


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


def comparar(nombre, esperado, obtenido):
    """Compara un valor del JSON contra el de la base."""
    if esperado == obtenido:
        ok(nombre)
    else:
        falla(nombre, f"JSON: {esperado!r}\nBD:   {obtenido!r}")


def cargar_env(ruta):
    """Carga Backend/.env en os.environ (sin pisar lo que ya venga del entorno).

    Igual que en smoke_test.py: sin esto Django intenta conectarse al Postgres
    local que ya no existe, en vez de a Supabase.
    """
    if not os.path.isfile(ruta):
        return
    with open(ruta, encoding="utf-8") as f:
        for linea in f:
            linea = linea.strip()
            if not linea or linea.startswith("#") or "=" not in linea:
                continue
            clave, valor = linea.split("=", 1)
            os.environ.setdefault(clave.strip(), valor.strip().strip('"').strip("'"))


def arrancar_django(raiz):
    cargar_env(os.path.join(raiz, ".env"))
    sys.path.insert(0, os.path.join(raiz, "backend"))
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "juego_backend.settings")
    import django

    django.setup()


# ─────────────────────────────────────────────────────────────────────────────
# Bloques
# ─────────────────────────────────────────────────────────────────────────────

def verificar_preguntas(data):
    from api.models import PreguntaBanco

    print("\n-- Preguntas y opciones --")
    preguntas = data.get("preguntas", [])
    en_bd = {p.pregunta_id: p for p in PreguntaBanco.objects.prefetch_related("opciones")}

    comparar("cantidad de preguntas", len(preguntas), len(en_bd))

    opciones_json = 0
    for p in preguntas:
        pid = p["id"]
        obj = en_bd.get(pid)
        if obj is None:
            falla(f"la pregunta {pid} está en el JSON pero no en la base")
            continue

        # Campos que el juego y el cálculo de riesgo usan de verdad. No se
        # comparan todos: los que sí, son los que rompen algo si se desfasan.
        difs = []
        for campo, esperado in (
            ("zona", p.get("zona", "")),
            ("hdu", p.get("hdu", "")),
            ("npc_id", p.get("npc_id", "")),
            ("categoria", p.get("categoria", "")),
            ("nivel_riesgo", p.get("nivel_riesgo", 0)),
            ("es_mensaje_riesgo", p.get("es_mensaje_riesgo", False)),
            ("mensaje_npc", p.get("mensaje_npc", "")),
            ("escenario_id", p.get("escenario_id") or ""),
        ):
            actual = getattr(obj, campo)
            if actual != esperado:
                difs.append(f"{campo}: JSON {esperado!r} vs BD {actual!r}")
        if difs:
            falla(f"la pregunta {pid} difiere", "\n".join(difs))
        else:
            ok(f"pregunta {pid} idéntica")

        # Opciones: son la llave del riesgo por zona (`opcion_banco_id`), así que
        # el impacto_puntuacion tiene que calzar exacto o el puntaje miente.
        ops_json = {o["id"]: o for o in (p.get("opciones_respuesta") or [])}
        ops_bd = {o.opcion_id: o for o in obj.opciones.all()}
        opciones_json += len(ops_json)

        faltan = set(ops_json) - set(ops_bd)
        sobran = set(ops_bd) - set(ops_json)
        if faltan:
            falla(f"opciones de {pid} que no llegaron a la base", sorted(faltan))
        if sobran:
            falla(f"opciones de {pid} en la base que el JSON ya no tiene", sorted(sobran))

        for oid, oj in ops_json.items():
            ob = ops_bd.get(oid)
            if ob is None:
                continue
            if ob.impacto_puntuacion != oj.get("impacto_puntuacion", 0):
                falla(
                    f"impacto_puntuacion distinto en {oid}",
                    f"JSON {oj.get('impacto_puntuacion', 0)} vs BD {ob.impacto_puntuacion}",
                )
            elif ob.texto != oj.get("texto", ""):
                falla(f"texto distinto en la opción {oid}")

    comparar("cantidad de opciones", opciones_json, sum(len(o.opciones.all()) for o in en_bd.values()))

    sobran = set(en_bd) - {p["id"] for p in preguntas}
    if sobran:
        falla("preguntas huérfanas en la base (ya no están en el JSON)", sorted(sobran))
    else:
        ok("sin preguntas huérfanas en la base")


def verificar_dialogos(data):
    from api.models import DialogoNPC, Mision

    print("\n-- Diálogos de NPCs neutros y misiones --")
    dialogos = data.get("dialogos_npc_neutros", [])
    en_bd = {d.dialogo_id: d for d in DialogoNPC.objects.select_related("mision")}

    comparar("cantidad de diálogos", len(dialogos), len(en_bd))

    for d in dialogos:
        did = d["id"]
        obj = en_bd.get(did)
        if obj is None:
            falla(f"el diálogo {did} está en el JSON pero no en la base")
            continue

        difs = []
        if obj.lineas != (d.get("lineas") or []):
            difs.append(f"lineas: JSON {len(d.get('lineas') or [])} vs BD {len(obj.lineas)}")
        for campo, esperado in (
            ("zona", d.get("zona", "")),
            ("npc_id", d.get("npc_id", "")),
            ("npc_nombre", d.get("npc_nombre", "")),
            ("trigger", d.get("trigger", "")),
        ):
            actual = getattr(obj, campo)
            if actual != esperado:
                difs.append(f"{campo}: JSON {esperado!r} vs BD {actual!r}")

        # La misión es lo que enlaza con el MissionManager de Unity: si el id no
        # calza, el juego no puede reportar la misión que completó el niño.
        esperada = d.get("mision_desbloquea")
        actual = obj.mision.mision_id if obj.mision else None
        if esperada != actual:
            difs.append(f"mision: JSON {esperada!r} vs BD {actual!r}")

        if difs:
            falla(f"el diálogo {did} difiere", "\n".join(difs))
        else:
            ok(f"diálogo {did} idéntico")

    misiones_json = {d["mision_desbloquea"] for d in dialogos if d.get("mision_desbloquea")}
    misiones_bd = set(Mision.objects.values_list("mision_id", flat=True))
    comparar("cantidad de misiones", len(misiones_json), len(misiones_bd))
    if misiones_json - misiones_bd:
        falla("misiones del JSON que no llegaron", sorted(misiones_json - misiones_bd))
    if misiones_bd - misiones_json:
        falla("misiones huérfanas en la base", sorted(misiones_bd - misiones_json))

    sobran = set(en_bd) - {d["id"] for d in dialogos}
    if sobran:
        falla("diálogos huérfanos en la base", sorted(sobran))
    else:
        ok("sin diálogos huérfanos en la base")


def verificar_album(data):
    """Comprueba las 12 recompensas de álbum.

    Se recalculan desde el JSON con el MISMO parser del cargador, a propósito:
    lo que se está probando no es el regex (eso sería probar el código contra sí
    mismo), sino que lo que el parser produjo haya llegado íntegro a Supabase y
    que la cuenta cuadre con lo que el banco realmente promete.
    """
    from api.models import RecompensaAlbum
    from api.management.commands.cargar_banco import (
        RE_ALBUM_DIALOGO,
        RE_ALBUM_OPCION,
        extraer_recompensa,
        menciona_album,
    )

    print("\n-- Recompensas de álbum --")
    esperadas = {}
    for p in data.get("preguntas", []):
        for o in (p.get("opciones_respuesta") or []):
            premio = extraer_recompensa(
                o.get("consecuencia_narrativa", ""), RE_ALBUM_OPCION, f"opción {o['id']}"
            )
            if premio:
                esperadas[f"ALB_{o['id']}"] = premio[0]
    for d in data.get("dialogos_npc_neutros", []):
        premio = extraer_recompensa(
            d.get("pista_mision") or "", RE_ALBUM_DIALOGO, f"diálogo {d['id']}"
        )
        if premio:
            esperadas[f"ALB_{d['mision_desbloquea']}"] = premio[0]

    en_bd = {r.recompensa_id: r for r in RecompensaAlbum.objects.all()}
    comparar("cantidad de recompensas", len(esperadas), len(en_bd))

    for rid, nombre in esperadas.items():
        obj = en_bd.get(rid)
        if obj is None:
            falla(f"la recompensa {rid} no llegó a la base")
        elif obj.nombre != nombre:
            falla(f"nombre distinto en {rid}", f"JSON {nombre!r} vs BD {obj.nombre!r}")
        else:
            ok(f"recompensa {rid}")

    sobran = set(en_bd) - set(esperadas)
    if sobran:
        falla("recompensas huérfanas en la base", sorted(sobran))

    # Cada recompensa cuelga de una misión o de una opción, nunca de las dos ni
    # de ninguna. La base ya lo impide con un CheckConstraint; acá se comprueba
    # que el cargador esté llenando el origen correcto y no dejando todo en uno.
    por_mision = sum(1 for r in en_bd.values() if r.mision_id is not None)
    por_opcion = sum(1 for r in en_bd.values() if r.opcion_banco_id)
    comparar("recompensas con origen en una misión", 6, por_mision)
    comparar("recompensas con origen en una opción", 6, por_opcion)

    # El texto libre del que salen es frágil: si el banco cambia la redacción, lo
    # correcto es que el cargador falle, no que guarde un nombre vacío o raro.
    vacias = [rid for rid, r in en_bd.items() if not r.nombre.strip()]
    if vacias:
        falla("recompensas con nombre vacío (el parseo se rompió)", sorted(vacias))
    else:
        ok("todas las recompensas tienen nombre")

    # Contraste contra el banco sin usar el parser: cuántos textos hablan de álbum.
    menciones = sum(
        1
        for p in data.get("preguntas", [])
        for o in (p.get("opciones_respuesta") or [])
        if menciona_album(o.get("consecuencia_narrativa", ""))
    ) + sum(
        1 for d in data.get("dialogos_npc_neutros", []) if menciona_album(d.get("pista_mision") or "")
    )
    comparar("textos del banco que mencionan el álbum vs recompensas cargadas", menciones, len(en_bd))


def main():
    raiz = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    default_json = os.path.join(raiz, "..", "banco_preguntas", "banco_preguntas.json")

    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--archivo", default=default_json, help="Ruta al banco_preguntas.json")
    args = ap.parse_args()

    if not os.path.isfile(args.archivo):
        print(f"No encontré el banco en {args.archivo}")
        return 1

    with open(args.archivo, encoding="utf-8") as f:
        data = json.load(f)

    print(f"Banco: {args.archivo}")
    print(f"Versión declarada: {data.get('version')}")

    arrancar_django(raiz)
    from django.db import OperationalError, connection

    try:
        connection.ensure_connection()
    except OperationalError as e:
        print(f"\nNo pude conectarme a la base: {e}")
        print("Revisa Backend/.env — sin credenciales Django intenta el Postgres local.")
        return 1
    print(f"Base: {connection.settings_dict.get('HOST')}")

    verificar_preguntas(data)
    verificar_dialogos(data)
    verificar_album(data)

    print(f"\n{'=' * 60}")
    print(f"  {ok_count} OK, {fail_count} fallas")
    if fail_count:
        print("  Si faltan filas, corre:  python manage.py cargar_banco")
    print(f"{'=' * 60}")
    return 1 if fail_count else 0


if __name__ == "__main__":
    sys.exit(main())
