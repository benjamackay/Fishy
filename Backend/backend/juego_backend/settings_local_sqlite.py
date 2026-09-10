"""Settings para levantar el servidor en local, sin Supabase.

    python manage.py runserver --settings=juego_backend.settings_local_sqlite

Existe para poder desarrollar y probar Unity contra un backend de verdad sin
necesitar la `DB_PASSWORD` de Supabase, y sin arriesgarse a escribirle a la base
que comparte el equipo.

**No es lo mismo que `settings_test`**, aunque las dos usen SQLite:

  - `settings_test` es para `manage.py test`: la base se crea y se borra en cada
    corrida, y usa MD5 como hasher para que los tests no paguen Argon2. Con esos
    settings un servidor guardaria contrasenas con MD5, que no queremos ni en
    local por si alguien reusa una de verdad.
  - Esto de aqui es un servidor normal con una base que PERSISTE entre arranques,
    y conserva el Argon2 de produccion.

La base queda en `backend/local_db.sqlite3`, que el .gitignore ya cubre con su
regla `*.sqlite3`. Para empezar de cero, borra el archivo y vuelve a migrar.

Lo que NO cubre: las diferencias propias de Postgres. SQLite no valida el largo
de los varchar ni tiene jsonb, asi que algo que aqui pasa puede reventar contra
Supabase. Para el flujo del juego basta; antes de dar algo por bueno de verdad,
probalo contra la base real.
"""
from .settings import *  # noqa: F401,F403

DATABASES = {
    "default": {
        "ENGINE": "django.db.backends.sqlite3",
        "NAME": BASE_DIR / "local_db.sqlite3",  # noqa: F405
    }
}

# En local siempre, pase lo que pase el .env: si DEBUG viniera en False, Django
# exigiria ALLOWED_HOSTS y devolveria 500 sin decir por que.
DEBUG = True
ALLOWED_HOSTS = ["127.0.0.1", "localhost", "0.0.0.0", "*"]
