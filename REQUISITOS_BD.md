# Requisitos de datos para Óscar — Fishy!

Consolida en un solo documento lo que antes vivía repartido en
`REQUISITOS_BD_MISIONES.md`, sus respuestas (`RespuestasRequisitosBD_Misiones.txt`,
Óscar, 2026-09-11) y `Backend/PENDIENTE_HDU11_RECOMPENSAS_DETECTIVE.md`. Esos tres
archivos quedan retirados; este es el único que hay que mantener al día de ahora
en adelante.

Tres partes, independientes entre sí salvo por el contrato común de la parte A:

- **A. Guardado** — el contrato que ya cumplen todos los endpoints de progreso, y
  lo único pendiente ahí (un endpoint de chat atómico).
- **B. Misiones** — catálogo, avance por objetivo, y las decisiones que ya tomamos
  el 2026-09-11.
- **C. Recompensas del Modo Detective (HDU-11)** — el contenido ya existe, falta
  exponerlo.

---

## A. Guardado — el contrato que asumen B y C

El juego no sube nada en el momento en que ocurre: acumula cambios en una cola en
memoria (`ColaDeCambios`) y los manda todos juntos en dos momentos — al cambiar de
zona y al cerrar el juego. Ver `Fishy!/documentacion/README_GUARDADO.md` para el
detalle completo; acá solo lo que le toca a la base.

### A.1 Todo lo que marca progreso es un camino de ida

Completar una misión, desbloquear una zona y (con B.3) cumplir un objetivo son los
tres casos de hoy, y los tres funcionan igual: una vez que el estado pasa a
"completado/desbloqueado/cumplido", no vuelve atrás. Cualquier endpoint nuevo de
progreso tiene que respetar esto — no por elegancia, sino porque los avisos del
juego no llegan en orden garantizado (una petición puede reintentarse y llegar
después de una más nueva), y un registro que retrocede es un dato falso en el
reporte que ve el adulto responsable.

### A.2 Todo lo que marca progreso tiene que tolerar recibir el mismo valor dos veces

La cola reintenta hasta 3 veces un cambio que falló, y en un cierre de sesión
puede quedar una petición **sin respuesta** — salió, venció el plazo, nadie sabe
si llegó al servidor — que **no se reencola** (para no arriesgarse a duplicar).
En la práctica esto significa que un endpoint como "marcar misión completada" o
"marcar objetivo cumplido" tiene que ser un no-op seguro la segunda vez que
recibe el mismo `completado: true`, no un error ni una fila duplicada. Ya es así
en `MisionProgreso`/`ZonaProgreso` (con `UniqueConstraint` por partida); el mismo
criterio aplica a todo endpoint nuevo de B.3.

### A.3 Pendiente concreto: un endpoint de conversación completa

Hoy subir una conversación de chat es una cadena obligatoriamente en serie:
`RegistrarNPC` → `IniciarChat` → N × `RegistrarMensaje` → `FinalizarChat`. Son
~9 peticiones × ~700 ms ≈ 6 s por conversación, y hay 6 lanzadores de chat en
`MainScene` (4 `PhoneChatLauncher` + 2 `ChatModuleLauncher`). Con un POST único
serían ~0,7 s, y además atómico — o entra la conversación entera o no entra nada,
que es justo lo que se quiere (hoy, si el juego se cierra a media cadena, queda
una conversación a medias en la base).

```
POST /api/partidas/{partida_id}/chats/completo/
{
  "npc":  { "nombre": "Alex", "area": "zona_2", "tipo": "enemigo", "confianza": 0 },
  "chat": { "categoria_riesgo": "desconocidos" },
  "mensajes": [
    { "tipo": "start",   "respuesta": "...", "pregunta_banco_id": "..." },
    { "tipo": "request", "respuesta": "...", "pregunta_banco_id": "...",
      "posibles_respuestas": [ ... ] },
    { "tipo": "chain",   "respuesta": "...", "calidad_respuesta": "segura",
      "opcion_banco_id": "..." }
  ],
  "finalizar": true
}
```

Crea `api_npc` + `api_chat` + los `api_mensaje` + `api_posiblerespuesta` en un
`transaction.atomic` y devuelve los ids. Puede reusar los serializers que ya
existen; el cálculo de riesgo por zona no cambia porque las filas quedan igual.
Del lado de Unity el cambio es un método (`ChatBackendLogger.Subir()`); lo que se
graba y cuándo se encola no se toca. No bloquea nada — es una mejora, no un
requisito para que el guardado funcione.

