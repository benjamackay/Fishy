# Requisitos de datos — Fishy!

Estado al **2026-09-29**. Este documento nació como la lista de pedidos para Óscar
(guardado, misiones, recompensas). Óscar ya los implementó en `dev` (commits
`cf3f435`, `f016f27`, `39e24a4`) y Unity ya los consume, así que ahora es el
**registro de lo que se construyó** más lo poco que sigue abierto.

Reemplaza a `REQUISITOS_BD_MISIONES.md`, `RespuestasRequisitosBD_Misiones.txt`
(Óscar, 2026-09-11) y `Backend/PENDIENTE_HDU11_RECOMPENSAS_DETECTIVE.md`.

## Resumen

| Pedido | Backend | Unity |
|---|---|---|
| A.3 Conversación completa atómica | Hecho: `POST /partidas/{id}/chats/completo/` | Hecho: `ChatBackendLogger.Subir` lo usa; si el servidor no lo tiene (404) cae a la cadena antigua |
| B.1/B.2 Catálogo de misiones | Hecho: `GET /misiones/` y `/misiones/{id}/`, tablas `Mision` + `ObjetivoMision`, migración 0016 | Hecho: `MisionCatalogoSync` lo baja al arrancar, con `misiones.json` como respaldo |
| B.3 Avance por objetivo | Hecho: `GET/POST /partidas/{id}/objetivos/`, tabla `ObjetivoProgreso` | Hecho: `ObjetivosBackendSync` (ver B.3) |
| B.5 Recargar el banco sin borrar el álbum | Hecho: `--limpiar` ya no toca misiones ni álbum; `--borrar-misiones` es explícito | — |
| C. Recompensas Detective (HDU-11) | Hecho: 5 campos `recompensa_*` en `CasoDetective`, migración 0015 | Sin cambios: ya prefería el dato del backend sobre el catálogo local |

Migraciones nuevas en `dev`: `0013_adulto_rol`, `0014_grupos_invitaciones`
(portal web, no afectan al juego), `0015_recompensa_caso_detective`,
`0016_catalogo_misiones_y_objetivos`. Todas con `db_default`, porque la base de
Supabase es compartida con ramas que todavía no conocen las columnas nuevas.

Los pedidos **nuevos** del 29 de septiembre están en la sección D, al final.

## Lo que sigue abierto

1. **Confirmar que la base compartida tiene aplicadas la 0015 y la 0016** y que se
   corrieron `cargar_banco` y `cargar_detective` después. Las migraciones existen
   en el repo, pero que estén aplicadas en Supabase no se puede ver desde el código.
   Sin la 0016, `GET /misiones/` falla y Unity cae al `misiones.json` local; sin la
   0015, los casos Detective siguen usando el catálogo local de recompensas.
2. **`cargar_banco` lee `Fishy!/Assets/Resources/misiones.json`** por defecto
   (`--archivo-misiones` para otra ruta, `--sin-misiones` para omitirlo). Un servidor
   desplegado sin la carpeta de Unity falla con `CommandError`, a propósito, y hay
   que pasar una de las dos opciones.
3. **Ninguna misión del catálogo usa aún `desbloquea_mision` ni
   `recompensa_item_id`.** Los campos existen y viajan; falta decidir contenido.
4. **`orden` sigue siendo una suposición** del archivo (progresión de zonas), no el
   orden narrativo confirmado.
5. **Ids inestables:** `MISION_NPC_03`, `MISION_NPC_04` y `MISION_PANTANO_CRIATURAS`
   pueden cambiar. Por eso `desbloquea_mision` y `ObjetivoProgreso.mision_id` son
   texto y no FK: reemplazar una misión no se lleva el progreso de nadie.
6. **HDU-12 completa** (álbum de evidencias agrupado por caso) sigue fuera de
   alcance; lo que hay es el álbum mínimo como ítem de mochila.

---

## A. Guardado — el contrato

