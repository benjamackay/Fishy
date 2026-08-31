"""
Carga el banco de preguntas desde banco_preguntas.json a la BD.

Uso:
    python manage.py cargar_banco
    python manage.py cargar_banco --archivo /ruta/absoluta/banco_preguntas.json
    python manage.py cargar_banco --limpiar   # borra todo antes de insertar

La carga es atomica: si una pregunta falla, se revierte todo. Sin eso, un
error a mitad de camino deja el banco incompleto en la BD compartida.
"""
import json
from pathlib import Path

from django.conf import settings
from django.core.management.base import BaseCommand, CommandError
from django.db import transaction

from api.models import PreguntaBanco, OpcionBanco

# Ruta por defecto: 2 niveles arriba de BASE_DIR (backend/) → raíz del repo Fishy/
RUTA_DEFAULT = settings.BASE_DIR.parent.parent / "banco_preguntas" / "banco_preguntas.json"


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

        if options["limpiar"]:
            deleted, _ = PreguntaBanco.objects.all().delete()
            self.stdout.write(self.style.WARNING(f"Se eliminaron {deleted} preguntas existentes."))

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

        self.stdout.write(self.style.SUCCESS(
            f"Banco cargado: {creadas} preguntas creadas, {actualizadas} actualizadas, "
            f"{opciones_total} opciones cargadas."
        ))
