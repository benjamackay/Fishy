"""Settings para correr la suite de tests.

    python manage.py test api --settings=juego_backend.settings_test

Usa SQLite en un archivo temporal en vez de Supabase, a propósito:

  - `manage.py test` CREA Y BORRA una base ("test_<nombre>"). Contra Supabase eso
    necesita permisos de creación de base y, peor, correrlo distraído con las
    credenciales de producción es justo el tipo de accidente que no queremos.
  - Los tests deben poder correr sin conexión y sin pisarle la base a nadie del
    equipo.

Lo que NO cubre: diferencias propias de Postgres (tipos jsonb, comportamiento de
los CheckConstraint bajo concurrencia). Para eso está
`scripts/verificar_carga_banco.py`, que sí habla con Supabase de verdad.
"""
from .settings import *  # noqa: F401,F403

DATABASES = {
    "default": {
        "ENGINE": "django.db.backends.sqlite3",
        "NAME": BASE_DIR / "test_db.sqlite3",  # noqa: F405
        "TEST": {"NAME": BASE_DIR / "test_db.sqlite3"},  # noqa: F405
    }
}

# Argon2 es lo correcto en producción, pero acá cada test que crea un adulto
# pagaría el hash completo. En tests solo importa que la contraseña se verifique.
PASSWORD_HASHERS = ["django.contrib.auth.hashers.MD5PasswordHasher"]