El juego acumula cambios en una cola en memoria (`ColaDeCambios`) y los manda al
cambiar de zona y al cerrar. Detalle en `Fishy!/documentacion/README_GUARDADO.md`.
Lo que le toca a la base:

- **A.1 Camino de ida.** Completar una misión, desbloquear una zona y cumplir un
  objetivo no retroceden. Los avisos pueden llegar desordenados o repetidos; un
  registro que retrocede es un dato falso en el reporte del adulto.
- **A.2 Idempotencia.** La cola reintenta hasta 3 veces, y en un cierre puede quedar
  una petición sin respuesta que no se reencola. Marcar lo mismo dos veces tiene que
  ser un no-op, no un error ni una fila duplicada (`UniqueConstraint` por partida).
- **A.3 Conversación completa.** Antes: `RegistrarNPC` → `IniciarChat` → N ×
  `RegistrarMensaje` → `FinalizarChat`, ~9 peticiones en serie (~6 s) y con riesgo
  de dejar una conversación partida. Ahora un solo POST atómico:

```
POST /api/partidas/{partida_id}/chats/completo/
{
  "npc":  { "nombre": "Alex", "area": "zona_2", "tipo": "enemigo", "confianza": 0 },
  "chat": { "categoria_riesgo": "desconocidos" },
  "mensajes": [ { "tipo": "start|request|chain", "respuesta": "...",
                  "calidad_respuesta": "...", "pregunta_banco_id": "...",
                  "opcion_banco_id": "...", "posibles_respuestas": [ ... ] } ],
  "finalizar": true,
  "respuesta_final": "..."
}
```

Acepta `npc.npc_id` para reusar un NPC ya existente en la partida. Las filas que
crea son las mismas que la cadena larga, así que el riesgo por zona no cambia.
Unity no deja `NpcId`/`ChatId` puestos tras usarlo: la conversación ya llega cerrada.

---

## B. Misiones

### B.1 Origen del contenido

`cargar_banco` llena `Mision` desde el banco de preguntas (id y nombre de 9
misiones) y luego le agrega encima lo que solo existe en `misiones.json`: `orden`,
`zona_objetivo`, `descripcion`, objetivos, `desbloquea_mision`,
`recompensa_item_id`/`recompensa_cantidad` y 3 misiones más. Una misión que está en
el archivo y no en el banco se crea igual. `MISION_NPC_01` y `MISION_NPC_02` se
guardan aunque hoy no se usen.

Los objetivos se resincronizan enteros en cada carga (son contenido); el avance del
niño no cuelga de ellos, ver B.3.

### B.2 Contrato de `GET /misiones/`

- Raíz: **arreglo**, no `{version, misiones}` (Unity lo lee como `List<MisionRegistro>`).
- Sin sesión ni partida: es contenido, se puede pedir "en frío". Filtro `?zona=`.
- `GET /misiones/{id}/` devuelve una sola, con sus objetivos.
- `titulo` y `nombre` viajan los dos (alias).
- Misión sin objetivos: `objetivos: []`. Campos de un objetivo que no aplican a su
  tipo: vacíos (`""` / `0`), nunca ausentes.

### B.3 Avance por objetivo

```
GET / POST /api/partidas/{id}/objetivos/
{ "mision_id": "...", "orden": 1, "cumplido": true }
```

Clave `(partida, mision_id, orden)`, sin FK a `ObjetivoMision`. Un objetivo que ya
no está en el catálogo se guarda igual (y se avisa por log).

Cómo lo usa Unity (`ObjetivosBackendSync`):

- **Se sincronizan** `hablar_npc`, `chatear_telefono`, `llegar_zona` y
  `completar_caso_detective`, solo los que vienen del catálogo (`orden` > 0).
- **No se sincroniza `recoger_objeto`:** se recalcula de la mochila, que ya se
  guarda aparte. Guardarlo sería una segunda fuente de verdad.