---

## B. Misiones

### B.1 De dónde sale el contenido

**Confirmado (Óscar, 2026-09-11):** `cargar_banco` lee
`Fishy!/Assets/Resources/misiones.json` para llenar la tabla `Mision` — es la
única copia del contenido, porque el banco de preguntas solo trae `mision_id` +
`nombre` de 9 misiones; el `orden`, la `zona_objetivo`, los objetivos y 3
misiones más (`MISION_NPC_03`, `MISION_NPC_04`, `MISION_PANTANO_CRIATURAS`)
existen solo en ese archivo.

- **Formato**: tomar la última versión de `dev`. Es poco probable que cambie
  mucho más a partir de ahí.
- **Estabilidad de ids**: los de `MISION_NPC_03`, `MISION_NPC_04` y
  `MISION_PANTANO_CRIATURAS` **no son estables**. Hace falta una forma fácil de
  borrar/reemplazar una misión cuando el contenido cambie — no asumir que un
  `mision_id` es para siempre al diseñar las FKs o la recarga.
- **`MISION_NPC_01`/`MISION_NPC_02`**: se guardan en la base igual, aunque hoy no
  se usen. "No hace falta arreglar lo que no está roto, aún si no se ocupa."
- **`orden`**: sigue siendo una suposición del archivo (progresión de zonas), NO
  el orden narrativo confirmado. No tratarlo como definitivo.
- **Catálogo actual**: 12 misiones (ver anexo). Dos campos opcionales nuevos,
  agregados esta semana solo del lado de Unity y sin persistir en ningún lado
  todavía — **ninguna misión los usa hoy, pero conviene que la tabla los
  contemple si el catálogo termina viviendo en la base**:
  - `desbloquea_mision` — id de la misión que se entrega sola al completar ésta.
  - `recompensa_item_id` + `recompensa_cantidad` — ítem que se entrega solo al
    completar ésta.
  Mismo criterio que `zona_objetivo`/`orden`: opcionales, vacío = no aplica. Ver
  `Fishy!/documentacion/README_CATALOGO_MISIONES.md`, sección "Conexión
  automática".

### B.2 La respuesta de `GET /misiones/`

- **Formato de la raíz**: un arreglo (`Send<List<MisionRegistro>>` en
  `ApiManager.cs`), no el objeto `{version, misiones}` del archivo.
- **⚠️ Punto a resolver, no decidido todavía**: Óscar prefiere que cada request
  traiga **una sola misión por id**, no el listado completo. Eso solo no
  alcanza: Unity necesita poder descubrir **qué misiones existen** antes de
  poder pedirlas una por una, y hoy la única fuente de esa lista de ids es el
  propio archivo local. Lo más probable es que hagan falta **los dos**: un
  `GET /misiones/` que liste (puede ser liviano — ids y poco más) y un
  `GET /misiones/{id}/` para el detalle completo. Falta confirmar con Óscar cuál
  cubre cada necesidad antes de que alguien lo implemente.
- **Sesión y partida**: para leer el catálogo, da igual el inicio de sesión y la
  partida (se puede pedir "en frío"). Para guardar progreso (B.3), sí hace falta
  la partida asociada.
- **Título**: seguir lo que diga el archivo — acepta `titulo` o `nombre`, los dos.
- **Misión sin objetivos**: mandar `objetivos: []` explícito, no omitir el campo.
- **Campos de un objetivo que no aplican a su tipo**: mandarlos vacíos (`""` / `0`),
  no omitirlos.

### B.3 Avance por objetivo (nuevo — hoy no existe)

Hoy el avance **dentro** de una misión no se guarda en ningún lado; ver el
detalle de qué se pierde y por qué en `documentacion/README_HDU-16.md` y
`README_CATALOGO_MISIONES.md`.

- **Quién lo conecta**: Óscar construye los endpoints; el equipo de Unity conecta
  `MissionTracker`/`ObjetivoMision` del lado del juego.
- **Identificador**: `(mision_id, orden)`. Se asume que el `orden` de un objetivo
  ya publicado es estable (no cambia al editar la misión).
