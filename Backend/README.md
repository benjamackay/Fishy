# juego_backend

Backend Django + Django REST Framework del videojuego **Fishy!**.
La base de datos es **Supabase** (Postgres administrado en la nube).

## Arquitectura

```
Unity (ApiManager.cs)  ──HTTP + Token──▶  Backend Django (DRF)  ──Postgres/SSL──▶  Supabase
```

- Unity **nunca** habla con la BD directamente: pasa por el backend, que se
  autentica con un **Token** de usuario.
- Solo Django se conecta a Supabase, con la **cadena de conexión de Postgres**
  (usuario/contraseña). **No se usa la API REST ni la anon key de Supabase.**
- Por eso las tablas están **sin RLS** (Row Level Security) y es correcto: nadie
  las expone por la Data API. ⚠️ **No habilitar/usar la Data API de Supabase**
  para tocar estas tablas; todo pasa por Django.

## Estructura

```
Backend/
├── docker-compose.yml       ← solo el servicio `web` (Django); lee .env
├── Dockerfile               ← imagen del servidor Django
├── .env                     ← credenciales (NO se commitea; ver .gitignore)
├── .env.example             ← plantilla versionada
└── backend/
    ├── requirements.txt
    ├── manage.py
    ├── juego_backend/        ← settings.py, urls.py, wsgi.py
    └── api/                  ← app principal (models, serializers, views, urls)
```

## Configuración inicial (una vez por máquina)

1. Copia la plantilla y rellena la contraseña de la BD:
   ```
   cp .env.example .env
   ```
   Edita `.env` y pon la `DB_PASSWORD` (Supabase → Project Settings → Database).
   El resto de valores ya vienen apuntando al proyecto Supabase.

   > Si `db.<ref>.supabase.co` no conecta (timeout / IPv6), usa el **Session
   > Pooler** que da IPv4: cambia en `.env` `DB_HOST` a
   > `aws-0-<region>.pooler.supabase.com` y `DB_USER` a `postgres.<project-ref>`.

2. Crea el entorno virtual e instala dependencias:
   ```
   python -m venv .venv
   .venv/Scripts/python -m pip install -r backend/requirements.txt
   ```

## Cómo correr

`settings.py` lee la configuración de **variables de entorno**, así que hay que
cargar el `.env` antes de cualquier comando `manage.py`. En PowerShell:

```powershell
Get-Content .\.env | ForEach-Object {
  if ($_ -match '^\s*([^#][^=]*)=(.*)$') { Set-Item -Path ("env:" + $matches[1].Trim()) -Value $matches[2].Trim() }
}
```

Luego, en la misma sesión:

```powershell
# Levantar el servidor
.\.venv\Scripts\python .\backend\manage.py runserver
# → http://127.0.0.1:8000/api/health/  debe responder {"status": "ok"}
```

> **Alternativa Docker** (solo sirve el servidor; ya no hay Postgres local):
> `docker compose up --build`. El contenedor `web` lee el `.env` y se conecta a
> Supabase. Ojo: el comando `cargar_banco` **no** funciona dentro de Docker
> (la ruta del JSON del banco no existe ahí); úsalo con el venv local.

## Migraciones y carga de datos

```powershell
# Tras modificar api/models.py:
.\.venv\Scripts\python .\backend\manage.py makemigrations
.\.venv\Scripts\python .\backend\manage.py migrate

# Cargar el banco de preguntas (banco_preguntas/banco_preguntas.json en la raíz del repo):
.\.venv\Scripts\python .\backend\manage.py cargar_banco
```

> **Regla de oro:** el esquema de la BD se maneja **solo desde Django**
> (`makemigrations` + `migrate`). **Nunca** crees ni edites tablas/columnas desde
> la UI de Supabase: Django dejaría de saber cómo es la BD y se desincroniza.

## Verificar que funciona

```powershell
# 1. Código y BD en sync (debe decir "No changes detected"):
.\.venv\Scripts\python .\backend\manage.py check
.\.venv\Scripts\python .\backend\manage.py makemigrations --check --dry-run

# 2. Salud del servidor: abrir http://127.0.0.1:8000/api/health/

# 3. Flujo completo: correr en Postman (Postman/Fishy_API.postman_collection.json)
#    Registro → Login → Crear Partida  (baseUrl = http://127.0.0.1:8000/api)

# 4. Ver datos en Supabase → Table Editor (api_usuario, api_partida, ...)
```

Desde **Unity**: en el componente `ApiManager`, dejar `useLocalMode` **desactivado**
y `baseUrl = http://127.0.0.1:8000/api`, y darle Play.

## Endpoints principales

| Método | URL | Descripción |
|--------|-----|-------------|
| GET | `/api/health/` | Ping del servidor |
| POST | `/api/auth/registro/` | Registro (devuelve token) |
| POST | `/api/auth/login/` | Login (devuelve token) |
| POST/GET | `/api/partidas/` | Crear partida |
| POST | `/api/chats/` | Iniciar chat |
| GET | `/api/banco/preguntas/` | Banco de preguntas (filtros por query params) |
| GET | `/admin/` | Panel admin de Django |

## Modelo de datos

Las tablas del juego llevan prefijo `api_` (convención de Django). Las tablas
`auth_*`, `authtoken_*`, `django_*` son infraestructura de Django (permisos,
tokens de login, sesiones, historial de migraciones) — normales, no se tocan.

Modelos nuevos de **control parental** (creados, aún **no cableados** al flujo
del juego — es la Fase 1): `AdultoResponsable`, `UsuarioJugador`, `Zona`.
El plan de Fases 2 (recablear backend) y 3 (Unity) está pendiente.
