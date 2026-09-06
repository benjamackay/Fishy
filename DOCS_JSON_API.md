# Documentación JSON — Comunicación con la Base de Datos

> ⚠️ **Actualizado a la Fase 2 (control parental, 2026-08-12).** El contrato de
> la API **cambió**: quien inicia sesión es un **adulto responsable**, que
> gestiona uno o más **perfiles de menores**, y la partida cuelga del perfil, no
> de la cuenta.
>
> `ApiManager.cs` **todavía implementa el contrato viejo** — eso es justamente la
> Fase 3. Este documento describe lo que el backend espera **hoy**; si algo aquí
> no calza con el código de Unity, el que está desactualizado es el código.

## Índice
1. [Arquitectura general](#arquitectura-general)
2. [Autenticación](#autenticación)
3. [Perfiles de menores](#perfiles-de-menores)
4. [Modelos de datos (DTOs)](#modelos-de-datos-dtos)
5. [Endpoints y payloads](#endpoints-y-payloads)
6. [Banco de preguntas](#banco-de-preguntas)
7. [Modo Detective (HDU-10)](#modo-detective-hdu-10)
8. [Modo local (fallback)](#modo-local-fallback)

---

## Flujo general

```
registro / login  ──▶  GET /jugadores/  ──▶  elegir perfil  ──▶  GET /jugadores/{id}/partidas/
   (adulto)              (o POST si                                      │
                          es la 1ª vez)                                  ├── hay → retomar
                                                                         └── no  → POST /partidas/
                                                                                   con usuario_jugador_id
```

De ahí en adelante (NPCs, chats, mensajes, banco) todo funciona igual que antes,
usando el `id` de la partida.

**Cada menor tiene su propio avance.** La partida cuelga del perfil, así que los
hermanos no comparten progreso y al elegir un perfil se retoma exactamente donde
ese niño lo dejó.

---

## Arquitectura general

| Componente | Detalle |
|---|---|
| URL base | `http://127.0.0.1:8000/api` (configurable en `ApiManager.cs`) |
| Serialización HTTP | Newtonsoft.Json (`JsonConvert`) |
| Deserialización local | `JsonUtility` (para `banco_preguntas.json`) |
| Autenticación | Token (Django REST Framework Token Auth) |
| Timeout | 4 segundos — si no hay respuesta, activa modo local |

El cliente Unity se comunica con el backend Django a través de `ApiManager.cs`. Todos los requests autenticados incluyen el header:
```
Authorization: Token <token>
```

---

## Autenticación

La cuenta que hace login es la del **adulto responsable**. Los perfiles de los
menores no tienen credenciales propias.

### POST `/auth/registro/` — Registro del adulto responsable
**Request:**
```json
{
  "nombre": "string",
  "email": "adulto@ejemplo.cl",
  "password": "string",
  "apellido": "string",
  "edad": 38,
  "fecha_nacimiento": "1988-04-12"
}
```
| Campo | Obligatorio | Notas |
|---|---|---|
| `nombre` | sí | único; es el que se usa para el login |
| `email` | sí | único |
| `password` | sí | mínimo 4 caracteres |
| `apellido`, `edad`, `fecha_nacimiento` | no | opcionales |

**Response (`201`):**
```json
{
  "token": "string",
  "adulto_id": 1
}
```

### POST `/auth/login/` — Login
**Request:**
```json
{
  "nombre": "string",
  "password": "string"
}
```
**Response (`200`):**
```json
{
  "token": "string",
  "adulto_id": 1
}
```

> ⚠️ El campo se llama **`adulto_id`**, ya no `usuario_id`. Ojo: Newtonsoft
> **no lanza error** si el DTO busca un campo que no llega — deja el `int` en `0`
> y sigue. O sea que este cambio falla en silencio.

### GET `/auth/perfil/` — Datos de la cuenta autenticada
**Response:**
```json
{
  "id": 1,
  "nombre": "papa_demo",
  "apellido": "Pérez",
  "email": "adulto@ejemplo.cl",
  "edad": 38,
  "fecha_nacimiento": "1988-04-12",
  "fecha_creacion": "2026-08-12T10:00:00Z"
}
```

El token recibido se almacena en `ApiManager.Token` y se adjunta a todos los requests posteriores.

---

## Perfiles de menores

Cada perfil pertenece a **un** adulto. Un adulto puede tener varios perfiles, y
el nombre no se puede repetir dentro de la misma cuenta.

### GET `/jugadores/` — Perfiles del adulto autenticado
**Response:** lista de `UsuarioJugadorDto` (vacía si todavía no creó ninguno)
```json
[
  { "id": 1, "adulto": 1, "nombre": "Benja", "edad": 9,  "fecha_creacion": "2026-08-12T10:01:00Z" },
  { "id": 2, "adulto": 1, "nombre": "Sofi",  "edad": 11, "fecha_creacion": "2026-08-12T10:02:00Z" }
]
```

### POST `/jugadores/` — Crear un perfil
**Request:**
```json
{ "nombre": "Benja", "edad": 9 }
```
**Response (`201`):** `UsuarioJugadorDto`

> `adulto` **no se manda**: lo asigna el backend con el usuario del token. Si
> intentas mandarlo, se ignora.

### GET `/jugadores/{jugador_id}/partidas/` — Partidas del perfil
**Response:** lista de `PartidaDto`, de la jugada **más reciente a la más antigua**.

Es lo que permite **retomar el avance**: cada menor conserva su propia partida
entre sesiones, independiente de la de sus hermanos.

```
elegir perfil ──▶ GET /jugadores/{id}/partidas/
                        │
                        ├── viene algo  ──▶ continuar con la primera
                        └── viene vacía ──▶ POST /partidas/ (primera vez)
```

### GET / PATCH / DELETE `/jugadores/{jugador_id}/`
```json
// PATCH — campos opcionales
{ "nombre": "Benjamín", "edad": 10 }
```
- `GET` → `UsuarioJugadorDto`
- `PATCH` → `UsuarioJugadorDto` actualizado
- `DELETE` → `204` sin cuerpo. **Arrastra en cascada** las partidas del perfil y
  todo lo que cuelga de ellas (NPCs, chats, mensajes).

**Errores:**

| Código | Causa |
|---|---|
| `400` | ya existe otro perfil con ese nombre en la misma cuenta |
| `404` | el perfil no existe **o es de otro adulto** (no se distingue, a propósito) |

---

## Modelos de datos (DTOs)

Definidos al final de `Fishy!/Assets/Scripts/ApiManager.cs`.

> ⚠️ Ese archivo todavía tiene los DTOs viejos (`usuario_id`, `usuario`).
> Actualizarlos es parte de la Fase 3. Ojo también con que hay una **copia
> huérfana** del ApiManager en `Assets/Scripts/` (raíz del repo) que Unity no
> compila: el bueno es el de `Fishy!/Assets/Scripts/`.

### AuthResponse
```json
{
  "token": "string",
  "adulto_id": 1
}
```

### UsuarioJugadorDto — Perfil de menor
```json
{
  "id": 1,
  "adulto": 1,
  "nombre": "Benja",
  "edad": 9,
  "fecha_creacion": "2026-08-12T10:01:00Z"
}
```
| Campo | Tipo | Descripción |
|---|---|---|
| `adulto` | int | id del adulto dueño; **solo lectura**, lo asigna el backend |
| `edad` | int? | opcional |

### PartidaDto — Sesión de juego
```json
{
  "id": 1,
  "usuario_jugador": 1,
  "progreso": 45.5,
  "nivel_riesgo": 2,
  "fecha_inicio": "2025-01-01T12:00:00Z",
  "fecha_update": "2025-01-01T12:30:00Z"
}
```
| Campo | Tipo | Descripción |
|---|---|---|
| `usuario_jugador` | int | id del perfil de menor dueño de la partida (antes era `usuario`) |
| `progreso` | float | Porcentaje de avance (0–100) |
| `nivel_riesgo` | int? | ID de nivel de riesgo (opcional) |

### NpcDto — Personaje no jugador
```json
{
  "id": 1,
  "partida": 1,
  "nombre": "Alex",
  "area": "desconocidos",
  "tipo": "enemigo",
  "confianza": 60
}
```
| Campo | Tipo | Valores posibles |
|---|---|---|
| `tipo` | string | `"aliado"`, `"neutral"`, `"enemigo"` |
| `confianza` | int | 0–100 |

### ChatDto — Sesión de chat
```json
{
  "id": 1,
  "partida": 1,
  "npc": 1,
  "categoria_riesgo": "grooming",
  "fecha_inicio": "2025-01-01T12:00:00Z",
  "fecha_termino": "2025-01-01T12:15:00Z"
}
```
| Campo | Tipo | Valores posibles |
|---|---|---|
| `categoria_riesgo` | string | `"grooming"`, `"ciberacoso"`, `"reto_viral"` |
| `fecha_termino` | string | Se asigna al crear el mensaje de tipo `"end"` |

### MensajeDto — Mensaje de chat
```json
{
  "id": 1,
  "chat": 1,
  "tipo": "request",
  "respuesta": "No le voy a dar mi dirección a un desconocido.",
  "calidad_respuesta": "buena",
  "pregunta_banco_id": "HDU2_NPC01_F2_Q01",
  "opcion_banco_id": "HDU2_NPC01_F2_Q01_R2",
  "timestamp": "2025-01-01T12:05:00Z",
  "posibles_respuestas": [
    {
      "id": 1,
      "texto": "Dale, te mando la dirección.",
      "orden": 1,
      "calidad_respuesta": "mala"
    },
    {
      "id": 2,
      "texto": "No creo, no te conozco.",
      "orden": 2,
      "calidad_respuesta": "buena"
    }
  ]
}
```
| Campo | Tipo | Valores posibles |
|---|---|---|
| `tipo` | string | `"start"`, `"chain"`, `"request"`, `"end"` |
| `calidad_respuesta` | string | `"buena"`, `"neutral"`, `"mala"` |
| `pregunta_banco_id` | string | ID del banco de preguntas (e.g., `"HDU2_NPC01_F1_Q01"`) |
| `opcion_banco_id` | string | ID de la opción elegida (e.g., `"HDU2_NPC01_F1_Q01_R2"`) |

> **`opcion_banco_id` es lo que hace que la respuesta cuente para el riesgo por
> zona.** Identifica la opción exacta del banco, con su puntaje real (`-1` / `+1` /
> `+2`). No basta con `calidad_respuesta`: el cliente colapsa `segura_basica` y
> `segura_optima` en `"buena"`, así que deducir el puntaje de ahí trataría toda
> respuesta segura como óptima. Si se omite, el mensaje se guarda igual pero no
> suma. Ver [riesgo por zona](#get-partidaspartida_idriesgo-por-zona--riesgo-acumulado-por-zona).

**Tipos de mensaje:**
- `start` — Primer mensaje de una conversación
- `chain` — Mensaje de continuación (sin opciones de respuesta)
- `request` — Pregunta al jugador (incluye `posibles_respuestas`)
- `end` — Cierre de conversación

### PosibleRespuestaDto — Opción de respuesta
```json
{
  "id": 1,
  "texto": "No le voy a dar mi dirección.",
  "orden": 1,
  "calidad_respuesta": "buena"
}
```

### CasoDetectiveDto — Caso del modo Detective
```json
{
  "id": 1,
  "caso_id": "caso_01",
  "titulo": "Caso de Alex y Sam",
  "zona": "playa",
  "etiquetas_ml": ["grooming"],
  "permiso_player_text": "Oye, ¿me puedes ayudar? Recibí mensajes raros...",
  "permiso_npc_nombre": "Otto",
  "permiso_npc_response": "Claro, muéstramelos.",
  "mensajes": [
    {
      "id": 1,
      "mensaje_id": "m04",
      "npc_sender": "Alex",
      "texto": "No le cuentes esto a tus papás, es un secreto entre nosotros.",
      "es_senal_riesgo": true,
      "es_ambiguo": false,
      "explicacion": "Pedir guardar secretos a los papás es una señal de alerta clásica.",
      "nota_ambiguo": null,
      "orden": 3
    }
  ]
}
```
| Campo | Tipo | Descripción |
|---|---|---|
| `mensajes` | lista de `MensajeDetectiveDto` | Ya vienen ordenados por `orden` |
| `es_ambiguo` | bool | No cuenta ni como acierto ni como error al calificar (CA5 de HDU-10) |
| `explicacion` | string? | Solo tiene valor en los mensajes de riesgo; se usa para el resumen al final |

### ProgresoDetectiveDto — Resultado de un intento
```json
{
  "id": 1,
  "partida": 1,
  "caso": 1,
  "mensajes_marcados": ["m04", "m06"],
  "aciertos": 2,
  "total_riesgo": 3,
  "porcentaje": 0.67,
  "intentos": 1,
  "fecha_inicio": "2026-09-01T12:00:00Z",
  "fecha_termino": "2026-09-01T12:03:00Z"
}
```
| Campo | Tipo | Descripción |
|---|---|---|
| `partida` / `caso` | int | **solo lectura**, los asigna el backend (mismo patrón que `UsuarioJugador.adulto`) |
| `mensajes_marcados` | lista de string | `mensaje_id` que el jugador marcó como riesgo |
| `intentos` | int | Se incrementa solo — reintentar el mismo caso no crea una fila nueva |

---

## Endpoints y payloads

### Partidas

**POST `/partidas/`** — Crear partida
```json
// Request
{ "usuario_jugador_id": 1, "progreso": 0.0, "nivel_riesgo": null }

// Response: PartidaDto
```
`usuario_jugador_id` es **obligatorio** y el perfil tiene que ser de la cuenta
autenticada. Si falta, no existe, o es de otro adulto → `404`.

**PATCH `/partidas/{partida_id}/`** — Actualizar partida
```json
// Request (campos opcionales)
{ "progreso": 50.0, "nivel_riesgo": 2 }

// Response: PartidaDto
```

**GET `/partidas/{partida_id}/riesgo-por-zona/`** — Riesgo acumulado por zona
```json
// Response
{
  "partida_id": 1,
  "zonas": [
    {
      "zona": "chat_simulado",
      "riesgo_acumulado": 3,
      "respuestas": 3,
      "minimo_posible": -3,
      "maximo_posible": 6
    },
    {
      "zona": "desconocidos",
      "riesgo_acumulado": -2,
      "respuestas": 4,
      "minimo_posible": -4,
      "maximo_posible": 8
    }
  ],
  "total": 1,
  "respuestas": 7,
  "sin_clasificar": 0
}
```
Suma el `impacto_puntuacion` de cada opción que el menor eligió, agrupado por la
`zona` de la pregunta a la que pertenece. El puntaje sale del banco:
`insegura = -1`, `segura_basica = +1`, `segura_optima = +2`.

**El signo no está invertido: más alto = más seguro.** Un total negativo significa
que el menor eligió mayoritariamente respuestas inseguras.

| Campo | Significado |
|---|---|
| `riesgo_acumulado` | Suma de los impactos de esa zona |
| `respuestas` | Cuántas respuestas se contaron |
| `minimo_posible` | Puntaje si hubiera elegido siempre la peor opción |
| `maximo_posible` | Puntaje si hubiera elegido siempre la mejor |
| `sin_clasificar` | Respuestas cuyo `opcion_banco_id` no existe en el banco (no suman) |

`minimo_posible` y `maximo_posible` son las cotas de **esas mismas preguntas**, no
del banco completo, así que sirven para mostrar el resultado como una escala en vez
de un número suelto.

Solo cuentan los mensajes que traen `opcion_banco_id`. Los flujos que no reportan
la opción elegida —como el módulo de diálogo antiguo de Desconocidos, con nodos
escritos a mano (`a0`, `a1`, …) que no existen en el banco— quedan fuera del
cálculo a propósito, en vez de contribuir con datos inventados.

**GET `/partidas/{partida_id}/oportunidades-mejora/`** — Decisiones inseguras

Filtro opcional: `?zona=ciberacoso`

```json
// Response
{
  "partida_id": 42,
  "jugador": "Perfil 2",
  "oportunidades": [
    {
      "fecha": "2026-09-03T16:31:19.462636Z",
      "zona": "ciberacoso",
      "categoria": "ciberacoso",
      "npc": "Flamenco",
      "chat_id": 84,
      "pregunta_banco_id": "HDU3_NPC03_Q01",
      "mensaje_npc": "oye Otto, te sacamos del grupo del chat del pantano…",
      "eligio": {
        "opcion_banco_id": "HDU3_NPC03_Q01_R3",
        "texto": "ja igual el grupo de ustedes era una porquería, quédense solos",
        "impacto_puntuacion": -1,
        "consecuencia": "Respondes con enojo. Flamenco aprovecha tu molestia…"
      },
      "mejor_opcion": {
        "opcion_banco_id": "HDU3_NPC03_Q01_R1",
        "texto": "Reportar y bloquear este mensaje",
        "impacto_puntuacion": 2,
        "consecuencia": "Lo reportaste de inmediato. Flamenco se desconecta…"
      },
      "puntos_perdidos": 3
    }
  ],
  "total": 1,
  "por_zona": [{ "zona": "ciberacoso", "oportunidades": 1 }]
}
```

Es el *"registro como oportunidad de mejora"* que piden los criterios de
aceptación de las zonas de riesgo (p. ej. HDU-3 CA3: el menor responde a un
mensaje de ciberacoso con otra burla).

**No existe un campo que las marque.** Una oportunidad de mejora **es** un
`Mensaje` cuyo `opcion_banco_id` resuelve a una `OpcionBanco` de tipo `insegura`.
Se deriva en vez de guardarse aparte para que no pueda quedar desincronizada del
banco: si mañana una opción deja de ser insegura, la lista lo refleja sola.

| Campo | Significado |
|---|---|
| `eligio` | La opción insegura que marcó, con su consecuencia narrativa |
| `mejor_opcion` | La de mayor `impacto_puntuacion` de esa misma pregunta |
| `puntos_perdidos` | `mejor_opcion` − `eligio`. Sirve para ordenar por gravedad |

Solo cuenta `insegura`. Una `segura_basica` no es un error —es correcta pero
mejorable—, y mezclarlas le quitaría sentido a la lista.

> ⚠️ **Es para el reporte del adulto responsable, no para mostrárselo al menor.**
> Etiquetarle la pantalla con sus errores lo señala y rompe el tono del juego,
> que corrige por consecuencia narrativa (el NPC reacciona, Otto cambia de ánimo).

**GET / POST `/partidas/{partida_id}/misiones/`** — Progreso de misiones (HDU-1 CA4 y CA5)

```json
// POST Request
{ "mision_id": "MISION_NPC_01", "estado": "completada" }   // estado: "disponible" | "completada"

// Response
{
  "id": 7,
  "mision_id": "MISION_NPC_01",
  "estado": "completada",
  "nombre": "",
  "zona": "",
  "en_catalogo": false,
  "fecha_desbloqueo": "2026-09-04T14:02:11.031Z",
  "fecha_completada": "2026-09-04T14:09:47.882Z"
}
```

El `GET` devuelve la lista de las misiones que esa partida tiene desbloqueadas.
Es lo que `MissionManager` necesita al cargar la partida y hoy solo tiene en
PlayerPrefs.

| Campo | Significado |
|---|---|
| `mision_id` | El `desafioId` del `DesafioData` de Unity, o el `mision_id` del banco |
| `estado` | `disponible` o `completada`. **No es una columna**: se deriva de `fecha_completada` |
| `nombre` / `zona` | Vienen del catálogo `Mision`. Vacíos si el id no está ahí |
| `en_catalogo` | `false` avisa que el id no existe en el banco. El progreso se guarda igual |

**Es idempotente**, igual que `MissionManager.CompletarDesafio`: repetir el POST no
duplica la fila ni mueve `fecha_completada`. **Completar es un camino de ida:** un
POST con `disponible` sobre una misión ya completada se ignora, porque el orden en
que llegan los mensajes desde el juego no está garantizado y el registro para el
adulto no puede retroceder.

> ⚠️ Hoy `en_catalogo` llega en `false` para `MISION_NPC_01` y `MISION_NPC_02`: los
> `DesafioData` de Unity usan esos ids y el banco define otros
> (`MISION_EXPLORACION_01`, `MISION_SEC_*`). Se guarda igual y queda el aviso en el
> log del servidor y la columna del admin, en vez de responder 404 y perder el dato.

**GET / POST `/partidas/{partida_id}/zonas/`** — Progreso de zonas (HDU-3 CA5, HDU-4 CA5)

```json
// POST Request
{ "zona": "ciberacoso", "completada": true }

// Response
{
  "id": 3,
  "zona": "ciberacoso",
  "desbloqueada": true,
  "completada": true,
  "fecha_desbloqueo": "2026-09-04T14:02:11.031Z",
  "fecha_completada": "2026-09-04T14:31:02.774Z"
}
```

Es el *"marca la temática como completada y habilita el acceso a la siguiente"* de
los CA de las zonas de riesgo.

**La zona inicial (`desconocidos`) ya viene en la lista**: `POST /partidas/` la deja
escrita al crear la partida, porque Otto empieza ahí y esa zona nunca está
oscurecida. Sin eso, una partida recién creada devolvía una lista vacía, que se lee
como "el mapa está todo cerrado".

**Que la fila exista significa que la zona está desbloqueada**, así que el POST con
`completada: false` es lo que se manda al abrir una zona nueva. El `GET` devuelve
solo las zonas abiertas de esa partida: lo que no está en la lista sigue oscurecido
en el mapa. Completar también es un camino de ida.

`zona` es el slug del banco (`desconocidos`, `ciberacoso`, `reto_viral`) y **no se
valida contra una lista fija a propósito**: agregar una temática es contenido, no
una migración.

> El progreso cuelga de la **partida**, no del perfil del menor. Un mismo perfil
> puede tener varias partidas (HDU-15, "continuar mi última partida"): si viviera en
> `UsuarioJugador`, la segunda partida empezaría con todo completado y resetearla
> borraría el registro de la primera.

---

### Inventario

**GET / PUT `/partidas/{partida_id}/inventario/`** — La mochila de Otto (HDU-15)

```json
// PUT Request — la mochila COMPLETA, no un objeto suelto
{
  "items": [
    { "item_id": "ITEM_BRUJULA", "cantidad": 1 },
    { "item_id": "ITEM_FLOR_01", "cantidad": 3 }
  ]
}

// Response (igual en GET y en PUT): el inventario tal como quedó
[
  {
    "id": 12,
    "item_id": "ITEM_BRUJULA",
    "cantidad": 1,
    "fecha_agregado": "2026-09-06T16:52:03.114Z",
    "fecha_actualizacion": "2026-09-06T16:52:03.114Z"
  }
]
```

**El PUT reemplaza la mochila entera: lo que no viene, no está.** Es la diferencia
con misiones y zonas, y es deliberada. Una misión solo crece —se desbloquea y se
completa, nunca se "descompleta"—, así que ahí el POST por fila es natural. El
inventario encoge: `ItemType.Consumable` significa que un objeto usado sale de la
mochila, y con POST por fila no hay forma de decir *"ya no tengo esto"* sin inventar
un DELETE por objeto. Bastaría con que una de esas llamadas se perdiera para que el
niño/a viera un objeto fantasma al retomar.

Mandar la lista completa hace el endpoint **idempotente por construcción** y corre
dentro de una transacción, así que no existe un instante con la mochila a medias.
Una lista vacía vacía el inventario. Una `cantidad` de 0 o menos se trata como "no
lo tengo" y no se guarda.

Un PUT con cualquier elemento inválido se rechaza **entero** con `400` y no escribe
nada: una mochila a medio escribir sería un estado que el niño/a nunca tuvo.

`item_id` es el `itemId` del `ItemData` de Unity (`Assets/Items/*.asset`), en
MAYÚSCULAS: `ITEM_BRUJULA`, `ITEM_FLOR_01`. **No se valida contra ningún catálogo
porque no hay catálogo de items en el backend, y no debería haberlo**: los objetos
se crean en Unity como ScriptableObjects y no vienen del banco de preguntas, así que
una tabla `Item` en Postgres sería una segunda copia mantenida a mano y lista para
desalinearse. El nombre visible, el ícono y la descripción se quedan en Unity, que
es donde se dibujan.

> Igual que misiones y zonas, cuelga de la **partida**. Y por la misma tercera razón
> de `MisionProgreso`: el progreso es del niño/a y el catálogo es contenido. Si
> mañana borran `flor2.asset`, la fila que dice que la recogió no tiene por qué
> desaparecer.

---

### Personaje

**GET / PATCH `/partidas/{partida_id}/personaje/`** — Dónde quedó Otto (HDU-15)

```json
// PATCH Request — todos los campos son opcionales
{ "escena": "SampleScene", "pos_x": 12.5, "pos_y": -3.25 }

// Response (igual en GET y en PATCH)
{
  "escena": "SampleScene",
  "pos_x": 12.5,
  "pos_y": -3.25,
  "tiene_posicion": true,
  "fecha_actualizacion": "2026-09-06T18:04:11.220Z"
}
```

Es **PATCH y no PUT**, al revés que el inventario, y por una razón concreta: aquí no
hay nada que borrar. Son tres columnas de una fila que siempre existe, así que mandar
la posición sin la escena es una actualización legítima y no una orden de dejar el
resto en blanco.

**La fila se crea sola.** `PersonajeJugador` es uno a uno con la partida, pero ninguna
vista lo creaba: las partidas que ya existen no tienen fila. El `GET` la crea en vez
de responder 404 — sale más barato que una migración de datos que recorra todas las
partidas para dejarles una fila vacía.

**`tiene_posicion` no es una columna**, la deriva el modelo. Va explícito para que el
cliente no tenga que decidir qué significa un `null`: **el `(0,0)` es un lugar del
mapa**, no la ausencia de posición. Si se confundieran, a un niño/a que guardó ahí lo
mandaría de vuelta al `spawnPoint`.

`escena` se guarda para no restaurar coordenadas de otra escena el día que haya más de
una — dejarían a Otto dentro de un cerro. Unity compara contra la escena activa y, si
no calzan, ignora la posición y avisa por consola.

> No hay `zona_actual`, y es a propósito. Sería útil para el reporte al tutor, pero hoy
> Unity no tiene el concepto de "zona en la que está Otto" —las `BlockedZone` saben
> abrirse, no saben contener—, así que el campo nacería vacío y alguien lo leería
> creyendo que significa algo.

---

### NPCs

**POST `/partidas/{partida_id}/npcs/`** — Registrar NPC
```json
// Request
{
  "nombre": "Valen",
  "area": "chat_simulado",
  "tipo": "neutral",
  "confianza": 50
}

// Response: NpcDto
```

**PATCH `/npcs/{npc_id}/`** — Actualizar confianza
```json
// Request
{ "confianza": 75 }

// Response: NpcDto
```

---

### Chat

**POST `/chats/`** — Iniciar sesión de chat
```json
// Request
{
  "partida_id": 1,
  "npc_id": 1,
  "categoria_riesgo": "grooming"
}

// Response: ChatDto
```

**POST `/chats/{chat_id}/mensajes/registrar/`** — Registrar mensaje

*Tipo `start`:*
```json
{
  "tipo": "start",
  "respuesta": "Hola, ¿cómo estás?",
  "pregunta_banco_id": "HDU2_NPC01_F1_Q01"
}
```

*Tipo `request` (con opciones para el jugador):*
```json
{
  "tipo": "request",
  "respuesta": "¿Me das tu dirección?",
  "pregunta_banco_id": "HDU2_NPC01_F2_Q01",
  "posibles_respuestas": [
    { "texto": "Claro, aquí va.", "orden": 1, "calidad_respuesta": "mala" },
    { "texto": "No, no te conozco.", "orden": 2, "calidad_respuesta": "buena" }
  ]
}
```

*Tipo `chain` (respuesta del jugador):*
```json
{
  "tipo": "chain",
  "respuesta": "No, no te conozco.",
  "calidad_respuesta": "buena",
  "pregunta_banco_id": "HDU2_NPC01_F2_Q01",
  "opcion_banco_id": "HDU2_NPC01_F2_Q01_R2"
}
```

**Response de todos los tipos:** `MensajeDto`

> En los mensajes `chain` conviene mandar siempre `opcion_banco_id`: es lo único
> que permite acumular riesgo por zona. Sin él el mensaje se guarda, pero no puntúa.

**GET `/chats/{chat_id}/mensajes/`** — Historial de mensajes
```json
// Response: lista de MensajeDto
[{ ... }, { ... }]
```

**POST `/chats/{chat_id}/finalizar/`** — Cerrar chat
```json
// Request (opcional)
{ "respuesta": "Fin de conversación." }

// Response: MensajeDto (tipo "end")
```

---

### Banco de preguntas

**GET `/banco/preguntas/`** — Consultar preguntas
```
Query params:
  ?zona=desconocidos
  ?npc_id=NPC_01
  ?fase=2
  ?escenario_id=CHAT_GROOMING_01
  ?hdu=HDU-2
  ?solo_riesgo=true
```
**Response:** lista de `PreguntaBancoSerializer`

**GET `/banco/preguntas/{pregunta_id}/`** — Obtener pregunta específica
**Response:** `PreguntaBancoSerializer`

**GET `/banco/zonas/`** — Catálogo de zonas del banco

Se arma consultando la BD, no desde una lista fija: al cargar un banco con una
zona nueva, aparece aquí sola y sin tocar código.

**Response:**
```json
[
  { "zona": "chat_simulado", "preguntas": 6,  "hdu": "HDU-8" },
  { "zona": "ciberacoso",    "preguntas": 4,  "hdu": "HDU-3" },
  { "zona": "desconocidos",  "preguntas": 18, "hdu": "HDU-2" }
]
```

**GET `/banco/zonas/{zona}/preguntas/`** — Preguntas de una zona

Acepta los mismos query params que `/banco/preguntas/`, salvo `?zona=`, que lo
manda la ruta.

**Response:** lista de `PreguntaBancoSerializer`

Responde **404** si la zona no existe en el banco. Eso permite distinguirla de
una zona real que todavía no tiene preguntas cargadas, cosa que
`/banco/preguntas/?zona=` no puede hacer porque devuelve `[]` en ambos casos.
```json
{ "detail": "La zona 'inventada' no existe." }
```

---

### Modo Detective

**GET `/casos-detective/`** — Listar casos
```
Query params:
  ?zona=playa
```
**Response:** lista de `CasoDetectiveDto` (con sus `mensajes` anidados)

**GET `/casos-detective/{caso_id}/`** — Obtener caso específico
**Response:** `CasoDetectiveDto`

Responde **404** si `caso_id` no existe (mismo criterio que
`/banco/preguntas/{pregunta_id}/`).

**POST `/casos-detective/{caso_id}/progreso/`** — Registrar resultado de un intento
```json
// Request
{
  "partida_id": 1,
  "mensajes_marcados": ["m04", "m06"],
  "aciertos": 2,
  "total_riesgo": 3,
  "porcentaje": 0.67
}

// Response: ProgresoDetectiveDto
```
`partida_id` debe pertenecer a una partida del adulto autenticado (mismo
aislamiento que el resto de los endpoints de partida). Reintentar el mismo caso
**no** crea una fila nueva: actualiza el resultado y suma 1 a `intentos`. La
primera vez responde `201`, las siguientes `200`.

**GET `/partidas/{partida_id}/casos-detective/`** — Progreso de una partida
```json
// Response: lista de ProgresoDetectiveDto
[{ ... }, { ... }]
```
Sirve para que el cliente sepa qué casos ya están completados sin tener que
llevar la cuenta localmente (mismo rol que cumple `PlayerPrefs` en
`MissionManager` cuando no hay backend).

---

## Banco de preguntas

Archivo local: `Fishy!/Assets/Resources/banco_preguntas.json`  
Cargado por: `BancoPreguntasLoader.cs` usando `JsonUtility`  
Modelos en: `BancoPreguntasData.cs`

### Estructura raíz
```json
{
  "version": "1.7",
  "preguntas": [ ... ]
}
```

### PreguntaBanco — Campos comunes
```json
{
  "id": "HDU2_NPC01_F1_Q01",
  "hdu": "HDU-2",
  "zona": "desconocidos",
  "categoria": "grooming",
  "nivel_riesgo": 2,
  "es_mensaje_riesgo": true,
  "es_fin_de_npc": false,
  "es_fin_de_zona": false,
  "mensaje_npc": "Oye, ¿cuántos años tienes?",
  "etiquetas_ml": ["identidad", "edad"],
  "opciones_respuesta": [ ... ]
}
```

**Formato del ID:** `{HDU}_{NPC_ID}_{FASE}_{NUMERO}`  
Ejemplo: `HDU2_NPC01_F2_Q03` = HDU-2, NPC 01, Fase 2, Pregunta 3

### Campos específicos HDU-2 (zona: `desconocidos`)
```json
{
  "npc_id": "NPC_01",
  "npc_nombre": "Alex",
  "npc_avatar": "alex_avatar",
  "fase": 1,
  "orden_en_fase": 2,
  "narrativa_continuacion": "HDU2_NPC01_F1_Q02"
}
```

### Campos específicos HDU-8 (zona: `chat_simulado`)
```json
{
  "escenario_id": "CHAT_GROOMING_01",
  "escenario_nombre": "El nuevo amigo online",
  "historial_previo": [
    {
      "remitente": "NPC",
      "npc_nombre": "Alex",
      "mensaje": "Hola, ¿me agregas?",
      "categoria": "grooming"
    }
  ]
}
```

### OpcionBanco — Opción de respuesta
```json
{
  "id": "HDU2_NPC01_F1_Q01_R1",
  "texto": "No, no te digo mi edad.",
  "tipo": "segura_optima",
  "consecuencia_narrativa": "Bien hecho, protegiste tu información.",
  "impacto_puntuacion": 2,
  "siguiente_pregunta": "HDU2_NPC01_F1_Q02"
}
```

| `tipo` | `impacto_puntuacion` | Descripción |
|---|---|---|
| `"insegura"` | `-1` | Respuesta riesgosa |
| `"segura_basica"` | `+1` | Respuesta correcta básica |
| `"segura_optima"` | `+2` | Respuesta óptima |

`siguiente_pregunta`: ID de la próxima pregunta, o `null` si termina la conversación.

---

## Modo Detective (HDU-10)

Archivo local (contenido fuente, cargado a la BD con `cargar_detective`):
`banco_preguntas/detective_cases.json`  
Modelos: `CasoDetective`, `MensajeDetective`, `CasoDetectiveProgreso` (`api/models.py`)

El jugador observa una conversación pregrabada entre dos NPCs (sin participar) y
marca los mensajes que considera señal de riesgo. A diferencia del banco de
preguntas, **este contenido no viaja empaquetado con el build de Unity** — se
consulta en vivo vía `/casos-detective/`, para poder agregar casos nuevos sin
tener que republicar la app.

### Estructura raíz (`detective_cases.json`, lo que carga `cargar_detective`)
```json
{
  "version": "1.8",
  "casos": [ ... ]
}
```

### Caso — Campos comunes
```json
{
  "id": "caso_01",
  "titulo": "Caso de Alex y Sam",
  "zona": "playa",
  "etiquetas_ml": ["grooming"],
  "permiso": {
    "player_text": "Oye, ¿me puedes ayudar? Recibí mensajes raros...",
    "npc_nombre": "Otto",
    "npc_response": "Claro, muéstramelos."
  },
  "conversacion": [ ... ]
}
```
El "permiso" es el intercambio previo: el jugador le pide a un NPC observar sus
mensajes con otro, antes de mostrar la conversación grabada.

### Mensaje de la conversación
```json
{
  "id": "m04",
  "npc_sender": "Alex",
  "texto": "No le cuentes esto a tus papás, es un secreto entre nosotros.",
  "es_senal_riesgo": true,
  "es_ambiguo": false,
  "explicacion": "Pedir guardar secretos a los papás es una señal de alerta clásica.",
  "nota_ambiguo": null
}
```
| Campo | Descripción |
|---|---|
| `es_senal_riesgo` | Si es `true`, marcarlo cuenta como acierto |
| `es_ambiguo` | No cuenta ni como acierto ni como error (CA5 de HDU-10) — se excluye del cálculo de `porcentaje` |
| `explicacion` | Solo en mensajes de riesgo; se muestra en el resumen si el jugador no lo marcó |
| `nota_ambiguo` | Solo en mensajes ambiguos; aclara por qué no cuenta |

### Cálculo de `porcentaje` (lo hace el backend; el cliente aplica la misma fórmula para mostrarla)
```
riesgo_real   = mensajes con es_senal_riesgo=true y es_ambiguo=false
aciertos      = de esos, cuántos marcó el jugador
porcentaje    = aciertos / total_riesgo   (1.0 si total_riesgo = 0)
```

`POST /casos-detective/{caso_id}/progreso/` **solo necesita `mensajes_marcados`**:
`aciertos`, `total_riesgo` y `porcentaje` los recalcula el servidor con esa misma
fórmula y son los que quedan guardados. Si el cuerpo los trae, se usan únicamente
para comparar y avisar por log cuando no coinciden — señal de que el cliente y el
banco no están viendo la misma versión del caso.

Es a propósito: el resultado alimenta el reporte del adulto responsable (HDU-13),
y un cliente con un bug o una petición hecha a mano dejarían números que después
nadie puede auditar. Las marcas sí vienen del cliente: son lo que el niño/a hizo,
no algo que el servidor pueda deducir.

---

## Modo local (fallback)

Si el backend no responde en 4 segundos, `ApiManager.cs` activa automáticamente el modo local:
- Los datos se simulan en memoria
- La persistencia usa `PlayerPrefs` de Unity
- El juego funciona sin conexión

Los archivos relevantes para este modo están en `ApiManager.cs` en las funciones con sufijo `Local`.

> **Fase 3:** el modo local también hay que actualizarlo, o el juego se comporta
> distinto según haya servidor o no. Necesita simular los perfiles de menores
> (listar, crear, y recordar cuál está seleccionado) para que el flujo sea el
> mismo en ambos modos.

---

## Errores comunes

| Código | Dónde | Causa |
|---|---|---|
| `400` | registro | falta el `email`, o el nombre/email ya existen |
| `400` | crear perfil | ya tienes otro perfil con ese nombre |
| `401` | cualquier endpoint | falta el header `Authorization: Token ...`, o el token no vale |
| `404` | crear partida | falta `usuario_jugador_id`, o el perfil es de otro adulto |
| `404` | partida / npc / chat | el recurso es de otro adulto |

Sobre los `404`: el backend **no distingue** entre "no existe" y "existe pero no
es tuyo", a propósito. Todo lo que cuelga de una partida se filtra por
`usuario_jugador__adulto`, así que un adulto nunca ve datos de otro.
