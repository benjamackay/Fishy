# Django en Railway

El `requirements.txt` de la raíz contiene las dependencias de Django y Gunicorn
sin referencias a otros archivos. Así puede instalarse durante la etapa de
dependencias de Railpack, antes de que esté disponible el resto del código.
Mantener su lista sincronizada con `Backend/backend/requirements.txt`, que usan
las herramientas de desarrollo y Docker del backend.

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
- `CORS_ALLOWED_ORIGINS`: dominio(s) del portal web que pueden llamar a la API,
  con esquema y sin `/` final, separados por coma. Ej: `https://portal.fishy.cl`.
  Sin esto el navegador bloquea las llamadas del portal desplegado.
- `DJANGO_ALLOWED_HOSTS` (opcional): dominios extra, separados por coma. El dominio
  de Railway (`fishy-test.up.railway.app`) no hace falta ponerlo: se toma solo de
  `RAILWAY_PUBLIC_DOMAIN`, que Railway define en cada servicio con dominio público.

El backend actual lee estas variables `DB_*`, no `DATABASE_URL`.
El correo sigue desactivado hasta configurar el proveedor según `.env.example`.

## Alcance

La API queda en `https://fishy-test.up.railway.app/api/` (salud:
`/api/health/`). Con `DJANGO_DEBUG=False`, el servidor solo acepta peticiones
dirigidas a su dominio y no arranca si no sabe cuál es. Detrás del proxy de
Railway toma la petición como https, así que el login del `/admin/` pasa el CSRF.

Falta el servicio de archivos estáticos del administrador de Django: el proyecto
todavía no configura `STATIC_ROOT` ni WhiteNoise, así que el `/admin/` se ve sin
estilos. No cambia modelos ni migraciones.

Referencia: [guía oficial de Django en Railway](https://docs.railway.com/guides/django)
y [detección de Python con Railpack](https://railpack.com/languages/python/).