- **Endpoint acordado**:
  ```
  GET / POST /partidas/{id}/objetivos/
  { "mision_id": "...", "orden": 1, "cumplido": true }
  ```
  Mismo contrato que A.1/A.2: camino de ida, no-op seguro ante el mismo valor
  repetido.
- **"Recoger objeto"**: NO guardar la cantidad — se recalcula del inventario, que
  ya persiste aparte.
- **"Llegar a zona"**: al restaurar la partida, dar el objetivo por cumplido
  aunque Otto ya no esté físicamente en esa zona.

### B.4 El quinto tipo de objetivo

`completar_caso_detective` (`caso_id`) no estaba en la propuesta original pero sí
en `misiones.json` y en `README_CATALOGO_MISIONES.md`. Se incluye como quinto
tipo, siguiendo lo que ya trae el archivo. El orden del enum `TipoObjetivo` del
lado de Unity es un problema de Unity, no de la base — y, en palabras de Óscar,
"idealmente no deberíamos cablear nada de objetivos en el Inspector, las
misiones se deberían recibir de la BD": el cableado a mano en la escena es el
respaldo, no el objetivo final.

### B.5 Recargar el banco

`cargar_banco` hace hoy `Mision.objects.all().delete()`
(`cargar_banco.py:135`), y eso borra en cascada `RecompensaAlbum` y con ello el
`RecompensaObtenida` de los niños (`models.py:596` y `641`). **Cambiar a un
update en vez de un delete-all.** Confirmado que no hay nada que dependa del
borrado completo; el criterio para lo que quede huérfano (progreso de una
misión que el catálogo nuevo ya no trae) es **conservarlo, no borrarlo** — los
errores de incompatibilidad que eso produzca se arreglan a mano, caso a caso.

### B.6 Migración

Se agrega como una migración nueva sobre la 0012 (`0012_personaje_zona_actual.py`
es la última hoy). Campos opcionales, así que builds viejos del juego no se
rompen. Sin migraciones en conflicto conocidas del lado de Unity/equipo de
misiones. Si algo lee `api_mision` directamente y se rompe con columnas nuevas,
es responsabilidad de quien la agregue arreglarlo.

### Anexo — las 12 misiones del catálogo hoy

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

Ninguna trae todavía `desbloquea_mision` ni `recompensa_item_id` (B.1). Sacado
de `Fishy!/Assets/Resources/misiones.json`, 2026-09-16 — puede haber cambiado
para cuando esto se implemente; no lo tomes como definitivo, lee el archivo.

---

## C. Recompensas del Modo Detective (HDU-11)

**Alcance:** exponer en la API el bloque `recompensa` que
`banco_preguntas/detective_cases.json` ya trae por caso. **No bloquea el
juego** — Unity ya funciona sin esto, con un respaldo local — pero **si se
implementa, Unity ya está listo para usarlo sin ningún cambio adicional**.

### C.1 Contexto

HDU-11 se implementó esta sesión **solo en Unity**: al resolver un caso
Detective con acierto ≥ umbral, el juego agrega un ítem "pin" al inventario
(`InventoryManager`), sin duplicar si ya lo tiene, y muestra un popup temporal.
También adelantó una versión mínima de HDU-12 (álbum de evidencias): un ítem
"Álbum de Evidencias" que aparece junto con el primer pin y, al tocarlo, lista
los pines obtenidos.

Unity resuelve qué pin corresponde a cada caso con un **fallback en dos
pasos**: primero mira si el caso que acaba de cargar ya trae su propia
recompensa (eso vendría del backend, si se implementa lo de abajo); si no trae
nada ahí — la situación de hoy — cae a un catálogo local
(`Fishy!/Assets/Scripts/Detective/CatalogoRecompensasDetective.cs`) que lee
`Fishy!/Assets/Resources/detective_recompensas.json`, una copia en formato
Unity del bloque `recompensa` que `detective_cases.json` ya trae por caso:

```json
"DC_CASO_01": {
  "recompensa": {
    "item_id": "PIN_VIGIA_SILENCIOSO",
    "nombre": "Pin del Vigía Silencioso",
    "accesorio_hdu06": "Gorro de detective con visera",
    "umbral_aciertos": 0.5,
    "no_duplica_al_repetir": true
  }
}
```

**El backend real (`cargar_detective.py`, modelo `CasoDetective`, su
serializer) sigue sin leer ni exponer `recompensa` hoy.**

