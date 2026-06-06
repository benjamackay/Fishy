# juego_backend

Backend Django + PostgreSQL en Docker para el videojuego.

## Estructura

```
juego_backend/
├── docker-compose.yml       ← orquesta los contenedores
├── Dockerfile               ← imagen del servidor Django
└── backend/
    ├── requirements.txt     ← dependencias Python
    ├── manage.py
    ├── juego_backend/       ← configuración del proyecto
    │   ├── settings.py
    │   ├── urls.py
    │   └── wsgi.py
    └── api/                 ← app principal (modelos, vistas, etc.)
        ├── models.py        ← define aquí la BD
        ├── serializers.py
        ├── views.py
        ├── urls.py
        └── admin.py
```

## Comandos

### Levantar el proyecto (primera vez)
```bash
docker compose up --build
```

### Aplicar migraciones (en otra terminal)
```bash
docker compose exec web python manage.py migrate
```

### Crear superusuario para el panel admin
```bash
docker compose exec web python manage.py createsuperuser
```

### Uso normal
```bash
docker compose up       # levanta todo
docker compose down     # apaga todo
docker compose down -v  # apaga y borra la base de datos
```

### Reiniciar el proyecto (uso diario, sin reconstruir)
Si ya lo levantaste antes, no hace falta `--build` ni volver a migrar. Reusa los contenedores existentes:
```bash
docker start backend-db-1 backend-web-1
```
Verifica que esté arriba:
```
http://127.0.0.1:8000/api/health/   →   {"status":"ok"}
```

Para apagarlo sin perder datos:
```bash
docker compose stop      # o: docker stop backend-db-1 backend-web-1
```
> ⚠️ No uses `docker compose down -v`: ese comando **borra la base de datos**.

> Nota (solo si tienes Docker Desktop **y** Docker dentro de WSL en la misma
> máquina): ejecuta estos comandos siempre en la terminal de **WSL/Ubuntu**, no
> en PowerShell. Si los corres en el motor equivocado se crea un stack duplicado
> vacío (mismos nombres de contenedor) que no contiene tus datos.

### Después de modificar models.py
```bash
docker compose exec web python manage.py makemigrations
docker compose exec web python manage.py migrate
```

## Endpoints disponibles

| Método | URL | Descripción |
|--------|-----|-------------|
| GET | `/api/health/` | Verifica que el servidor responde |
| GET | `/admin/` | Panel de administración Django |
