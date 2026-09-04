"""Las partidas creadas antes de que existiera ZonaProgreso no tienen la zona 1.

Desde ahora `crear_partida` la deja escrita al crear la partida, pero las que ya
estaban en la base quedaron sin ninguna fila: el mapa se lee como si estuviera
todo cerrado, incluida la zona donde Otto empieza y que nunca está oscurecida.

Se rellena una sola vez. Es idempotente (no toca las partidas que ya la tengan) y
al revertir borra exactamente lo que creó, no las zonas que abrió el jugador: solo
las de la zona inicial sin fecha de completada.
"""
from django.db import migrations

ZONA_INICIAL = "desconocidos"


def rellenar(apps, schema_editor):
    Partida = apps.get_model("api", "Partida")
    ZonaProgreso = apps.get_model("api", "ZonaProgreso")

    ya_tienen = set(
        ZonaProgreso.objects
        .filter(zona=ZONA_INICIAL)
        .values_list("partida_id", flat=True)
    )
    faltantes = [
        ZonaProgreso(partida_id=pid, zona=ZONA_INICIAL)
        for pid in Partida.objects.exclude(pk__in=ya_tienen).values_list("pk", flat=True)
    ]
    # `fecha_desbloqueo` es auto_now_add: en un bulk_create se llena igual, con la
    # fecha de la migración. No se puede saber cuándo empezó realmente cada partida.
    ZonaProgreso.objects.bulk_create(faltantes)


def borrar(apps, schema_editor):
    ZonaProgreso = apps.get_model("api", "ZonaProgreso")
    ZonaProgreso.objects.filter(zona=ZONA_INICIAL, fecha_completada__isnull=True).delete()


class Migration(migrations.Migration):

    dependencies = [
        ("api", "0005_misionprogreso_zonaprogreso"),
    ]

    operations = [
        migrations.RunPython(rellenar, borrar),
    ]
