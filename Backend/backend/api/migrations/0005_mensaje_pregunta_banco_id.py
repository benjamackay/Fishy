from django.db import migrations, models


class Migration(migrations.Migration):
    """
    Agrega pregunta_banco_id a Mensaje para vincular cada respuesta
    del jugador con la pregunta del banco que la generó.
    Ej: "HDU2_NPC01_F2_Q01"
    """

    dependencies = [
        ("api", "0004_preguntabanco_fin_flags"),
    ]

    operations = [
        migrations.AddField(
            model_name="mensaje",
            name="pregunta_banco_id",
            field=models.CharField(
                blank=True,
                null=True,
                max_length=70,
                help_text="ID de la pregunta del banco que originó este mensaje (ej: HDU2_NPC01_F2_Q01).",
            ),
        ),
    ]