### C.2 Qué falta (si se decide hacerlo)

**1. Modelo — `backend/api/models.py`, clase `CasoDetective`**

Agregar 5 campos planos, mismo criterio que `permiso_player_text` /
`permiso_npc_nombre` / `permiso_npc_response` que ya tiene el modelo (un solo
grupo de datos por caso, no una tabla aparte):

```python
recompensa_item_id               = models.CharField(max_length=60, blank=True, default="")
recompensa_nombre                = models.CharField(max_length=150, blank=True, default="")
recompensa_accesorio_hdu06       = models.CharField(max_length=150, blank=True, default="")
recompensa_umbral_aciertos       = models.FloatField(default=0.5)
recompensa_no_duplica_al_repetir = models.BooleanField(default=True)
```

Independiente de `RecompensaAlbum` (la tabla que ya existe para las
recompensas de Misión) — no hace falta tocar esa tabla ni su
`CheckConstraint`. El pin de HDU-11 va directo al inventario del jugador, que
ya es genérico por `item_id` de texto.

**2. Migración** — puede ir junto con la de B.6, o aparte.

**3. `cargar_detective.py`** — leer el bloque `recompensa` igual que ya se lee
`permiso`, y agregarlo a `defaults`:

```python
permiso = c.get("permiso") or {}
recompensa = c.get("recompensa") or {}
defaults = {
    "titulo":               c.get("titulo", ""),
    "zona":                 c.get("zona", ""),
    "etiquetas_ml":         c.get("etiquetas_ml", []),
    "permiso_player_text":  permiso.get("player_text", ""),
    "permiso_npc_nombre":   permiso.get("npc_nombre", ""),
    "permiso_npc_response": permiso.get("npc_response", ""),
    "recompensa_item_id":               recompensa.get("item_id", ""),
    "recompensa_nombre":                recompensa.get("nombre", ""),
    "recompensa_accesorio_hdu06":       recompensa.get("accesorio_hdu06", ""),
    "recompensa_umbral_aciertos":       recompensa.get("umbral_aciertos", 0.5),
    "recompensa_no_duplica_al_repetir": recompensa.get("no_duplica_al_repetir", True),
}
```

**4. `CasoDetectiveSerializer`** — agregar los 5 campos a `fields`:

```python
fields = [
    "id", "caso_id", "titulo", "zona", "etiquetas_ml",
    "permiso_player_text", "permiso_npc_nombre", "permiso_npc_response",
    "recompensa_item_id", "recompensa_nombre", "recompensa_accesorio_hdu06",
    "recompensa_umbral_aciertos", "recompensa_no_duplica_al_repetir",
    "mensajes",
]
```

**5. Recargar y verificar**: `python manage.py cargar_detective --limpiar` y
confirmar que el endpoint de casos Detective trae los 5 campos `recompensa_*`
no vacíos.

### C.3 Cómo lo va a usar Unity en cuanto exista

`DetectiveCaseManager.OtorgarRecompensaSiCorresponde` prueba primero si el
caso recién cargado ya trae su propia recompensa
(`DetectiveCase.TieneRecompensa`); si sí, usa esos 5 valores del backend sin
tocar el catálogo local. Solo si vienen vacíos cae al catálogo local. Es el
mismo patrón try-backend-then-local que ya usa el resto del juego (banco de
preguntas, casos Detective, catálogo de misiones). **Implementarlo no requiere
ningún cambio en Unity**: en cuanto el serializer mande los 5 campos, dejan de
estar vacíos y pasan a ser la fuente de verdad automáticamente.

### C.4 Sobre el álbum mínimo (adelanto de HDU-12)

No requiere ningún cambio de backend: es un ítem más de inventario
(`ITEM_ALBUM_EVIDENCIAS`), y la mochila completa ya sube al guardar (ver A). La
HDU-12 completa (agrupar evidencias por caso, registrar cada señal de riesgo
identificada) es una historia aparte, no cubierta acá.

### C.5 Fuera de alcance (a propósito)

- No se toca `RecompensaAlbum` ni se crea álbum server-side: HDU-12 completa
  queda para más adelante.
- No hay endpoint para "reclamar" el pin: Unity lo agrega directamente al
  inventario del jugador por el mecanismo normal de guardado de mochila (A). El
  backend no necesita "saber" que un ítem vino de una recompensa Detective — es
  solo otro ítem en la mochila.
