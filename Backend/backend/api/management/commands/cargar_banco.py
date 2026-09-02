"""
Carga el banco de preguntas desde banco_preguntas.json a la BD.

Uso:
    python manage.py cargar_banco
    python manage.py cargar_banco --archivo /ruta/absoluta/banco_preguntas.json
    python manage.py cargar_banco --limpiar   # borra todo antes de insertar

La carga es atomica: si una pregunta falla, se revierte todo. Sin eso, un
error a mitad de camino deja el banco incompleto en la BD compartida.

Carga tres bloques del JSON:
  - `preguntas`              -> PreguntaBanco + OpcionBanco
  - `dialogos_npc_neutros`   -> DialogoNPC + Mision
  - las recompensas de album -> RecompensaAlbum, extraidas del texto (ver abajo)

Sobre las recompensas: hoy el banco no tiene un campo propio para ellas, van
incrustadas en prosa de dos formas distintas (`pista_mision` de los dialogos y
`consecuencia_narrativa` de las opciones). Este cargador las extrae con regex
como puente, hasta que el banco traiga un campo `recompensa_album` explicito.
El parseo falla RUIDOSAMENTE: si un texto menciona el album pero no calza con
el formato conocido, la carga entera se revierte en vez de guardar basura.
"""
import json
import re
import unicodedata
from pathlib import Path

from django.conf import settings
from django.core.management.base import BaseCommand, CommandError
from django.db import transaction

from api.models import (
    DialogoNPC,
    Mision,
    OpcionBanco,
    PreguntaBanco,
    RecompensaAlbum,
)

# Ruta por defecto: 2 niveles arriba de BASE_DIR (backend/) → raíz del repo Fishy/
RUTA_DEFAULT = settings.BASE_DIR.parent.parent / "banco_preguntas" / "banco_preguntas.json"

# Claves del JSON que este cargador entiende. Cualquier otra se avisa por consola:
# el modo de falla que costó caro fue justamente ignorar bloques en silencio.
CLAVES_DE_CONTENIDO = {"preguntas", "dialogos_npc_neutros"}
CLAVES_DE_METADATA = {
    "version", "autor", "fecha_creacion", "fecha_actualizacion",
    "hdu_cubiertas", "formato_respuesta",
}

# "RECOMPENSA DE ÁLBUM: 'Sticker del Mapa del Bosque' (tip sobre ...)."
RE_ALBUM_DIALOGO = re.compile(
    r"RECOMPENSA\s+DE\s+[ÁA]LBUM\s*:\s*'([^']+)'\s*(?:\(([^)]*)\))?",
    re.IGNORECASE,
)
# "... Álbum: desbloquea la 'Estampa de la Lupa de Huemul'. ..."
RE_ALBUM_OPCION = re.compile(
    r"[ÁA]lbum\s*:\s*desbloquea\s+la\s+'([^']+)'",
    re.IGNORECASE,
)


def menciona_album(texto):
    """True si el texto habla del álbum, con o sin tilde."""
    if not texto:
        return False
    plano = unicodedata.normalize("NFD", texto).encode("ascii", "ignore").decode()
    return "album" in plano.lower()


def extraer_recompensa(texto, patron, origen):
    """Saca (nombre, tip) del texto libre. Devuelve None si no habla del álbum.

    Si el texto SÍ menciona el álbum pero no calza con el formato, revienta. Un
    parseo que falla callado guardaría una recompensa a medias, y eso es peor
    que no tenerla: nadie se enteraría hasta que un apoderado viera el álbum
    incompleto.
    """
    if not menciona_album(texto):
        return None
    m = patron.search(texto)
    if not m:
        raise CommandError(
            f"{origen}: el texto menciona el álbum pero no calza con el formato "
            f"esperado, así que no puedo sacar el nombre de la recompensa.\n"
            f"  Texto: {texto[:200]}\n"
            f"  Se esperaba el nombre entre comillas simples. Corrige el banco o "
            f"ajusta el patrón en cargar_banco.py."
        )
    nombre = m.group(1).strip()
    tip = (m.group(2) or "").strip() if patron.groups > 1 else ""
    return nombre, tip


