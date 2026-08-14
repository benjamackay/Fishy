# juego_backend

Backend Django + Django REST Framework del videojuego **Fishy!**.
La base de datos es **Supabase** (Postgres administrado en la nube).

## Arquitectura

```
Unity (ApiManager.cs)  ──HTTP + Token──▶  Backend Django (DRF)  ──Postgres/SSL──▶  Supabase
```

- Unity **nunca** habla con la BD directamente: pasa por el backend, que se
  autentica con un **Token** del adulto responsable (ver *Modelo de datos*).
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

3. La BD compartida ya está migrada y con el banco cargado, así que **no hace
   falta correr `migrate` ni `cargar_banco`** para empezar. Solo si vas a usar
   el panel `/admin/` necesitas una cuenta (pide nombre, **email** y contraseña).
   Carga antes el `.env` como se explica en *Cómo correr* — cualquier comando
   `manage.py` necesita las credenciales en el entorno:
   ```powershell
   .\.venv\Scripts\python .\backend\manage.py createsuperuser
   ```

## Si ya tenías el repo antes de la Fase 2

El commit *Cablear el modelo de control parental* cambió el modelo de usuarios y
**reseteó las migraciones**. Después de hacer `pull`:

1. **Reinstala las dependencias** — entró `argon2-cffi`, y sin él Django no
   arranca:
   ```powershell
   .\.venv\Scripts\python -m pip install -r backend/requirements.txt
   ```

2. **Limpia los `.pyc` viejos de migraciones**. El pull borra los archivos
   `0002..0006`, pero pueden quedar sus compilados:
   ```powershell
   Remove-Item -Recurse -Force .\backend\api\migrations\__pycache__
   ```

3. **No corras `migrate`**: la BD compartida ya fue reseteada y migrada el
   2026-08-12. Comprueba que estás en sync — debe decir *No changes detected*:
   ```powershell
   .\.venv\Scripts\python .\backend\manage.py makemigrations --check --dry-run
   ```

4. **Tu cuenta vieja ya no existe.** El reset vació las tablas, así que hay que
   registrarse de nuevo (y ahora el registro **exige email**). Los superusuarios
   también hay que recrearlos con `createsuperuser`.

> ⚠️ **Unity no conecta contra el backend real** hasta que se haga la Fase 3:
> la API cambió de contrato (ver más abajo). El `useLocalMode` de `ApiManager`
> sigue funcionando, así que se puede jugar y presentar sin backend.

## Cómo correr

### Atajo: `run.sh` / `run.ps1` (recomendado)

Cargan el `.env` solos y eligen el intérprete correcto. Mismos modos en ambos:

```bash
# Git Bash o WSL
cd Backend
./run.sh                 # servidor en 127.0.0.1:8000
./run.sh 0.0.0.0:8000    # escuchando en todas las interfaces
./run.sh --check         # config + drift de migraciones
./run.sh --smoke         # smoke test (con el servidor ya corriendo aparte)
```

```powershell
# PowerShell
cd Backend
.\run.ps1
.\run.ps1 0.0.0.0:8000
.\run.ps1 --check
.\run.ps1 --smoke
```

Si `run.sh` no quedó ejecutable: `bash run.sh`. Si PowerShell bloquea el script:
`powershell -ExecutionPolicy Bypass -File .\run.ps1`.

> `run.ps1` está guardado en **UTF-8 con BOM** a propósito: Windows PowerShell 5.1
> lee los `.ps1` sin BOM como ANSI y destroza los acentos de los mensajes. Si lo
> editas, guárdalo con BOM.

> ⚠️ **Desde WSL no conecta a Supabase.** El host directo
> `db.<ref>.supabase.co` solo publica IPv6 y WSL no tiene IPv6 → *Network is
> unreachable*. Hay que usar el **Session Pooler** (IPv4); `run.sh` lo detecta y
> te dice qué exportar. Desde Windows funciona sin tocar nada.

### A mano

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

# 2. Con el servidor corriendo en otra terminal, el smoke test end-to-end:
.\.venv\Scripts\python .\scripts\smoke_test.py
```

`scripts/smoke_test.py` recorre el flujo completo contra Supabase (health →
registro y login del adulto → perfiles de menores → partida → NPC → chat →
mensajes → banco), comprueba que un adulto **no** pueda ver ni tocar los datos
de otro, y borra los datos de prueba al terminar. Debe cerrar con
`46 OK, 0 fallas`. Con `--no-limpiar` deja los datos para inspeccionarlos en
Supabase → Table Editor.

Si el smoke test empieza a fallar después de tocar la API, es que **cambió el
contrato**: es su función avisarlo. Actualízalo junto con el cambio.

También está la colección de Postman (`Postman/Fishy_API.postman_collection.json`)
para pruebas manuales, con `baseUrl = http://127.0.0.1:8000/api`.

### Sobre la latencia

Supabase está en la nube, así que **cada consulta paga ~65 ms de ida y vuelta** y
abrir la conexión cuesta ~500 ms más. Como una request de la API hace varias
consultas, lo normal es ver **600–800 ms por request**. No es un error.

`DB_CONN_MAX_AGE` (en `.env`) reutiliza la conexión y ahorra esos ~500 ms, pero
**solo funciona con un servidor de workers persistentes** (gunicorn). Con
`runserver`, que crea un hilo nuevo por request, no sirve de nada y además deja
conexiones colgando — y Supabase solo permite 60. Por eso viene en `0`.

