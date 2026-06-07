from django.db import migrations, models


class Migration(migrations.Migration):

    dependencies = [
        ('api', '0003_banco_preguntas'),
    ]

    operations = [
        migrations.AddField(
            model_name='preguntabanco',
            name='es_fin_de_npc',
            field=models.BooleanField(default=False, help_text='True en estados FIN_SEGURO / FIN_INSEGURO'),
        ),
        migrations.AddField(
            model_name='preguntabanco',
            name='es_fin_de_zona',
            field=models.BooleanField(default=False, help_text='True en la pregunta ZONA_FIN'),
        ),
    ]