- Al entrar a la partida baja el avance y marca esos objetivos como cumplidos en
  `MissionTracker`, también si la misión se entrega después de bajarlo. Un objetivo
  `llegar_zona` guardado se da por cumplido aunque Otto ya no esté en esa zona.

### B.4 Cinco tipos de objetivo

`recoger_objeto`, `hablar_npc`, `chatear_telefono`, `llegar_zona`,
`completar_caso_detective`. El tipo viaja en texto porque el enum de Unity se puede
reordenar. `cargar_banco` falla si el archivo trae un tipo desconocido.

### B.5 Recargar el banco

`--limpiar` borra preguntas y diálogos, pero **ya no borra misiones ni álbum**
(borrar `Mision` arrastra `RecompensaAlbum` y con ella el álbum que los niños ya
ganaron). `--borrar-misiones` lo hace a propósito. Lo que el catálogo nuevo ya no
traiga se conserva.

### Anexo — las 12 misiones del catálogo

| zona | mision_id | orden | objetivos | zona_objetivo |
|---|---|---|---|---|
| desconocidos | `MISION_EXPLORACION_01` | 10 | 1 | zona_1 |
| desconocidos | `MISION_NPC_03` | 15 | 3 | zona_1 |
| desconocidos | `MISION_SEC_MOCHILA_HUEMUL` | 20 | 0 | zona_1 |
| desconocidos | `MISION_NPC_04` | 25 | 1 | zona_1 |
| desconocidos | `MISION_SEC_COLLAR_PUDU` | 30 | 0 | zona_1 |
| ciberacoso | `MISION_EXPLORACION_02` | 40 | 1 | zona_2 |
| ciberacoso | `MISION_SEC_MASCOTA_COIPO` | 50 | 0 | zona_2 |
| ciberacoso | `MISION_SEC_MEGAFONO_FLAMENCO` | 60 | 0 | zona_2 |
| ciberacoso | `MISION_PANTANO_CRIATURAS` | 65 | 4 | zona_2 |
| reto_viral | `MISION_EXPLORACION_03` | 70 | 0 | zona_3 |
| reto_viral | `MISION_SEC_TABLA_PINGUINO` | 80 | 0 | zona_3 |
| reto_viral | `MISION_SEC_SILBATO_LOBOMARINO` | 90 | 0 | zona_3 |

Sacado de `Fishy!/Assets/Resources/misiones.json` (verificado contra `GET /misiones/`
con la base cargada, 2026-09-20). Ante la duda, lee el archivo.

---

## C. Recompensas del Modo Detective (HDU-11)

`CasoDetective` tiene cinco campos planos (migración 0015): `recompensa_item_id`,
`recompensa_nombre`, `recompensa_accesorio_hdu06`, `recompensa_umbral_aciertos`
(0.5 por defecto) y `recompensa_no_duplica_al_repetir` (true). `cargar_detective`
los lee del bloque `recompensa` de `banco_preguntas/detective_cases.json` y
`CasoDetectiveSerializer` los expone.

En Unity, `DetectiveCaseManager.OtorgarRecompensaSiCorresponde` usa primero la
recompensa que trae el caso (`DetectiveCase.TieneRecompensa`) y solo si viene vacía
cae a `CatalogoRecompensasDetective` (`Resources/detective_recompensas.json`). Un
caso sin bloque `recompensa` queda con los campos vacíos y usa el catálogo local.

El álbum mínimo (`ITEM_ALBUM_EVIDENCIAS`, adelanto de HDU-12) es un ítem más de
mochila y no necesita backend. No hay endpoint para "reclamar" el pin: entra por el
guardado normal de la mochila.

---

## D. Pedidos nuevos (2026-09-29)

Salen de revisar los criterios de aceptación de "continuar partida" contra el código y
contra la base local. Cada uno trae la evidencia que lo motiva.

**Ninguno necesita migración de esquema salvo D.3**, que solo cambia `Meta.ordering`.