`registro` y `login` antes tardaban 2–5 s por el hashing de la contraseña
(PBKDF2 con 1.5M iteraciones — CPU local, no Supabase). Desde la Fase 2 se usa
**Argon2** (`PASSWORD_HASHERS` en `settings.py`) y bajaron a ~1,3 s y ~684 ms
respectivamente, o sea prácticamente el piso de latencia de cualquier request.
PBKDF2 queda de respaldo en la lista para poder validar contraseñas hasheadas
antes del cambio: Django las rehashea sola en el primer login exitoso.

Desde **Unity**: en el componente `ApiManager`, dejar `useLocalMode` **desactivado**
y `baseUrl = http://127.0.0.1:8000/api`, y darle Play. ⚠️ Esto **no funciona
hasta la Fase 3**: la API espera ahora un perfil de menor que `ApiManager`
todavía no sabe pedir.

## Endpoints principales

| Método | URL | Descripción |
|--------|-----|-------------|
| GET | `/api/health/` | Ping del servidor |
| POST | `/api/auth/registro/` | Registro del adulto responsable (devuelve token) |
| POST | `/api/auth/login/` | Login (devuelve token) |
| GET | `/api/auth/perfil/` | Datos de la cuenta autenticada |
| GET/POST | `/api/jugadores/` | Perfiles de menores del adulto autenticado |
| GET/PATCH/DELETE | `/api/jugadores/<id>/` | Detalle de un perfil de menor |
| GET | `/api/jugadores/<id>/partidas/` | Partidas de un perfil (para retomar el avance) |
| POST | `/api/partidas/` | Crear partida (requiere `usuario_jugador_id`) |
| POST | `/api/chats/` | Iniciar chat |
| GET | `/api/banco/preguntas/` | Banco de preguntas (filtros por query params) |
| GET | `/admin/` | Panel admin de Django |

## Modelo de datos — control parental

Las tablas del juego llevan prefijo `api_` (convención de Django). Las tablas
`auth_*`, `authtoken_*`, `django_*` son infraestructura de Django (permisos,
tokens de login, sesiones, historial de migraciones) — normales, no se tocan.

Desde la **Fase 2** el modelo de usuarios es de control parental:

```
AdultoResponsable  (única cuenta con login — es el AUTH_USER_MODEL)
      │ 1─N
      ▼
UsuarioJugador     (perfil del menor, SIN credenciales propias)
      │ 1─N
      ▼
Partida ──► NPC ──► Chat ──► Mensaje
```

El adulto se autentica y todo lo que consulta se filtra por
`usuario_jugador__adulto=request.user`: nunca puede ver los datos de otro
adulto ni colgar una partida de un perfil ajeno.

**Cada menor conserva su propio avance:** la partida cuelga del perfil, así que
los hermanos no comparten progreso.

**Flujo típico del cliente:** `registro`/`login` → `GET /jugadores/` (o
`POST /jugadores/` si es la primera vez) → elegir perfil →
`GET /jugadores/<id>/partidas/` → si trae algo se retoma esa partida, y si viene
vacía se crea una con `POST /partidas/` → resto del juego igual que antes.

### Probarlo a mano

Con el servidor corriendo, los tres pasos que cambiaron respecto de la Fase 1:

```bash
# 1. Registro del adulto — el email ahora es obligatorio y único
curl -X POST http://127.0.0.1:8000/api/auth/registro/ \
  -H "Content-Type: application/json" \
  -d '{"nombre":"papa_demo","email":"papa@ejemplo.cl","password":"clave1234"}'
# → {"token":"abc123...","adulto_id":1}      ojo: adulto_id, ya no usuario_id

# 2. Crear el perfil del menor (con el token del paso 1)
curl -X POST http://127.0.0.1:8000/api/jugadores/ \
  -H "Content-Type: application/json" -H "Authorization: Token abc123..." \
  -d '{"nombre":"Benja","edad":9}'
# → {"id":1,"adulto":1,"nombre":"Benja",...}

# 3. ¿Ese perfil ya tiene avance? (vacío la primera vez)
curl http://127.0.0.1:8000/api/jugadores/1/partidas/ \
  -H "Authorization: Token abc123..."
# → []   ...entonces se crea; si trae partidas, se retoma la primera

# 4. Crear la partida colgada de ese perfil
curl -X POST http://127.0.0.1:8000/api/partidas/ \
  -H "Content-Type: application/json" -H "Authorization: Token abc123..." \
  -d '{"usuario_jugador_id":1,"progreso":0}'
```

De ahí en adelante (NPCs, chats, mensajes, banco) todo funciona igual que antes,
usando el `id` de la partida.

**Errores esperables y qué significan:**

| Respuesta | Causa |
|---|---|
| `400` en el registro | falta el email, o ya existe ese email/nombre |
| `404` al crear partida | falta `usuario_jugador_id`, o el perfil es de otro adulto |
| `400` al crear perfil | ya tienes otro perfil con ese mismo nombre |
| `401` en todo | falta el header `Authorization: Token ...` o el token no vale |

`Zona` existe como tabla pero todavía no está relacionada con `NPC`/`Chat`;
queda para la HDU de riesgo por zona.

> **Ojo (Fase 3 pendiente):** la Fase 2 rompe el contrato de la API, así que
> `ApiManager.cs` en Unity deja de conectar contra el backend real hasta que se
> agregue el paso de selección de perfil. El `useLocalMode` sigue funcionando.
