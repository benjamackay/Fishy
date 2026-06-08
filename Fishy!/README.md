# Fishy! — Informe Técnico

> Videojuego educativo 2D de prevención de riesgos en internet para niños y niñas de educación básica.
> Desarrollado en Unity 6000.4.9f1 · 2D URP · New Input System.

---

## Tabla de contenidos

1. [Descripción del proyecto](#descripción-del-proyecto)
2. [Stack tecnológico](#stack-tecnológico)
3. [Arquitectura general](#arquitectura-general)
4. [Módulos implementados](#módulos-implementados)
   - [HDU-5 — Movimiento y zonas](#hdu-5--movimiento-y-zonas)
   - [HDU-2 — Zona Desconocidos](#hdu-2--zona-desconocidos)
   - [HDU-8 — Chat de prevención](#hdu-8--chat-de-prevención)
   - [Celular diegético](#celular-diegético)
   - [Banco de Preguntas](#banco-de-preguntas)
   - [Sistema de autenticación y partida](#sistema-de-autenticación-y-partida)
5. [Backend Django](#backend-django)
6. [Flujo de datos pedagógico](#flujo-de-datos-pedagógico)
7. [Estructura de archivos Unity](#estructura-de-archivos-unity)
8. [Estructura de archivos Backend](#estructura-de-archivos-backend)
9. [API REST — Endpoints](#api-rest--endpoints)
10. [Modelo de datos](#modelo-de-datos)
11. [Puesta en marcha](#puesta-en-marcha)
12. [Bugs corregidos](#bugs-corregidos)

---

## Descripción del proyecto

**Fishy!** es un videojuego 2D de rol ligero en el que el jugador controla a **Otto**, un niño que recorre distintas zonas de un mundo 2D. Cada zona corresponde a una Habilidad Digital del Usuario (HDU) y expone al jugador a situaciones de riesgo en internet (grooming, manipulación, ciberacoso, retos virales). El juego enseña a reconocer y responder correctamente a esas situaciones a través de mecánicas de diálogo y toma de decisiones.

El sistema registra todas las respuestas del jugador en una base de datos PostgreSQL, lo que permite a educadores y especialistas analizar el nivel de riesgo percibido y el progreso de cada niño/a.

---

## Stack tecnológico

| Capa | Tecnología | Versión |
|------|-----------|---------|
| Motor de juego | Unity | 6000.4.9f1 |
| Pipeline gráfico | Universal Render Pipeline (URP) | 2D |
| Sistema de entrada | Unity New Input System | — |
| Lenguaje de juego | C# | .NET (Unity) |
| Serialización JSON (Unity) | Newtonsoft.Json + JsonUtility | — |
| Backend | Django + Django REST Framework | ≥5.0 |
| Base de datos | PostgreSQL | 16 |
| Autenticación backend | Token Authentication (DRF) | — |
| Infraestructura | Docker + Docker Compose | — |
| Lenguaje backend | Python | 3.12 |

---

## Arquitectura general

```
┌─────────────────────────────────────────────────────┐
│                    UNITY (cliente)                  │
│                                                     │
│  AuthScreen ──► ApiManager ──► UnityWebRequest      │
│                    │                │               │
│  ChatModuleController              HTTP             │
│  PhoneChatLauncher                 │               │
│  GroomingDialogue                  ▼               │
└─────────────────────────────────────────────────────┘
                                     │
                              HTTP/REST (JSON)
                                     │
┌─────────────────────────────────────────────────────┐
│                  DJANGO BACKEND                     │
│                                                     │
│  /api/auth/      /api/partidas/    /api/chats/      │
│  /api/npcs/      /api/banco/       /api/health/     │
│                                                     │
│  models.py ──► PostgreSQL (Docker)                  │
└─────────────────────────────────────────────────────┘
```

### Modo local (offline)

`ApiManager` tiene un flag `useLocalMode` (por defecto `false`). Si el health-check al backend falla al arrancar, se activa automáticamente: todas las llamadas se simulan en memoria/PlayerPrefs con las mismas firmas de método, por lo que el resto del juego no sabe ni le importa si hay servidor o no.

---

## Módulos implementados

### HDU-5 — Movimiento y zonas

Movimiento 2D de Otto con el nuevo Input System, cámara con seguimiento suave y sistema de **zonas bloqueadas** con condiciones de desbloqueo.

| Script | Ubicación | Función |
|--------|-----------|---------|
| `OttoController.cs` | `Scripts/Otto/` | Movimiento físico (Rigidbody2D + IA) |
| `CameraFollow2D.cs` | `Scripts/Camera/` | Seguimiento con lerp configurable |
| `BlockedZone.cs` | `Scripts/Otto/` | Zona que bloquea el paso hasta completar la HDU anterior |
| `ZonePopupUI.cs` | `Scripts/Otto/` | Notificación emergente al entrar en una zona de riesgo |
| `WorldZoneManager.cs` | `Scripts/Otto/` | Registro global del estado de las zonas |

---

### HDU-2 — Zona Desconocidos

NPCs que simulan tácticas de **grooming progresivo**: ganan confianza del jugador y luego van escalando las peticiones (datos, secretos, encuentros).

#### Flujo de interacción

```
Otto entra en rango del NPC (Collider2D trigger)
  └─► DesconocidosNPC.OnTriggerEnter2D
        └─► DialogueController.StartDialogue(script)
              ├─► Fase 1: mensaje neutro + halago
              ├─► Fase 2: pide nombre / edad
              ├─► Fase 3: pide dirección / colegio
              └─► Fase 4: propone encuentro secreto
                    ├─► Acepta → cierre educativo (EndCaptured)
                    └─► Rechaza → NPC se aleja (EndSuccess)
```

| Script | Función |
|--------|---------|
| `GroomingDialogue.cs` | ScriptableObject con el grafo de nodos + fases |
| `DesconocidosDefaultScripts.cs` | Guiones prefabricados: **Alex** y **Sam** |
| `DesconocidosNPC.cs` | NPC con trigger de cercanía y control de estados |
| `DialogueController.cs` | Recorre el grafo, aplica reglas de ramificación |
| `DialogueUI.cs` | Panel de diálogo + botones de respuesta (se autogenera) |
| `GroomingChatLogger.cs` | Registro best-effort en el backend (`start/request/chain/end`) |
| `ZonaDesconocidosManager.cs` | Marca la temática completada y desbloquea la siguiente zona |

---

### HDU-8 — Chat de prevención

Módulo de chat tipo mensajería que simula conversaciones con señales de riesgo (ciberacoso, retos virales, grooming en plataformas). El jugador elige entre 2-3 respuestas por mensaje peligroso. Al finalizar, Otto muestra su estado emocional según el porcentaje de respuestas seguras.

#### Grafo de nodos `ChatConversation`

```
INTRO ──► HIST_0 ──► HIST_1 ──► PREGUNTA_RIESGO
                                    ├─► Opción Segura  ──► CONS_{id} (consecuencia) ──► siguiente
                                    ├─► Opción Neutral ──► CONS_{id}
                                    └─► Opción Insegura ──► CONS_{id} ──► fin
```

Los nodos `CONS_{opcionId}` son **sintéticos** (generados en runtime por `BancoPreguntasLoader`): muestran la `consecuencia_narrativa` del banco de preguntas como retroalimentación pedagógica antes de avanzar.

#### Estado emocional de Otto

| % de respuestas seguras | Estado | Trigger Animator |
|------------------------|--------|-----------------|
| ≥ 70 % | 😌 Seguro | `Seguro` |
| < 70 % | 😟 Preocupado | `Preocupado` |

| Script | Función |
|--------|---------|
| `ChatConversation.cs` | ScriptableObject: grafo de nodos de la conversación |
| `ChatModuleController.cs` | Orquesta la sesión, calcula % seguras, activa estado Otto |
| `ChatModuleUI.cs` | UI de mensajería + modo teléfono (chrome, bisel, reloj) |
| `ChatBackendLogger.cs` | Registro asíncrono en el backend con cola de pendientes |
| `ChatDefaultConversations.cs` | Conversaciones de fallback (si el banco no carga) |
| `BancoPreguntasData.cs` | Clases serializables que mapean `banco_preguntas.json` |
| `BancoPreguntasLoader.cs` | Carga el JSON y construye `ChatConversation` en runtime |
| `OttoMoodController.cs` | Aplica sprite/animación según el estado emocional |

---

### Celular diegético

El celular de Otto es un **objeto físico en el mundo 2D** (hijo de Otto). Cuando entra en una zona de riesgo, la mecánica ocurre *dentro del juego*, sin salir a un menú:

#### Secuencia completa

```
1. Otto entra en la PhoneChatZone (Collider2D isTrigger)
2. OttoPhone.Vibrate()     → el teléfono vibra (animación de sacudida)
3. PhoneZoomController     → la cámara hace zoom hacia el teléfono
4. FadeOverlay             → fundido a negro (Canvas sort 9000)
5. ChatModuleUI            → se activa con "chrome" de teléfono (bisel, notch, reloj)
6. FadeOverlay inverso     → se revela la UI de chat
   ──────── el jugador lee y responde ────────
7. FadeOverlay             → fundido a negro
8. PhoneZoomController.ZoomOut() → cámara regresa, sigue a Otto
9. OttoPhone.TurnScreenOff()
10. ChatModuleUI.EnablePhoneMode(false)
11. Otto recupera el movimiento
```

#### Chrome del teléfono (generado en runtime)

`ChatModuleUI.ApplyPhoneChrome()` construye sobre el panel de chat:
- **Bisel** oscuro (640×1120 px)
- **Notch** central (120×28 px)
- **Barra de estado** con hora en tiempo real + iconos de batería
- **Barra inferior** (home bar)

| Script | Función |
|--------|---------|
| `OttoPhone.cs` | Objeto físico: vibración, pantalla on/off, singleton `Instance` |
| `PhoneZoomController.cs` | Zoom de cámara + overlay de fade (sort 9000), `DontDestroyOnLoad` |
| `PhoneChatLauncher.cs` | Trigger de zona, orquesta toda la secuencia |

#### Bug corregido: pantalla negra permanente

**Causa:** `ZoomInRoutine` hacía fade a negro, llamaba `onArrived()` (activaba el chat), pero nunca volvía a hacer fade inverso. La UI del chat (sort 950) quedaba tapada por el overlay sólido (sort 9000).

**Solución:**
```csharp
// En ZoomInRoutine, después de onArrived?.Invoke():
yield return null;                                          // 1 frame para que la UI se active
yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration)); // fade de negro a transparente
```

---

### Banco de Preguntas

Integración del banco oficial `banco_preguntas.json` (autoreado por Luis González — MLOps) como fuente de contenido para los chats del teléfono.

#### Pipeline de carga

```
Resources/banco_preguntas.json
    └─► BancoPreguntasLoader.Load()          (JsonUtility, caché estático)
          └─► CreateHDU2Conversations()      (agrupa por npc_id)
          └─► CreateHDU8Conversations()      (agrupa por escenario_id)
                └─► BuildConversationFromPreguntas()
                      ├─► Nodo INTRO
                      ├─► Nodos de pregunta (id = pregunta_banco_id)
                      └─► Nodos CONS_{opcionId} (consecuencia pedagógica)
```

#### Estructura del JSON

```jsonc
{
  "version": "1.0",
  "preguntas": [
    {
      "id": "HDU2_NPC01_F2_Q01",
      "hdu": "HDU-2",
      "zona": "desconocidos",
      "npc_id": "NPC_01",
      "npc_nombre": "Alex",
      "fase": 2,
      "es_mensaje_riesgo": true,
      "es_fin_de_npc": false,
      "mensaje_npc": "Oye, ¿cuál es tu nombre real?",
      "opciones_respuesta": [
        {
          "id": "HDU2_NPC01_F2_Q01_OPT_A",
          "texto": "No te lo digo, no te conozco bien.",
          "tipo": "segura_optima",
          "consecuencia_narrativa": "Alex se molesta un poco pero respetas tu privacidad.",
          "impacto_puntuacion": 10
        }
      ]
    }
  ]
}
```

#### Mapeo de tipos a seguridad

| `tipo` en JSON | `OptionSafety` en Unity |
|---------------|------------------------|
| `"segura_optima"` | `Safe` |
| `"segura_basica"` | `Safe` |
| `"insegura"` | `Unsafe` |

---

### Sistema de autenticación y partida

#### Flujo de login inteligente

```
AuthScreen.Awake()
  └─► ApiManager.CheckHealth()   GET /api/health/ (timeout 4s)
        ├─► OK  → badge 🟢, formulario habilitado (modo BD real)
        └─► Fallo → useLocalMode = true, badge 🔴 (modo offline)

AuthScreen.Submit() — modo "Entrar"
  └─► ApiManager.Login(user, pass)
        ├─► OK  → OnAuthSuccess() → CrearPartida() → StartGame()
        └─► Error → TryAutoRegister(user, pass)
              ├─► OK  → cuenta nueva creada → OnAuthSuccess()
              └─► Error "ya existe" → "Contraseña incorrecta."
```

**Ventaja pedagógica:** el jugador no necesita saber si ya tiene cuenta. Con el mismo botón "Entrar" se hace login o registro automáticamente.

#### Sesión persistente entre escenas

`ApiManager` usa `DontDestroyOnLoad`. Si al volver a la escena de login ya existe token válido, `AuthScreen` lo detecta con `ApiManager.Instance.IsLoggedIn` y va directo al juego sin mostrar el formulario.

---

## Backend Django

### Modelos principales

```
Usuario (AbstractBaseUser)
  └── nombre (unique), password (hashed)

Partida
  └── usuario FK, progreso, nivel_riesgo FK, fechas

NPC
  └── partida FK, nombre, area, tipo (aliado/neutral/enemigo), confianza

Chat
  └── partida FK, npc FK, categoria_riesgo, fecha_inicio, fecha_termino

Mensaje
  └── chat FK, tipo (start/chain/request/end),
      respuesta, calidad_respuesta (buena/neutral/mala),
      pregunta_banco_id ← vincula con el banco de preguntas
      timestamp

PosibleRespuesta
  └── mensaje FK, texto, orden, calidad_respuesta

PreguntaBanco
  └── pregunta_id (ej: "HDU2_NPC01_F2_Q01"), hdu, zona, npc_id,
      fase, mensaje_npc, es_mensaje_riesgo, es_fin_de_npc, ...

OpcionBanco
  └── pregunta FK, opcion_id, texto, tipo, consecuencia_narrativa,
      impacto_puntuacion, siguiente_pregunta
```

### Migraciones aplicadas

| Migración | Descripción |
|-----------|-------------|
| `0001_initial` | Modelos base: Usuario, NivelRiesgo, Partida, NPC, Chat, Mensaje, PosibleRespuesta |
| `0002_chat_fecha_termino...` | Agrega `fecha_termino` a Chat, `puntaje` a NivelRiesgo |
| `0003_banco_preguntas` | Modelos `PreguntaBanco` y `OpcionBanco` |
| `0004_preguntabanco_fin_flags` | Flags `es_fin_de_npc` y `es_fin_de_zona` en PreguntaBanco |
| `0005_mensaje_pregunta_banco_id` | Campo `pregunta_banco_id` en Mensaje (trazabilidad analítica) |

---

## Flujo de datos pedagógico

El campo `pregunta_banco_id` conecta cada respuesta del jugador con la pregunta exacta del banco que la originó, permitiendo análisis pedagógico detallado.

```
Banco de Preguntas (JSON)
  "HDU2_NPC01_F2_Q01"
         │
         ▼
  ChatNode.id = "HDU2_NPC01_F2_Q01"    (BancoPreguntasLoader)
         │
         ▼
  ChatModuleController.EnterNode(node)
    └─► logger.LogRequest(text, opciones, preguntaBancoId: node.id)
         │
         ▼
  Jugador elige opción
    └─► logger.LogChoice(text, calidad, preguntaBancoId: node.id)
         │
         ▼
  ApiManager.RegistrarRespuestaJugador(text, calidad, preguntaBancoId)
    └─► POST /api/chats/{id}/mensajes/registrar/
         {
           "tipo": "chain",
           "respuesta": "No te lo digo.",
           "calidad_respuesta": "buena",
           "pregunta_banco_id": "HDU2_NPC01_F2_Q01"
         }
         │
         ▼
  Mensaje { calidad="buena", pregunta_banco_id="HDU2_NPC01_F2_Q01" }
  guardado en PostgreSQL
```

---

## Estructura de archivos Unity

```
Assets/
├── Resources/
│   └── banco_preguntas.json          ← banco oficial (24 preguntas HDU-2 y HDU-8)
│
└── Scripts/
    ├── ApiManager.cs                 ← cliente HTTP central (Login, Partida, NPC, Chat)
    │
    ├── Camera/
    │   └── CameraFollow2D.cs
    │
    ├── Chat/                         ← HDU-8
    │   ├── BancoPreguntasData.cs     ← clases serializables (mapeo JSON)
    │   ├── BancoPreguntasLoader.cs   ← carga JSON → ChatConversation en runtime
    │   ├── ChatBackendLogger.cs      ← cola asíncrona de mensajes al backend
    │   ├── ChatConversation.cs       ← ScriptableObject: grafo de nodos
    │   ├── ChatDefaultConversations.cs
    │   ├── ChatModuleController.cs   ← lógica de sesión + cálculo de estado Otto
    │   ├── ChatModuleLauncher.cs
    │   ├── ChatModuleUI.cs           ← UI chat + chrome de teléfono
    │   └── OttoMoodController.cs
    │
    ├── Desconocidos/                 ← HDU-2
    │   ├── DesconocidosDefaultScripts.cs
    │   ├── DesconocidosNPC.cs
    │   ├── DialogueController.cs
    │   ├── DialogueUI.cs
    │   ├── GroomingChatLogger.cs
    │   ├── GroomingDialogue.cs
    │   └── ZonaDesconocidosManager.cs
    │
    ├── Otto/                         ← HDU-5
    │   ├── BlockedZone.cs
    │   ├── OttoController.cs
    │   ├── OttoOnScreenButton.cs
    │   ├── WorldZoneManager.cs
    │   └── ZonePopupUI.cs
    │
    ├── Phone/                        ← celular diegético (nuevo)
    │   ├── OttoPhone.cs              ← objeto físico: vibración, pantalla on/off
    │   ├── PhoneChatLauncher.cs      ← trigger de zona + secuencia completa
    │   └── PhoneZoomController.cs    ← zoom de cámara + overlay de fade
    │
    └── UI/                           ← arranque
        ├── AuthScreen.cs             ← login inteligente + health check
        ├── LoadingScreen.cs
        └── UiBootstrap.cs
```

**Namespaces:**

| Namespace | Scripts |
|-----------|---------|
| `Fishy.Net` | `ApiManager` |
| `Fishy.Chat` | Chat, BancoPreguntas |
| `Fishy.Desconocidos` | HDU-2 |
| `Fishy.Otto` | HDU-5 |
| `Fishy.Phone` | Celular diegético |
| `Fishy.World` | ZonePopupUI |
| `Fishy.UI` | AuthScreen, LoadingScreen |
| `Fishy.Camera` | CameraFollow2D |

---

## Estructura de archivos Backend

```
Backend/
├── docker-compose.yml
├── Dockerfile
└── backend/
    ├── requirements.txt              django, psycopg2-binary, djangorestframework
    ├── manage.py
    ├── juego_backend/
    │   ├── settings.py               AUTH_USER_MODEL = "api.Usuario"
    │   └── urls.py                   path("api/", include("api.urls"))
    └── api/
        ├── models.py                 Usuario, Partida, NPC, Chat, Mensaje, PreguntaBanco...
        ├── serializers.py
        ├── views.py
        ├── urls.py
        ├── admin.py
        ├── migrations/
        │   ├── 0001_initial.py
        │   ├── 0002_chat_fecha_termino_...
        │   ├── 0003_banco_preguntas.py
        │   ├── 0004_preguntabanco_fin_flags.py
        │   └── 0005_mensaje_pregunta_banco_id.py
        └── management/commands/
            └── cargar_banco.py       comando: python manage.py cargar_banco
```

---

## API REST — Endpoints

Todos los endpoints autenticados requieren el header:
```
Authorization: Token <token>
```

### Auth (sin autenticación)

| Método | URL | Descripción |
|--------|-----|-------------|
| `GET` | `/api/health/` | Health check |
| `POST` | `/api/auth/registro/` | Crear cuenta `{nombre, password}` |
| `POST` | `/api/auth/login/` | Login `{nombre, password}` → `{token, usuario_id}` |

### Partida (HDU-2)

| Método | URL | Descripción |
|--------|-----|-------------|
| `POST` | `/api/partidas/` | Crear partida |
| `GET/PATCH` | `/api/partidas/{id}/` | Ver / actualizar progreso |
| `GET/POST` | `/api/partidas/{id}/npcs/` | Listar / registrar NPC |
| `PATCH` | `/api/npcs/{id}/` | Actualizar confianza del NPC |

### Chat (HDU-8)

| Método | URL | Descripción |
|--------|-----|-------------|
| `POST` | `/api/chats/` | Iniciar chat |
| `GET` | `/api/chats/{id}/mensajes/` | Historial completo |
| `POST` | `/api/chats/{id}/mensajes/registrar/` | Registrar mensaje + `pregunta_banco_id` |
| `POST` | `/api/chats/{id}/finalizar/` | Cerrar chat (crea mensaje `end`) |

### Banco de Preguntas

| Método | URL | Descripción |
|--------|-----|-------------|
| `GET` | `/api/banco/preguntas/` | Listar (filtros: `zona`, `npc_id`, `fase`, `hdu`, `solo_riesgo`) |
| `GET` | `/api/banco/preguntas/{pregunta_id}/` | Detalle de una pregunta |

---

## Modelo de datos

### `Mensaje` — campos clave

```python
tipo             = CharField  # "start" | "request" | "chain" | "end"
respuesta        = TextField  # texto enviado
calidad_respuesta = CharField # "buena" | "neutral" | "mala"
pregunta_banco_id = CharField # "HDU2_NPC01_F2_Q01" — vincula con el banco
timestamp        = DateTimeField(auto_now_add=True)
```

El campo `pregunta_banco_id` (migración 0005) es el nexo de trazabilidad entre las respuestas registradas en la BD y las preguntas pedagógicas del banco, permitiendo reportes del tipo:

> *"El 73 % de los jugadores eligió una respuesta insegura ante la pregunta HDU2_NPC01_F2_Q01."*

---

## Puesta en marcha

### Backend

```bash
# Primera vez
cd Backend
docker-compose up --build

# Uso diario (sin reconstruir)
docker start backend-db-1 backend-web-1

# Verificar que está activo
curl http://127.0.0.1:8000/api/health/
# → {"status": "ok"}

# Cargar banco de preguntas en la BD (opcional)
docker-compose exec web python manage.py cargar_banco
```

> ⚠️ No usar `docker-compose down -v`: borra la base de datos.

### Unity

1. Abrir el proyecto en Unity 6000.4.9f1.
2. En el `ApiManager` del Inspector, verificar que `Base Url` apunta a `http://127.0.0.1:8000/api` y que `Use Local Mode` está **desmarcado**.
3. Abrir la escena `Boot` (o la escena de arranque configurada).
4. Pulsar **Play** — `AuthScreen` hace el health-check automáticamente.

#### Sin servidor (modo offline)

Si el backend no está corriendo, `ApiManager.CheckHealth()` detecta el timeout (4 s), activa `useLocalMode = true` automáticamente y el juego funciona con datos locales (PlayerPrefs + memoria). El badge de conexión mostrará `🔴 Sin conexión (modo local)`.

---

## Bugs corregidos

### Pantalla negra permanente tras el zoom al teléfono

| | Detalle |
|-|---------|
| **Síntoma** | Al entrar en una zona de riesgo, la cámara hacía zoom al teléfono, la pantalla se ponía negra y no se veía nada más. |
| **Causa** | `PhoneZoomController.ZoomInRoutine` hacía fade a negro (alpha 1), invocaba `onArrived()` (la UI de chat se activaba en sorting order 950), pero el overlay negro (sorting order 9000) nunca volvía a transparente. |
| **Corrección** | Después de `onArrived?.Invoke()`, se añadió un frame de espera y luego el fade inverso: `yield return null; yield return FadeRoutine(1f, 0f, fadeDuration);` |
| **Archivo** | `Assets/Scripts/Phone/PhoneZoomController.cs` |

---

*Última actualización: junio 2026 — Fishy! Development Team*
