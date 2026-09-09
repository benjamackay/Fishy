import os
from pathlib import Path

from django.core.exceptions import ImproperlyConfigured
from django.core.management.utils import get_random_secret_key

BASE_DIR = Path(__file__).resolve().parent.parent

# ─── Seguridad ────────────────────────────────────────────────────────────────
DEBUG = os.environ.get("DJANGO_DEBUG", "True") == "True"

# La clave sale SOLO del entorno. Antes habia aqui un valor por defecto
# ("clave-local-insegura-cambiar") y eso es justo lo que no puede ser: este
# archivo vive en un repositorio publico, asi que esa clave la podia leer
# cualquiera. Y peor, Django arrancaba con ella sin avisar nada, de modo que
# un servidor mal configurado se veia perfectamente sano.
#
# run.ps1, run.sh y docker-compose cargan el .env antes de levantar Django,
# asi que por esas tres vias la clave siempre viene del entorno. Lo de abajo
# es la red para cuando alguien arranca con `manage.py runserver` a secas.
SECRET_KEY = os.environ.get("DJANGO_SECRET_KEY")
if not SECRET_KEY:
    if not DEBUG:
        # Fuera de desarrollo, mejor no arrancar que arrancar inseguro y callado.
        raise ImproperlyConfigured(
            "Falta la variable de entorno DJANGO_SECRET_KEY y DJANGO_DEBUG es "
            "False, asi que el servidor no se levanta.\n"
            "  1. Copia Backend/.env.example a Backend/.env\n"
            "  2. Genera una clave con:\n"
            "     python -c \"from django.core.management.utils import "
            "get_random_secret_key; print(get_random_secret_key())\"\n"
            "  3. Pegala en DJANGO_SECRET_KEY, dentro de Backend/.env\n"
            "Y levanta con Backend/run.ps1 (o run.sh), que carga el .env solo."
        )
    # En desarrollo se inventa una al vuelo: distinta en cada arranque, pero de
    # nadie mas. Como cambia, las sesiones de /admin/ no sobreviven un reinicio;
    # solo le pasa a quien no tenga .env, que tampoco alcanza la base de datos.
    SECRET_KEY = get_random_secret_key()

# Las paginas de error de Django listan las variables locales de cada frame,
# y ahi aparecia la contrasena del apoderado en texto plano. Este filtro las
# tapa por nombre. El decorador @sensitive_variables NO sirve con DRF; el
# porque esta explicado en juego_backend/filtros_error.py.
DEFAULT_EXCEPTION_REPORTER_FILTER = "juego_backend.filtros_error.FiltroCredenciales"

ALLOWED_HOSTS = ["*"]  # Restringir en producción

# ─── Aplicaciones ─────────────────────────────────────────────────────────────
INSTALLED_APPS = [
    "django.contrib.admin",
    "django.contrib.auth",
    "django.contrib.contenttypes",
    "django.contrib.sessions",
    "django.contrib.messages",
    "django.contrib.staticfiles",
    # Terceros
    "rest_framework",
    "rest_framework.authtoken",
    # Propias
    "api",
]

# ─── Middleware ───────────────────────────────────────────────────────────────
MIDDLEWARE = [
    "django.middleware.security.SecurityMiddleware",
    "django.contrib.sessions.middleware.SessionMiddleware",
    "django.middleware.common.CommonMiddleware",
    "django.middleware.csrf.CsrfViewMiddleware",
    "django.contrib.auth.middleware.AuthenticationMiddleware",
    "django.contrib.messages.middleware.MessageMiddleware",
    "django.middleware.clickjacking.XFrameOptionsMiddleware",
]

ROOT_URLCONF = "juego_backend.urls"

TEMPLATES = [
    {
        "BACKEND": "django.template.backends.django.DjangoTemplates",
        "DIRS": [],
        "APP_DIRS": True,
        "OPTIONS": {
            "context_processors": [
                "django.template.context_processors.debug",
                "django.template.context_processors.request",
                "django.contrib.auth.context_processors.auth",
                "django.contrib.messages.context_processors.messages",
            ],
        },
    },
]

