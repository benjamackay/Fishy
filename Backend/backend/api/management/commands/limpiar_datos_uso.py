"""
Borra los datos de uso (cuentas, niños, partidas y todo lo que cuelga de ellas)
y deja intacto el catálogo del juego (banco, misiones, detective, álbum).

Uso:
    python manage.py limpiar_datos_uso                        # solo muestra qué borraría
    python manage.py limpiar_datos_uso --confirmar            # borra de verdad
    python manage.py limpiar_datos_uso --conservar dani,qa_profesor --confirmar
    python manage.py limpiar_datos_uso --sin-partidas dani --confirmar

Se conservan siempre las cuentas admin: `is_admin` (entran a /admin/) o rol admin
del portal. `--conservar` agrega otras por nombre de usuario; una cuenta
conservada mantiene también sus niños y partidas, salvo que se nombre en
`--sin-partidas`: ahí se queda con la cuenta y los niños, y pierde las partidas.

Borra con el ORM y no con TRUNCATE: en Postgres las FK no tienen ON DELETE
CASCADE, la cascada la hace Django. Todo va en una transacción, así que si algo
falla no queda a medias. Antes de correrlo contra Supabase: `run.ps1 --respaldo`.
"""
from collections import Counter

from django.conf import settings
from django.core.management.base import BaseCommand, CommandError
from django.db import DEFAULT_DB_ALIAS, transaction
from django.db.models import Q
from django.db.models.deletion import Collector

from api.models import AdultoResponsable, Partida


def lista(valor):
    return [n.strip() for n in valor.split(",") if n.strip()]


class Command(BaseCommand):
    help = "Borra cuentas no admin con sus niños y partidas; no toca el catálogo"

    def add_arguments(self, parser):
        parser.add_argument("--conservar", default="",
                            help="Nombres de usuario a conservar además de los admin, separados por coma")
        parser.add_argument("--sin-partidas", default="",
                            help="Cuentas conservadas a las que se les borran las partidas, separadas por coma")
        parser.add_argument("--confirmar", action="store_true",
                            help="Borra de verdad. Sin esto solo muestra el conteo")

    def handle(self, *args, conservar, sin_partidas, confirmar, **opciones):
        nombres = lista(conservar)
        vaciar = lista(sin_partidas)
        filtro = Q(is_admin=True) | Q(rol=AdultoResponsable.ROL_ADMIN)
        for nombre in nombres:
            filtro |= Q(nombre__iexact=nombre)
        conservadas = AdultoResponsable.objects.filter(filtro).order_by("nombre")

        encontrados = {a.nombre.lower() for a in conservadas}
        faltan = [n for n in nombres if n.lower() not in encontrados]
        if faltan:
            # Un nombre mal escrito borraría justo la cuenta que se quería salvar.
            raise CommandError(f"No existen estas cuentas para conservar: {', '.join(faltan)}. No se borró nada.")
        ajenas = [n for n in vaciar if n.lower() not in encontrados]
        if ajenas:
            raise CommandError(f"--sin-partidas solo aplica a cuentas conservadas: {', '.join(ajenas)}. "
                               "No se borró nada.")

        a_borrar = AdultoResponsable.objects.exclude(pk__in=conservadas.values("pk"))
        sin_partidas_pks = [a.pk for a in conservadas if a.nombre.lower() in {v.lower() for v in vaciar}]
        partidas = Partida.objects.filter(usuario_jugador__adulto__in=sin_partidas_pks)

        db = settings.DATABASES[DEFAULT_DB_ALIAS]
        self.stdout.write(f"Base: {db['ENGINE'].rsplit('.', 1)[-1]} · {db.get('HOST') or db['NAME']}")
        self.stdout.write("Se conservan:")
        for a in conservadas:
            self.stdout.write(f"  {a.nombre} (rol {a.rol}{', is_admin' if a.is_admin else ''}, "
                              f"{a.jugadores.count()} niños"
                              f"{', sin sus partidas' if a.pk in sin_partidas_pks else ''})")

        collector = Collector(using=DEFAULT_DB_ALIAS)
        collector.collect(list(a_borrar))
        collector.collect(list(partidas))  # collect() pide objetos de un solo modelo por llamada
        conteo = Counter({m._meta.label: len(objs) for m, objs in collector.data.items()})
        for qs in collector.fast_deletes:
            conteo[qs.model._meta.label] += qs.count()

        self.stdout.write(f"\nSe borrarían {a_borrar.count()} cuentas, {partidas.count()} partidas de "
                          "cuentas conservadas y lo que cuelga de ellas:")
        for modelo, n in sorted(conteo.items()):
            if n:
                self.stdout.write(f"  {modelo:<32} {n:>6}")

        if not confirmar:
            self.stdout.write(self.style.WARNING("\nNo se borró nada. Para borrar: --confirmar"))
            return

        with transaction.atomic():
            total = partidas.delete()[0] + a_borrar.delete()[0]
        self.stdout.write(self.style.SUCCESS(f"\nListo: {total} filas borradas."))
