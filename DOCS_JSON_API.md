# Documentación JSON — Comunicación con la Base de Datos

## Índice
1. [Arquitectura general](#arquitectura-general)
2. [Autenticación](#autenticación)
3. [Modelos de datos (DTOs)](#modelos-de-datos-dtos)
4. [Endpoints y payloads](#endpoints-y-payloads)
5. [Banco de preguntas](#banco-de-preguntas)
6. [Modo local (fallback)](#modo-local-fallback)

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

### POST `/auth/registro/` — Registro de usuario
**Request:**
```json
{
  "nombre": "string",
  "password": "string"
}
```
**Response:**
```json
{
  "token": "string",
  "usuario_id": 1
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
**Response:**
```json
{
  "token": "string",
  "usuario_id": 1
}
```

El token recibido se almacena en `ApiManager.Token` y se adjunta a todos los requests posteriores.

---

## Modelos de datos (DTOs)

Definidos en `Fishy!/Assets/Scripts/ApiManager.cs` (líneas 560–639).

### AuthResponse
```json
{
  "token": "string",
  "usuario_id": 1
}
```

### PartidaDto — Sesión de juego
```json
{
  "id": 1,
  "usuario": 1,
  "progreso": 45.5,
  "nivel_riesgo": 2,
  "fecha_inicio": "2025-01-01T12:00:00Z",
  "fecha_update": "2025-01-01T12:30:00Z"
}
```
| Campo | Tipo | Descripción |
|---|---|---|
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
{ "progreso": 0.0, "nivel_riesgo": null }

// Response: PartidaDto
```

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
