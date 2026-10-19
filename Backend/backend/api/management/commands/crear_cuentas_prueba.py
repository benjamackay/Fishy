"""
Crea las cuentas de prueba del portal: una de profesor y una de admin.

Uso:
    python manage.py crear_cuentas_prueba                 # pide la contraseña
    FISHY_CLAVE_PRUEBA=... python manage.py crear_cuentas_prueba
    python manage.py crear_cuentas_prueba --prefijo qa2   # otro juego de cuentas

La contraseña nunca va en el código ni en un argumento (quedaría en el
historial de la terminal): sale de FISHY_CLAVE_PRUEBA o se pide al correrlo.
Las dos cuentas quedan con la misma.

Solo CREA. Si ya existe una cuenta con ese nombre o correo, avisa y no la toca:
ni su rol ni su contraseña. Así correrlo dos veces, o contra una base con
cuentas reales, no le cambia nada a nadie.
"""
import getpass
import os
import sys

from django.core.management.base import BaseCommand, CommandError
from django.db.models import Q

from api.models import AdultoResponsable

VARIABLE_CLAVE = "FISHY_CLAVE_PRUEBA"


class Command(BaseCommand):
    help = "Crea una cuenta profesor y una admin del portal para pruebas (sin tocar cuentas existentes)"

    def add_arguments(self, parser):
        parser.add_argument("--prefijo", default="qa",
                            help="Las cuentas quedan como <prefijo>_profesor y <prefijo>_admin (por defecto: qa)")
        parser.add_argument("--dominio", default="example.com",
                            help="Dominio de los correos (por defecto example.com, que no existe de verdad)")

    def handle(self, *args, prefijo, dominio, **opciones):
        cuentas = [(f"{prefijo}_profesor", AdultoResponsable.ROL_PROFESOR),
                   (f"{prefijo}_admin", AdultoResponsable.ROL_ADMIN)]
        pendientes = []
        for nombre, rol in cuentas:
            email = f"{nombre}@{dominio}"
            existente = AdultoResponsable.objects.filter(Q(nombre__iexact=nombre) | Q(email__iexact=email)).first()
            if existente:
                self.stdout.write(self.style.WARNING(
                    f"  ya existe: {existente.nombre} <{existente.email}> con rol {existente.rol}. No se toca."))
            else:
                pendientes.append((nombre, email, rol))
        if not pendientes:
            self.stdout.write("Nada que crear.")
            return

        clave = self.pedir_clave()
        for nombre, email, rol in pendientes:
            AdultoResponsable.objects.create_user(nombre=nombre, email=email, password=clave, rol=rol)
            self.stdout.write(self.style.SUCCESS(f"  creada: {nombre} <{email}> con rol {rol}"))
        self.stdout.write("Se entra al portal con el NOMBRE de usuario, no con el correo.")

    def pedir_clave(self):
        clave = os.environ.get(VARIABLE_CLAVE, "")
        if clave:
            return clave
        if sys.stdin is None or not sys.stdin.isatty():
            raise CommandError(f"Sin terminal para pedir la contraseña: pásala en la variable {VARIABLE_CLAVE}.")
        clave = getpass.getpass("Contraseña para las cuentas de prueba: ")
        if not clave:
            raise CommandError("La contraseña no puede quedar vacía.")
        if getpass.getpass("Repítela: ") != clave:
            raise CommandError("Las contraseñas no coinciden. No se creó nada.")
        return clave
