import uuid

from django.db import migrations, models


def borrar_codigos(apps, schema_editor):
    # Viven minutos. Borrarlos evita que el default se calcule una sola vez y
    # repita el mismo uuid en filas existentes, rompiendo el unique.
    apps.get_model("api", "CodigoRecuperacion").objects.all().delete()


class Migration(migrations.Migration):

    dependencies = [
        ("api", "0021_codigo_recuperacion"),
    ]

    operations = [
        migrations.RunPython(borrar_codigos, migrations.RunPython.noop),
        migrations.AddField(
            model_name="codigorecuperacion",
            name="solicitud_id",
            field=models.UUIDField(default=uuid.uuid4, editable=False, unique=True),
        ),
    ]
