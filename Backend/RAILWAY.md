# Django en Railway

El `requirements.txt` de la raíz incluye el de `Backend/backend`, que contiene
las dependencias de Django y Gunicorn. No hace falta mantener dos listas.

## Configuración del servicio

- Rama de GitHub: `test` (los cambios deben estar subidos para que Railway los vea).
- Root Directory: raíz del repositorio, `/`.
- Builder: Railpack.
- Start Command:

```sh
gunicorn --chdir Backend/backend juego_backend.wsgi:application --bind 0.0.0.0:$PORT --access-logfile - --error-logfile -
```

Railpack instala las dependencias del `requirements.txt`. El puerto lo entrega
Railway. Este comando no ejecuta migraciones automáticamente.

## Variables del servicio

Configurar en Railway, sin subir el archivo `.env`:

- `DJANGO_DEBUG=False`
- `DJANGO_SECRET_KEY`: una clave privada estable para este despliegue.
- `DB_NAME`, `DB_USER`, `DB_PASSWORD`, `DB_HOST`, `DB_PORT`: los datos de Supabase.
- `DB_SSLMODE=require`
- `DB_CONN_MAX_AGE=0` inicialmente; no dejar este valor vacío.
- `FISHY_WEB_URL`: URL pública HTTPS del frontend.

El backend actual lee estas variables `DB_*`, no `DATABASE_URL`.
El correo sigue desactivado hasta configurar el proveedor según `.env.example`.

## Alcance

Esto prepara la instalación de dependencias y el inicio de la API; no realiza el
despliegue ni cambia modelos o migraciones. Antes de publicar para usuarios aún
deben configurarse los dominios permitidos (`ALLOWED_HOSTS`), el acceso desde el
frontend y el servicio de archivos estáticos del administrador de Django. El
proyecto todavía no configura `STATIC_ROOT` ni WhiteNoise.

Referencia: [guía oficial de Django en Railway](https://docs.railway.com/guides/django)
y [detección de Python con Railpack](https://railpack.com/languages/python/).