### D.1 — `fecha_update` no dice cuándo se guardó por última vez

> **Resuelto desde Unity (2026-10-05), sin tocar el servidor.** Cada guardado encola un
> `PATCH /partidas/{id}/` sin campos (`SaveManager`, clave `partida.ultimo_guardado`):
> la vista guarda la fila y `auto_now` pone `fecha_update` al día, así que la lista
> muestra el último guardado y se ordena por él. Lo de abajo queda como contexto; el
> pedido al backend ya no hace falta, aunque seguiría siendo más exacto para escrituras
> que no pasen por un guardado (ninguna, hoy).

**Era el único CA que fallaba.** El CA pide que la lista de sesiones muestre *"la fecha
y hora asociadas al último guardado de cada una"*, y muestra la de creación.

`Partida.fecha_update` es `auto_now=True`, así que solo se actualiza **cuando se guarda la
fila `Partida`**. Durante la partida no se guarda nunca: todas las escrituras van a otras
tablas (`api_personajejugador`, `api_misionprogreso`, `api_objetivoprogreso`…). La única
que toca esa fila es `PATCH /partidas/{id}/`, que Unity solo llama cuando cambia
`progreso` — y `progreso` casi nunca cambia (ver D.2).

Medido en la base local, partida 6, jugada anoche:

```
api_partida.fecha_update              2026-09-29 22:25:48   ← cuando se creó
api_personajejugador.fecha_actualiz.  2026-09-29 23:30:05   ← el último guardado real
```

Una hora de diferencia. Las seis partidas de la base tienen `fecha_update` a microsegundos
de `fecha_inicio`, o sea que ninguna se ha actualizado nunca.

**Arrastra dos cosas más**, porque `partidas_jugador` ordena por `-fecha_update`
(`views.py:133`):

- La lista sale ordenada por cuándo se **creó** cada sesión, no por cuándo se jugó.
- La etiqueta "Seguir donde quedaste" se la lleva la sesión equivocada.

Afecta a las dos puertas de entrada: `AuthScreen` (Boot) y el panel de `MenuDos` pintan la
misma `TextoDePartida`.

**Pedido:** que escribir cualquier progreso de una partida toque su `fecha_update`. Basta
un `partida.save(update_fields=["fecha_update"])` —`auto_now` hace el resto— en los
endpoints que ya reciben la partida:

| Vista | `views.py` |
|---|---|
| `personaje_partida` (PATCH) | 1224 |
| `inventario_partida` (PUT) | 1116 |
| `misiones_partida` (POST) | 993 |
| `objetivos_partida` (POST) | 778 |
| `zonas_partida` (POST) | 1063 |
| `objetos_recogidos_partida` (POST) | 1262 |
| `progreso_npcs_partida` (POST) | 1316 |
| `chat_completo` (POST) | 306 |
| `registrar_progreso_detective` (POST) | 913 — la partida viene en el cuerpo |

Sin migración: la columna ya existe con la semántica correcta, solo no se la estaba
tocando.

> Se descartó la alternativa de que Unity leyera `personaje.fecha_actualizacion`: el
> serializer no lo expone —habría que tocar el backend igual— y no arreglaría el orden,
> que se decide en el servidor.

### D.2 — `progreso` no mide nada, y hay que decidir qué debería medir

Las seis partidas de la base tienen `progreso = 0.0`. La partida 6 completó **cinco
misiones** y sigue en 0.

El campo solo lo mueve `BosqueDesconocidosManager` al cerrar su zona. El propio código de
Unity ya lo da por perdido: `TextoDePartida` esconde el dato si es 0 *"porque un 0%
explorado no informa"*.

Esto no es un bug con arreglo obvio, es una definición que falta. Dos caminos:

1. **Derivarlo en el backend** —por ejemplo, porcentaje de misiones del catálogo
   completadas en esa partida— y dejar de aceptarlo desde el cliente. Ventaja: deja de
   depender de que Unity se acuerde de mandarlo, y vale para el reporte del adulto.
