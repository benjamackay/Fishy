"""Settings de prueba: misma app, pero SQLite en memoria.

Existe para poder correr la suite sin crear una base `test_postgres` en Supabase.
No se usa en produccion ni en el servidor de desarrollo.
"""
from .settings import *  # noqa: F401,F403

DATABASES = {
    "default": {
        "ENGINE": "django.db.backends.sqlite3",
        "NAME": ":memory:",
    }
}