class Command(BaseCommand):
    help = "Carga el banco de preguntas desde un JSON al modelo PreguntaBanco/OpcionBanco"

    def add_arguments(self, parser):
        parser.add_argument(
            "--archivo",
            default=str(RUTA_DEFAULT),
            help=f"Ruta al JSON (default: {RUTA_DEFAULT})",
        )
        parser.add_argument(
            "--limpiar",
            action="store_true",
            help="Elimina todas las preguntas existentes antes de cargar",
        )

    @transaction.atomic
    def handle(self, *args, **options):
        ruta = Path(options["archivo"])
        if not ruta.exists():
            raise CommandError(f"Archivo no encontrado: {ruta}")

        with open(ruta, encoding="utf-8") as f:
            data = json.load(f)

        # Avisar de bloques que el JSON trae y este cargador no sabe leer.
        desconocidas = set(data) - CLAVES_DE_CONTENIDO - CLAVES_DE_METADATA
        if desconocidas:
            self.stdout.write(self.style.WARNING(
                f"El JSON trae bloques que este cargador NO carga: {sorted(desconocidas)}. "
                f"Si son contenido, hay que modelarlos: no se están guardando."
            ))

        if options["limpiar"]:
            deleted, _ = PreguntaBanco.objects.all().delete()
            self.stdout.write(self.style.WARNING(f"Se eliminaron {deleted} preguntas existentes."))
            # Ojo: borrar las recompensas se lleva en cascada el álbum que los
            # niños ya obtuvieron (RecompensaObtenida). Por eso va avisado.
            borradas_r, _ = RecompensaAlbum.objects.all().delete()
            borrados_d, _ = DialogoNPC.objects.all().delete()
            borradas_m, _ = Mision.objects.all().delete()
            self.stdout.write(self.style.WARNING(
                f"Se eliminaron {borradas_r} recompensas, {borrados_d} diálogos y "
                f"{borradas_m} misiones — junto con el progreso de álbum asociado."
            ))

        recompensas = []  # (recompensa_id, nombre, tip, mision_obj, opcion_banco_id)

        # ── Preguntas y opciones ────────────────────────────────────────────
        preguntas = data.get("preguntas", [])
        creadas = actualizadas = opciones_total = 0

        for p in preguntas:
            defaults = {
                "hdu":                    p.get("hdu", ""),
                "zona":                   p.get("zona", ""),
                "npc_id":                 p.get("npc_id", ""),
                "npc_nombre":             p.get("npc_nombre", ""),
                "npc_avatar":             p.get("npc_avatar", ""),
                "fase":                   p.get("fase"),
                "orden_en_fase":          p.get("orden_en_fase"),
                "narrativa_continuacion": p.get("narrativa_continuacion"),
                "escenario_id":           p.get("escenario_id") or "",
                "escenario_nombre":       p.get("escenario_nombre") or "",
                "historial_previo":       p.get("historial_previo") or [],
                "categoria":              p.get("categoria", ""),
                "nivel_riesgo":           p.get("nivel_riesgo", 0),
                "es_mensaje_riesgo":      p.get("es_mensaje_riesgo", False),
                "es_fin_de_npc":          p.get("es_fin_de_npc", False),
                "es_fin_de_zona":         p.get("es_fin_de_zona", False),
                "mensaje_npc":            p.get("mensaje_npc", ""),
                "etiquetas_ml":           p.get("etiquetas_ml", []),
            }

            pregunta_obj, created = PreguntaBanco.objects.update_or_create(
                pregunta_id=p["id"],
                defaults=defaults,
            )
            if created:
                creadas += 1
            else:
                actualizadas += 1

            # Sincronizar opciones: borrar las antiguas y recrear
            pregunta_obj.opciones.all().delete()
            for i, op in enumerate(p.get("opciones_respuesta") or []):
                OpcionBanco.objects.create(
                    pregunta=pregunta_obj,
                    opcion_id=op["id"],
                    texto=op.get("texto", ""),
                    tipo=op.get("tipo", ""),
                    consecuencia_narrativa=op.get("consecuencia_narrativa", ""),
                    impacto_puntuacion=op.get("impacto_puntuacion", 0),
                    siguiente_pregunta=op.get("siguiente_pregunta"),
                    orden=i,
                )
                opciones_total += 1

                premio = extraer_recompensa(
                    op.get("consecuencia_narrativa", ""),
                    RE_ALBUM_OPCION,
                    f"opción {op['id']}",
                )
                if premio:
                    nombre, tip = premio
                    recompensas.append((f"ALB_{op['id']}", nombre, tip, None, op["id"]))

        # ── Diálogos de NPCs neutros y sus misiones ─────────────────────────
        dialogos = data.get("dialogos_npc_neutros", [])
        dialogos_creados = dialogos_actualizados = misiones_total = 0

        for d in dialogos:
            mision_obj = None
            mision_id = d.get("mision_desbloquea")
            if mision_id:
                # El id del diálogo distingue la misión de exploración del guía
                # (HDU1_NPC_*) de los encargos secundarios (HDU1_SEC_*).
                tipo = (
                    Mision.Tipo.SECUNDARIA if "_SEC_" in d["id"]
                    else Mision.Tipo.EXPLORACION
                )
                mision_obj, _ = Mision.objects.update_or_create(
                    mision_id=mision_id,
                    defaults={
                        "nombre": d.get("nombre_mision") or "",
                        "tipo":   tipo,
                        "zona":   d.get("zona", ""),
                    },
                )
                misiones_total += 1

            _, created = DialogoNPC.objects.update_or_create(
                dialogo_id=d["id"],
                defaults={
                    "hdu":          d.get("hdu", ""),
                    "zona":         d.get("zona", ""),
                    "npc_id":       d.get("npc_id", ""),
                    "npc_nombre":   d.get("npc_nombre", ""),
                    "npc_avatar":   d.get("npc_avatar", ""),
                    "tipo":         d.get("tipo", ""),
                    "trigger":      d.get("trigger", ""),
                    "lineas":       d.get("lineas") or [],
                    "pista_mision": d.get("pista_mision"),
                    "mision":       mision_obj,
                },
            )
            if created:
                dialogos_creados += 1
            else:
                dialogos_actualizados += 1

            premio = extraer_recompensa(
                d.get("pista_mision") or "",
                RE_ALBUM_DIALOGO,
                f"diálogo {d['id']}",
            )
            if premio:
                nombre, tip = premio
                if mision_obj is None:
                    raise CommandError(
                        f"diálogo {d['id']}: tiene recompensa de álbum pero no declara "
                        f"`mision_desbloquea`, así que no hay a qué colgarla."
                    )
                recompensas.append((f"ALB_{mision_id}", nombre, tip, mision_obj, ""))

        # ── Recompensas de álbum ────────────────────────────────────────────
        # Van con update_or_create sobre el recompensa_id (derivado del origen,
        # que es estable) y no con delete+create: así conservan su PK y el
        # progreso de los niños en RecompensaObtenida sobrevive a cada recarga.
        recompensas_creadas = recompensas_actualizadas = 0
        for recompensa_id, nombre, tip, mision_obj, opcion_id in recompensas:
            _, created = RecompensaAlbum.objects.update_or_create(
                recompensa_id=recompensa_id,
                defaults={
                    "nombre":          nombre,
                    "tip_educativo":   tip,
                    "mision":          mision_obj,
                    "opcion_banco_id": opcion_id,
                },
            )
            if created:
                recompensas_creadas += 1
            else:
                recompensas_actualizadas += 1

        self.stdout.write(self.style.SUCCESS(
            f"Banco cargado: {creadas} preguntas creadas, {actualizadas} actualizadas, "
            f"{opciones_total} opciones cargadas."
        ))
        self.stdout.write(self.style.SUCCESS(
            f"Diálogos: {dialogos_creados} creados, {dialogos_actualizados} actualizados, "
            f"{misiones_total} misiones vinculadas."
        ))
        self.stdout.write(self.style.SUCCESS(
            f"Álbum: {recompensas_creadas} recompensas creadas, "
            f"{recompensas_actualizadas} actualizadas."
        ))
