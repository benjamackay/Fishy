# Requisitos de datos — Fishy!

Estado al **2026-09-20**. Este documento nació como la lista de pedidos para Óscar
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
