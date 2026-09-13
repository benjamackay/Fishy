# Revisión de migraciones de web

## Estado posterior a la reversión solicitada

El 13 de septiembre de 2026 se restauraron `models.py`, `admin.py` y
`0012_personaje_zona_actual.py` desde `origin/dev` (`a1ce141`). Se retiraron los
modelos, la migración y la implementación de grupos e invitaciones, con sus
dependencias. Sus rutas ahora responden 503; la demo del frontend permanece.
El serializer del personaje también admite `zona_actual`. Los reportes
individuales y los roles del portal se conservan.

Validación posterior: 160 pruebas del backend aprobadas en SQLite aislado;
`makemigrations --check --dry-run` sin cambios; preflight contra Supabase en
solo lectura con 16 comprobaciones correctas, cero fallos y ninguna migración
pendiente. Se compararon los modelos, el admin y todos los archivos de migración
con `origin/dev`: coinciden en contenido.

No se ejecutó una reversión en Supabase: la migración de grupos no estaba
aplicada y la del personaje ya lo estaba. Los hallazgos siguientes documentan
el estado anterior a este cambio, no tareas pendientes de la versión actual.

## Diagnóstico anterior a la reversión

Revisión del 13 de septiembre de 2026. Código local y `origin/web`: `1065c32`.
Referencia consultada de `origin/dev`: `a1ce141`. Se actualizaron las referencias
remotas sin cambiar de rama, hacer pull ni integrar código de dev.

## Resultado

Los modelos de esta versión de **web coinciden con sus propios archivos de
migración**. `makemigrations --check --dry-run` devuelve `No changes detected`
y el grafo no tiene dependencias faltantes ni conflictos.

Sin embargo, **la base compartida no tiene el mismo esquema que web**:

| Diferencia confirmada en Supabase | Consecuencia |
|---|---|
| `api.0012_grupos_invitaciones` pendiente | No existen `api_grupotutor`, `api_miembrogrupo` ni `api_invitaciongrupo`. Las operaciones reales de grupos e invitaciones necesitan esas tablas. |
| `api.0012_personaje_zona_actual` aplicada, pero ausente en web | La columna `api_personajejugador.zona_actual` existe en la base, es `NOT NULL` y no tiene valor predeterminado en PostgreSQL. El modelo de web no la conoce y la omite al crear un personaje. |

El segundo problema se reprodujo en una base SQLite en memoria que imitaba esa
columna: crear `PersonajeJugador` desde el modelo actual viola `NOT NULL`.
No se creó ningún personaje de prueba en Supabase.

Que `makemigrations --check` pase no demuestra que la base compartida coincida:
compara los modelos con el historial disponible en la rama. Tampoco ejecutar
solamente la migración de grupos corrige el modelo del personaje.

## Scripts encontrados

Desde `Backend`, con el entorno virtual funcionando:

```powershell
.\run.ps1 --check
.\run.ps1 --global --fase 1
```

- `--check` ejecuta `check` y `makemigrations --check --dry-run`. Detecta
  discrepancias locales; no crea archivos ni aplica migraciones.
- `--global --fase 1` ejecuta el preflight de `scripts/test_global.py`: revisa
  configuración, grafo, modelos, conexión, migraciones pendientes y catálogos.
  Tampoco aplica migraciones. Se ejecutó esta fase con PostgreSQL forzado a
  solo lectura: **15 comprobaciones correctas y 1 fallo**, por
  `0012_grupos_invitaciones` pendiente. Esta fase no detecta por sí sola todas
  las columnas adicionales de otras ramas; la comparación de esquema de esta
  revisión encontró `zona_actual`.
- `run.sh` tiene las opciones equivalentes para Git Bash/Linux.
- `scripts/respaldar_bd.py` existe en dev, no en esta versión de web. Hace
  respaldos de datos; no resuelve modelos ni ejecuta migraciones.

No se encontró un script independiente que reconcilie y aplique automáticamente
las migraciones en las versiones revisadas de web y dev. No ejecutar el test
global completo para una revisión de esquema: otras fases crean datos de prueba.

## Qué hay que coordinar para corregirlo

1. Alinear el campo `PersonajeJugador.zona_actual` y su migración histórica con
   el código que se vaya a ejecutar contra la base compartida.
2. Si esa versión reúne los cambios de grupos y personaje, conservar
   `0012_grupos_invitaciones` y `0012_personaje_zona_actual`. Ambas parten de
   `0011_progreso_npcs`: requieren una migración de unión dependiente de las dos.
   El prefijo numérico compartido no autoriza a borrar ni renombrar una de ellas.
3. Verificar el estado resultante contra los modelos y ejecutar las migraciones
   en una base de prueba. Revisar el plan de Supabase antes del despliegue:

   ```powershell
   # Después de cargar Backend/.env en esa terminal.
   .\.venv\Scripts\python .\backend\manage.py showmigrations api
   .\.venv\Scripts\python .\backend\manage.py migrate --plan
   ```

4. Con el código alineado, respaldo y despliegue coordinado, aplicar las
   migraciones pendientes con el procedimiento del equipo. Verificar la creación
   de un personaje y un grupo en el entorno de prueba antes de usar la base
   compartida.

No usar `--fake`, borrar tablas o regenerar todo el historial para silenciar
el error. Tampoco copiar solo una migración de dev dejando fuera su campo del
modelo: eso genera otra discrepancia y puede proponer eliminar la columna.

## Incidencia local adicional

El ejecutable de `Backend/.venv` de este computador apunta a un Python 3.12 que
ya no está disponible. Por eso `run.ps1` falla antes de llegar a Django. La revisión
usó otro intérprete disponible con las dependencias existentes; este problema
local no demuestra que los demás integrantes tengan el mismo entorno roto.
Recrear el entorno virtual local con un Python instalado resuelve ese arranque,
pero no las diferencias de esquema descritas arriba.

La inspección de Supabase leyó exclusivamente metadatos y conteos de catálogo.
No aplicó migraciones, no cambió los modelos y no integró ramas. El mensaje exacto
que obtenga otro integrante puede revelar además un problema propio de su copia.
