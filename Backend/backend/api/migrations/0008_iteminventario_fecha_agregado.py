"""Renombra `fecha_primera_vez` a `fecha_agregado`, porque el nombre mentia.

Si el objeto se consume, la fila se borra; volver a recogerlo crea una nueva con
fecha de hoy. O sea que nunca fue "la primera vez que lo recogio" sino "cuando
entro a la mochila en el tramo actual", y un nombre que promete mas de lo que
cumple termina usado para lo que no puede responder.

Se hace ahora a proposito: la tabla nacio en la 0007, el mismo dia, y todavia
esta vacia. Un `RenameField` conserva los datos igual, pero es el momento en que
no hay nada que conservar.

Escrita a mano en vez de con `makemigrations` porque el autodetector no puede
distinguir un renombre de un borrar-y-crear: pregunta de forma interactiva y, si
se le responde que no, genera RemoveField + AddField, que en una tabla con datos
los perderia en silencio.
"""
from django.db import migrations, models


class Migration(migrations.Migration):

    dependencies = [
        ("api", "0007_inventario"),
    ]

    operations = [
        migrations.RenameField(
            model_name="iteminventario",
            old_name="fecha_primera_vez",
            new_name="fecha_agregado",
        ),
        migrations.AlterField(
            model_name="iteminventario",
            name="fecha_agregado",
            field=models.DateTimeField(
                auto_now_add=True,
                help_text="Cuando entro a la mochila. Se reinicia si el objeto se "
                          "consume y se vuelve a recoger.",
            ),
        ),
    ]
