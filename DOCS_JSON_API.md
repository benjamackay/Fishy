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
7. [Modo local (fallback)](#modo-local-fallback)

---

## Flujo general

```
registro / login  ──▶  GET /jugadores/  ──▶  elegir perfil  ──▶  POST /partidas/
   (adulto)              (o POST si                              con usuario_jugador_id
                          es la 1ª vez)
```

De ahí en adelante (NPCs, chats, mensajes, banco) todo funciona igual que antes,
usando el `id` de la partida.

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
  "pregunta_banco_id": "HDU2_NPC01_F2_Q01"
}
```

**Response de todos los tipos:** `MensajeDto`

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

---

## Banco de preguntas

Archivo local: `Fishy!/Assets/Resources/banco_preguntas.json`  
Cargado por: `BancoPreguntasLoader.cs` usando `JsonUtility`  
Modelos en: `BancoPreguntasData.cs`

### Estructura raíz
```json
{
  "version": "1.3",
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
