from django.db import migrations, models
import django.db.models.deletion


class Migration(migrations.Migration):

    dependencies = [
        ('api', '0002_chat_fecha_termino_nivelriesgo_puntaje_and_more'),
    ]

    operations = [
        migrations.CreateModel(
            name='PreguntaBanco',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('pregunta_id', models.CharField(max_length=60, unique=True)),
                ('hdu', models.CharField(max_length=10)),
                ('zona', models.CharField(max_length=50)),
                ('npc_id', models.CharField(blank=True, max_length=30)),
                ('npc_nombre', models.CharField(blank=True, max_length=60)),
                ('npc_avatar', models.CharField(blank=True, max_length=60)),
                ('fase', models.PositiveSmallIntegerField(blank=True, null=True)),
                ('orden_en_fase', models.PositiveSmallIntegerField(blank=True, null=True)),
                ('narrativa_continuacion', models.CharField(blank=True, max_length=60, null=True)),
                ('escenario_id', models.CharField(blank=True, max_length=60)),
                ('escenario_nombre', models.CharField(blank=True, max_length=120)),
                ('historial_previo', models.JSONField(default=list)),
                ('categoria', models.CharField(max_length=60)),
                ('nivel_riesgo', models.PositiveSmallIntegerField(default=0)),
                ('es_mensaje_riesgo', models.BooleanField(default=False)),
                ('mensaje_npc', models.TextField()),
                ('etiquetas_ml', models.JSONField(default=list)),
            ],
            options={
                'verbose_name': 'Pregunta Banco',
                'verbose_name_plural': 'Preguntas Banco',
                'ordering': ['zona', 'npc_id', 'fase', 'orden_en_fase', 'escenario_id'],
            },
        ),
        migrations.CreateModel(
            name='OpcionBanco',
            fields=[
                ('id', models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name='ID')),
                ('opcion_id', models.CharField(max_length=70, unique=True)),
                ('texto', models.TextField()),
                ('tipo', models.CharField(max_length=20)),
                ('consecuencia_narrativa', models.TextField()),
                ('impacto_puntuacion', models.SmallIntegerField(default=0)),
                ('siguiente_pregunta', models.CharField(blank=True, max_length=60, null=True)),
                ('orden', models.PositiveSmallIntegerField(default=0)),
                ('pregunta', models.ForeignKey(
                    on_delete=django.db.models.deletion.CASCADE,
                    related_name='opciones',
                    to='api.preguntabanco',
                )),
            ],
            options={
                'verbose_name': 'Opción Banco',
                'verbose_name_plural': 'Opciones Banco',
                'ordering': ['orden'],
            },
        ),
    ]
