import django.db.models.deletion
from django.db import migrations, models


class Migration(migrations.Migration):

    dependencies = [
        ("api", "0002_mensaje_opcion_banco_id"),
    ]

    operations = [
        migrations.CreateModel(
            name="CasoDetective",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("caso_id", models.CharField(max_length=60, unique=True)),
                ("titulo", models.CharField(max_length=150)),
                ("zona", models.CharField(max_length=50)),
                ("etiquetas_ml", models.JSONField(default=list)),
                ("permiso_player_text", models.TextField()),
                ("permiso_npc_nombre", models.CharField(max_length=60)),
                ("permiso_npc_response", models.TextField()),
            ],
            options={
                "verbose_name": "Caso Detective",
                "verbose_name_plural": "Casos Detective",
                "ordering": ["zona", "caso_id"],
            },
        ),
        migrations.CreateModel(
            name="MensajeDetective",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("mensaje_id", models.CharField(max_length=70, unique=True)),
                ("npc_sender", models.CharField(max_length=60)),
                ("texto", models.TextField()),
                ("es_senal_riesgo", models.BooleanField(default=False)),
                ("es_ambiguo", models.BooleanField(default=False)),
                ("explicacion", models.TextField(blank=True, null=True)),
                ("nota_ambiguo", models.TextField(blank=True, null=True)),
                ("orden", models.PositiveSmallIntegerField(default=0)),
                ("caso", models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name="mensajes", to="api.casodetective")),
            ],
            options={
                "verbose_name": "Mensaje Detective",
                "verbose_name_plural": "Mensajes Detective",
                "ordering": ["caso", "orden"],
            },
        ),
        migrations.CreateModel(
            name="CasoDetectiveProgreso",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("mensajes_marcados", models.JSONField(default=list, help_text="mensaje_id de los MensajeDetective que el jugador marcó como riesgo")),
                ("aciertos", models.PositiveSmallIntegerField(default=0)),
                ("total_riesgo", models.PositiveSmallIntegerField(default=0)),
                ("porcentaje", models.FloatField(default=0.0)),
                ("intentos", models.PositiveSmallIntegerField(default=1)),
                ("fecha_inicio", models.DateTimeField(auto_now_add=True)),
                ("fecha_termino", models.DateTimeField(blank=True, null=True)),
                ("caso", models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name="progresos", to="api.casodetective")),
                ("partida", models.ForeignKey(on_delete=django.db.models.deletion.CASCADE, related_name="casos_detective", to="api.partida")),
            ],
            options={
                "verbose_name": "Progreso Caso Detective",
                "verbose_name_plural": "Progresos Casos Detective",
            },
        ),
        migrations.AddConstraint(
            model_name="casodetectiveprogreso",
            constraint=models.UniqueConstraint(fields=("partida", "caso"), name="progreso_unico_por_partida_caso"),
        ),
    ]
