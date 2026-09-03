"""
Carga los casos del Modo Detective desde detective_cases.json a la BD.

Uso:
    python manage.py cargar_detective
    python manage.py cargar_detective --archivo /ruta/absoluta/detective_cases.json
    python manage.py cargar_detective --limpiar   # borra todo antes de insertar

La carga es atomica: si un caso falla, se revierte todo. Sin eso, un error a
mitad de camino deja el banco incompleto en la BD compartida.
"""
import json
from pathlib import Path

from django.conf import settings
from django.core.management.base import BaseCommand, CommandError
from django.db import transaction

from api.models import CasoDetective, MensajeDetective

# Ruta por defecto: 2 niveles arriba de BASE_DIR (backend/) → raíz del repo Fishy/
RUTA_DEFAULT = settings.BASE_DIR.parent.parent / "banco_preguntas" / "detective_cases.json"


class Command(BaseCommand):
    help = "Carga los casos del Modo Detective desde un JSON al modelo CasoDetective/MensajeDetective"

    def add_arguments(self, parser):
        parser.add_argument(
            "--archivo",
            default=str(RUTA_DEFAULT),
            help=f"Ruta al JSON (default: {RUTA_DEFAULT})",
        )
        parser.add_argument(
            "--limpiar",
            action="store_true",
            help="Elimina todos los casos existentes antes de cargar",
        )

    @transaction.atomic
    def handle(self, *args, **options):
        ruta = Path(options["archivo"])
        if not ruta.exists():
            raise CommandError(f"Archivo no encontrado: {ruta}")

        with open(ruta, encoding="utf-8") as f:
            data = json.load(f)

        if options["limpiar"]:
            deleted, _ = CasoDetective.objects.all().delete()
            self.stdout.write(self.style.WARNING(f"Se eliminaron {deleted} casos existentes."))

        casos = data.get("casos", [])
        creados = actualizados = mensajes_total = 0

        for c in casos:
            permiso = c.get("permiso") or {}
            defaults = {
                "titulo":               c.get("titulo", ""),
                "zona":                 c.get("zona", ""),
                "etiquetas_ml":         c.get("etiquetas_ml", []),
                "permiso_player_text":  permiso.get("player_text", ""),
                "permiso_npc_nombre":   permiso.get("npc_nombre", ""),
                "permiso_npc_response": permiso.get("npc_response", ""),
            }

            caso_obj, created = CasoDetective.objects.update_or_create(
                caso_id=c["id"],
                defaults=defaults,
            )
            if created:
                creados += 1
            else:
                actualizados += 1

            # Sincronizar mensajes: borrar los antiguos y recrear
            caso_obj.mensajes.all().delete()
            for i, m in enumerate(c.get("conversacion") or []):
                MensajeDetective.objects.create(
                    caso=caso_obj,
                    mensaje_id=m["id"],
                    npc_sender=m.get("npc_sender", ""),
                    texto=m.get("texto", ""),
                    es_senal_riesgo=m.get("es_senal_riesgo", False),
                    es_ambiguo=m.get("es_ambiguo", False),
                    explicacion=m.get("explicacion"),
                    nota_ambiguo=m.get("nota_ambiguo"),
                    orden=i,
                )
                mensajes_total += 1

        self.stdout.write(self.style.SUCCESS(
            f"Casos Detective cargados: {creados} creados, {actualizados} actualizados, "
            f"{mensajes_total} mensajes cargados."
        ))
