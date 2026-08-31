# Migración de la base de datos a Supabase

**Fecha:** 2026-08-09 · **Alcance:** Fase 1 (infraestructura). Ver el uso día a día
en [`README.md`](./README.md).

## Qué se hizo

Se movió la base de datos del backend desde un **Postgres local en Docker** a
**Supabase** (Postgres administrado en la nube, proyecto `cgolqpchpvyuvnyejsyq`).

Enfoque elegido: **Django adelante, Supabase solo como Postgres.** No se usa la
API REST ni la anon key de Supabase; solo Django se conecta con la cadena de
Postgres. Se descartó el enfoque "Supabase como backend directo" (habría exigido
reescribir todo el `ApiManager` de Unity y definir políticas RLS).

## Cambios en el repo

| Archivo | Cambio |
|---|---|
| `backend/juego_backend/settings.py` | `DATABASES` lee de variables de entorno + `sslmode=require` (Supabase exige SSL). |
| `docker-compose.yml` | Se **eliminó** el servicio `db` (Postgres local), el volumen `postgres_data` y el `depends_on`. `web` ahora usa `env_file: .env`. |
| `backend/api/models.py` | +3 modelos de control parental: `AdultoResponsable`, `UsuarioJugador`, `Zona`. |
| `backend/api/migrations/0006_*` | Migración de los modelos nuevos. |
| `.env.example` | Plantilla de credenciales (versionada). |
| `.env`, `.venv/` | Locales, **no** se commitean (`.gitignore` actualizado). |

## Sobre las tablas que estaban en Supabase

El equipo había creado 12 tablas **a mano** en Supabase. Se detectó que:

- Estaban **rotas relacionalmente**: todas las FK apuntaban sobre la columna `id`
  (`id → id`), sin columnas de relación reales (`partida_id`, `npc_id`, …). Con
  ese diseño el juego no podía guardar relaciones.
- Eran **incompatibles** con los modelos de Django (nombres distintos, faltaban
  columnas que usa el código).
- Estaban **vacías** (solo 1 fila de prueba en `usuario`).

Decisión: se **borraron las 12** y Django recreó el esquema correcto con
`migrate`. La *intención* de diseño (control parental + zonas) se conservó
re-expresándola como modelos Django bien hechos (los 3 nuevos de arriba).

## Estado tras la migración (verificado)

- 21 tablas en `public`: 13 del juego (`api_*`) + 8 de sistema de Django.
- FKs correctas (columnas `*_id` reales). `makemigrations --check` → sin drift.
- Banco cargado: **24 preguntas, 57 opciones**.
- Smoke test end-to-end OK: health → registro → login → crear partida → persiste
  en Supabase.

## Pendiente (siguientes fases del control parental)

- **Fase 2 (backend):** `AdultoResponsable` como `AUTH_USER_MODEL` (login con
  hashing); `Partida.usuario` → `Partida.usuario_jugador`; ajustar las ~8 vistas
  (`usuario=request.user` → `usuario_jugador__adulto=request.user`); endpoints
  `POST/GET /jugadores/`; serializers.
- **Fase 3 (Unity):** reescribir `ApiManager.cs` (paso de elegir/crear perfil de
  menor + modo local) y armar la pantalla de selección de perfil.
- Confirmar con el equipo: cardinalidad adulto↔jugador y relación de `Zona` con
  `NPC`/`Chat`/`Partida` antes de cablear.