2. **Mantenerlo escrito por el cliente** y definir qué lo mueve además del Bosque.

Recomendación: la 1. Es un dato que el servidor ya tiene entero y el cliente solo en
parte.

Relacionado, del lado de Unity: `WorldZoneManager.SetProgress()` **no lo llama nadie**, así
que la regla `progresoRequerido` de las zonas nunca se dispara. Hoy no rompe nada porque
todas están en `-1`, pero configurar una zona con umbral de progreso no la abriría jamás.

### D.3 — `PreguntaBanco.Meta.ordering` no es determinista entre motores

Único pedido con migración, y es de una línea.

```python
ordering = ["zona", "npc_id", "fase", "orden_en_fase", "escenario_id"]
```

`fase` y `orden_en_fase` son `null=True`. **SQLite ordena los NULL primero y PostgreSQL
los ordena últimos**, así que el mismo banco sale en orden distinto según el motor. Ya
costó un bug real: en local los chats con personajes sospechosos no se mostraban completos
—solo los neutros y los de detective— porque la conversación empezaba por su propio nodo
de cierre.

**Pedido:** ordenar con nulos explícitos.

```python
from django.db.models import F
ordering = [
    "zona", "npc_id",
    F("fase").asc(nulls_last=True),
    F("orden_en_fase").asc(nulls_last=True),
    "escenario_id",
]
```

Genera un `AlterModelOptions` (no toca datos ni columnas). Comprobado que la versión de
SQLite y el Django del venv lo soportan.

Opcional, del mismo viaje: mover `npc_id` después de `fase`. Hoy un mismo `npc_id` se
repite entre conversaciones distintas, así que ordenar por él antes que por la fase mezcla
conversaciones.

> Unity ya no depende de este orden —`BancoPreguntasLoader` deduce el arranque de la
> conversación por estructura, no por posición—, así que esto es defensa en profundidad.
> Pero la API sigue devolviendo un orden engañoso a cualquier otro consumidor.

### D.4 — Datos de catálogo que hay que cargar después de cada despliegue

**`api_casodetective` vacía.** En la base local estaba sin cargar, y el efecto era que
**todos** los vaciados de la cola reportaban un fallo: `POST
/casos-detective/DC_CASO_01/progreso/` devolvía 404 *"No CasoDetective matches the given
query"*. Se había corrido `cargar_banco` pero no `cargar_detective`. Ya corregido en local
(3 casos, 26 mensajes); **falta confirmar en Supabase**.

**Seis de las doce misiones no tienen ningún objetivo** en `api_objetivomision`:

| Misión | Objetivos |
|---|---|
| `MISION_EXPLORACION_01` / `_02` | 1 |
| `MISION_NPC_03` | 3 |
| `MISION_NPC_04` | 1 |
| `MISION_PANTANO_CRIATURAS` | 4 |
| `MISION_EXPLORACION_03` | **0** |
| `MISION_SEC_*` (las cinco) | **0** |

El panel las muestra como título suelto, sin contador, porque no hay nada que contar. Es
contenido que falta en `banco_preguntas`, no un bug: se arregla escribiendo los objetivos
y recargando.

### D.5 — Estado de migraciones en la base compartida

`0017_adulto_rol_admin` y `0018_miembro_un_curso_por_jugador` estaban **sin aplicar** en la
base local hasta hoy. En Supabase no se puede ver desde el código.

Ojo con la **0018**: cambia la restricción de única-por-(grupo, jugador) a **única por
jugador**. Si en la base compartida hay algún jugador en dos cursos, la migración **falla a
mitad**. Conviene comprobarlo antes:

```sql
SELECT jugador_id, COUNT(*) FROM api_miembrogrupo GROUP BY jugador_id HAVING COUNT(*) > 1;
```

En local daba cero filas (la tabla estaba vacía) y la migración pasó limpia.