WSGI_APPLICATION = "juego_backend.wsgi.application"

# ─── Base de datos (Supabase / Postgres) ───────────────────────────────────────
# Credenciales por variables de entorno (ver Backend/.env.example).
# Supabase EXIGE SSL, por eso sslmode=require.
#
# DB_CONN_MAX_AGE: Supabase está en la nube, así que abrir la conexión cuesta
# ~500 ms (handshake TLS) mientras que una consulta ya conectado cuesta ~65 ms.
# Reutilizar la conexión ahorra esos ~500 ms por request, PERO solo sirve con un
# servidor de workers persistentes (gunicorn/uwsgi). Con `runserver` —que crea un
# hilo nuevo por request— no se reutiliza nada y encima las conexiones quedan
# colgando, y Supabase solo permite 60. Por eso el default es 0 (desactivado):
# actívalo (ej. 600) solo al desplegar con gunicorn.
DATABASES = {
    "default": {
        "ENGINE": "django.db.backends.postgresql",
        "NAME": os.environ.get("DB_NAME", "postgres"),
        "USER": os.environ.get("DB_USER", "postgres"),
        "PASSWORD": os.environ.get("DB_PASSWORD", ""),
        "HOST": os.environ.get("DB_HOST", "localhost"),
        "PORT": os.environ.get("DB_PORT", "5432"),
        "CONN_MAX_AGE": int(os.environ.get("DB_CONN_MAX_AGE", "0")),
        "CONN_HEALTH_CHECKS": True,
        "OPTIONS": {"sslmode": os.environ.get("DB_SSLMODE", "require")},
    }
}

# ─── Hashing de contraseñas ───────────────────────────────────────────────────
# El default de Django (PBKDF2 con 1.5M iteraciones) tarda 2-5 s por login/registro
# en los equipos del equipo, y eso es CPU local, no latencia de Supabase. Argon2
# da la misma (o mejor) resistencia a fuerza bruta en decenas de milisegundos.
# PBKDF2 se deja de segundo para poder validar contraseñas viejas: Django rehashea
# al hash nuevo de forma transparente en el primer login exitoso.
PASSWORD_HASHERS = [
    "django.contrib.auth.hashers.Argon2PasswordHasher",
    "django.contrib.auth.hashers.PBKDF2PasswordHasher",
    "django.contrib.auth.hashers.PBKDF2SHA1PasswordHasher",
    "django.contrib.auth.hashers.ScryptPasswordHasher",
]

# ─── Validación de contraseñas ────────────────────────────────────────────────
AUTH_PASSWORD_VALIDATORS = [
    {"NAME": "django.contrib.auth.password_validation.UserAttributeSimilarityValidator"},
    {"NAME": "django.contrib.auth.password_validation.MinimumLengthValidator"},
    {"NAME": "django.contrib.auth.password_validation.CommonPasswordValidator"},
    {"NAME": "django.contrib.auth.password_validation.NumericPasswordValidator"},
]

# ─── Internacionalización ─────────────────────────────────────────────────────
LANGUAGE_CODE = "es-cl"
TIME_ZONE = "America/Santiago"
USE_I18N = True
USE_TZ = True

# ─── Archivos estáticos ───────────────────────────────────────────────────────
STATIC_URL = "static/"

DEFAULT_AUTO_FIELD = "django.db.models.BigAutoField"

# Usuario custom: la cuenta con login es la del adulto responsable (control
# parental). Los perfiles de menores (`UsuarioJugador`) NO tienen credenciales.
AUTH_USER_MODEL = "api.AdultoResponsable"

# ─── Django REST Framework ────────────────────────────────────────────────────
REST_FRAMEWORK = {
    "DEFAULT_PERMISSION_CLASSES": [
        "rest_framework.permissions.IsAuthenticated",
    ],
    "DEFAULT_AUTHENTICATION_CLASSES": [
        "rest_framework.authentication.TokenAuthentication",
        "rest_framework.authentication.SessionAuthentication",
    ],
}
