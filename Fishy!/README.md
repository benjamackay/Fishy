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
   - [Sistema de Misiones](#sistema-de-misiones)
   - [Modo Detective y recompensas](#modo-detective-y-recompensas)
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
| Base de datos | PostgreSQL (Supabase, administrado en la nube) | 16 |
| Autenticación backend | Token Authentication (DRF) | — |
| Infraestructura | Docker Compose (solo servicio `web`) | — |
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
│  ColaDeCambios / SaveManager       ▼               │
└─────────────────────────────────────────────────────┘
                                     │
                              HTTP/REST (JSON)
                                     │
┌─────────────────────────────────────────────────────┐
│                  DJANGO BACKEND                     │
│                                                     │
│  /api/auth/       /api/jugadores/   /api/partidas/  │
│  /api/chats/      /api/npcs/        /api/banco/     │
│  /api/casos-detective/  /api/dialogos-npc/  /api/health/ │
│                                                     │
│  models.py ──► PostgreSQL (Supabase, en la nube)    │
└─────────────────────────────────────────────────────┘
```

### Modo local (offline)

`ApiManager` tiene un flag `useLocalMode` (por defecto `false`). Si el health-check al backend falla al arrancar, se activa automáticamente: todas las llamadas se simulan en memoria/PlayerPrefs con las mismas firmas de método, por lo que el resto del juego no sabe ni le importa si hay servidor o no.

---

## Módulos implementados

### HDU-5 — Movimiento y zonas

Movimiento 2D de Otto con el nuevo Input System, cámara con seguimiento suave y sistema de **zonas bloqueadas** con condiciones de desbloqueo.

Al desbloquearse, `BlockedZone.Unlock()` ya no quita el bloqueo en seco: dispara una **cinemática de desbloqueo** (`ZoneUnlockCinematic`) que hace un paneo de cámara hacia la zona, desvanece el oscurecido y muestra un cartel, y solo entonces aplica el cambio de estado real (`BlockedZone.UnlockInmediato()`). `UnlockInmediato()` se mantiene como el desbloqueo silencioso/sin cámara que se usa al restaurar una partida donde la zona ya estaba abierta.

| Script | Ubicación | Función |
|--------|-----------|---------|
| `OttoController.cs` | `Scripts/Otto/` | Movimiento físico (Rigidbody2D + IA) |
| `CameraFollow2D.cs` | `Scripts/Camera/` | Seguimiento con lerp configurable |
| `BlockedZone.cs` | `Scripts/Otto/` | Zona que bloquea el paso hasta completar la HDU anterior |
| `ZoneUnlockCinematic.cs` | `Scripts/Otto/` | Cinemática de desbloqueo (paneo + cartel) que corre antes de aplicar el desbloqueo real |
| `ZonePopupUI.cs` | `Scripts/Otto/` | Notificación emergente al entrar en una zona de riesgo |
| `WorldZoneManager.cs` | `Scripts/Otto/` | Registro global del estado de las zonas |

---

### HDU-2 — Zona Desconocidos

NPCs que simulan tácticas de **grooming progresivo**: ganan confianza del jugador y luego van escalando las peticiones (datos, secretos, encuentros).

> **El diálogo en sí ya no vive en esta carpeta.** El guion, la UI, la ramificación y el registro en el backend corren por el módulo de chat genérico de HDU-8 (`Scripts/Chat/`, `ChatModuleLauncher`/`ChatModuleController`), que arma las conversaciones de esta zona a partir del `banco_preguntas.json` compartido. Los scripts de `Scripts/Zonas/BosqueDesconocidos/` son solo lo específico de la zona: decidir éxito/fracaso, hacer que el NPC "se aleje" y desbloquear la siguiente zona. La versión anterior (`DialogueController`, `DialogueUI`, `GroomingDialogue`, `DesconocidosDefaultScripts`, `GroomingChatLogger`) se movió a `deprecated/Desconocidos/` en la raíz del repo.

#### Flujo de interacción

```
Otto entra en rango del NPC (Collider2D trigger, ChatModuleLauncher.openOnTriggerEnter)
  └─► ChatModuleController recorre el grafo armado desde el banco de preguntas
        ├─► Fase 1: mensaje neutro + halago
        ├─► Fase 2: pide nombre / edad
        ├─► Fase 3: pide dirección / colegio
        └─► Fase 4: propone encuentro secreto
              └─► ChatModuleLauncher.OnSesionFinalizada(safePercent)
                    └─► BosqueDesconocidosNPC decide éxito/fracaso (umbral 70% por defecto)
                          ├─► Éxito → NPC se aleja + mensaje de felicitación
                          └─► Fracaso → cierre educativo (nodo de sistema del grafo)
                    └─► Cuando terminan todos los NPCs de la temática → BosqueDesconocidosManager
                          marca la temática completada y desbloquea la siguiente zona
```

| Script | Función |
|--------|---------|
| `BosqueDesconocidosNPC.cs` | Reacciona cuando el `ChatModuleLauncher` de este NPC cierra su sesión: decide éxito/fracaso según % de respuestas seguras, hace que el NPC "se aleje" si corresponde, y avisa al manager |
| `BosqueDesconocidosManager.cs` | Lleva la cuenta de los NPCs de la zona; al completarse todos, marca la temática y habilita la siguiente zona del mapa (con la cinemática de `ZoneUnlockCinematic`) |

---

### HDU-8 — Chat de prevención

Módulo de chat tipo mensajería que simula conversaciones con señales de riesgo (ciberacoso, retos virales, grooming en plataformas). El jugador elige entre 2-3 respuestas por mensaje peligroso. Al finalizar, Otto muestra su estado emocional según el porcentaje de respuestas seguras.

**Aspecto unificado:** `ChatModuleUI.EnablePhoneMode()` decide el diseño completo, no solo el marco. Hablar cara a cara con un NPC (sea neutro o sospechoso) se ve siempre como el panel de diálogo de los NPCs neutros; hablar por teléfono (sea con un NPC sospechoso o dentro del Modo Detective) se ve siempre como una app de mensajería, con el mismo aspecto que el Modo Detective. Antes eran 4 sistemas visualmente distintos (NPC neutro, NPC sospechoso cara a cara, NPC sospechoso por teléfono, Modo Detective); ahora hay dos aspectos —cara a cara y teléfono— compartidos entre esos cuatro casos.

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

#### El banco también trae diálogos de NPCs neutros

Además de `preguntas`, el JSON tiene un arreglo `dialogos_npc_neutros` (cargado por `DialogoNpcLoader.cs`, `Scripts/NPC/`) con las líneas de los NPCs que no son de riesgo. Cada entrada puede traer un campo opcional `mision_desbloquea`: si el `NPC` de la escena tiene su `dialogoId` puesto y coincide con el `id` de esa entrada, `MissionGiver` puede resolver solo qué misión entregar sin que nadie escriba el id a mano (ver [Sistema de Misiones](#sistema-de-misiones)).

---

### Sistema de Misiones

`CatalogoMisiones` es la fuente de verdad de qué misiones existen y qué pide cada una. Al arrancar se lee de forma síncrona `Resources/misiones.json` (nunca falla, no depende de red); si más tarde responde el backend (`GET /misiones/`, hoy sin implementar del lado servidor) reemplaza el catálogo en memoria y avisa a los `MissionGiver` que todavía no entregaron su misión para que se resuelvan solos. El catálogo es de contenido, no de progreso: qué misiones lleva hechas un niño/a concreto lo sigue llevando `MissionManager` + `MisionBackendSync`, por su lado.

Cada misión del catálogo puede traer, opcionalmente, `desbloquea_mision` y `recompensa_item_id` (+ `recompensa_cantidad`). Los resuelve `ConexionAutomaticaMisiones`, que se crea sola al arrancar: al completarse una misión con esos campos, entrega automáticamente la siguiente misión encadenada y/o agrega el ítem de recompensa al inventario (con guardia anti-duplicado). Esto no reemplaza a `DisparadorDeMision`/`EntregarMisionAlEntrarZona` para misiones que necesitan algo más que "dar esta misión o este ítem" (cinemática, desbloqueo de zona, mensaje propio); dejando los campos vacíos la conexión sigue siendo manual, como antes.

Un NPC también puede resolver sola la misión que entrega: si su `MissionGiver` no tiene `Mision Id` ni `Desafio` asignados, busca en `dialogos_npc_neutros` del banco de preguntas la entrada cuyo `id` sea el `dialogoId` del NPC, y si trae `mision_desbloquea`, la usa.

| Script | Ubicación | Función |
|--------|-----------|---------|
| `CatalogoMisiones.cs` | `Scripts/Mision/Nucleo/` | Catálogo en memoria: las dos fuentes (archivo/backend) y la fábrica de fichas |
| `CatalogoDesafios.cs` | `Scripts/Mision/Nucleo/` | Registro de fichas fabricadas en memoria para las misiones que no son un asset |
| `MisionCatalogoSync.cs` | `Scripts/Mision/` | Espera a que haya sesión y baja el catálogo desde el backend |
| `ConexionAutomaticaMisiones.cs` | `Scripts/Mision/` | Encadena misiones y entrega recompensas automáticamente según el catálogo |
| `MissionGiver.cs` | `Scripts/Mision/` | Entrega una misión (de un NPC), tomando ficha/objetivos del catálogo si no están cableados a mano |
| `MissionManager.cs` | `Scripts/Mision/Nucleo/` | Progreso de misiones de la partida actual |
| `MissionTracker.cs` | `Scripts/Mision/` | Sigue el progreso de los objetivos de una misión activa |
| `ObjetivoMision.cs` | `Scripts/Mision/` | Resuelve identificadores del catálogo a objetos concretos de la escena |
| `MisionInicial.cs` | `Scripts/Mision/` | Entrega la primera misión sin que haga falta un NPC |
| `Resources/misiones.json` | `Resources/` | Respaldo local del catálogo (misma forma de datos que `GET /misiones/`) |

---

### Modo Detective y recompensas

El Modo Detective (HDU-10) presenta un caso: una conversación ya ocurrida donde el niño/a debe marcar qué mensajes fueron señales de riesgo (`DetectiveCaseManager`, `DetectiveUI`). Al calcular el resultado, si el % de aciertos alcanza el umbral del caso, se entrega automáticamente un **pin** de recompensa al inventario (sin duplicarse si el caso se repite) y, junto con el primer pin, un ítem mínimo de **álbum de evidencias** (`AlbumEvidenciasUI`) — adelanto de HDU-12, que agrupará las evidencias por caso más adelante. La recompensa de cada caso se busca primero en lo que trae el propio caso desde el backend y, si no vino nada, cae al catálogo local `CatalogoRecompensasDetective` (mismo contenido, embebido en el juego); esto cubre caso sin backend, backend sin el campo todavía, y juego sin conexión con el mismo código.

| Script | Ubicación | Función |
|--------|-----------|---------|
| `DetectiveCaseManager.cs` | `Scripts/Detective/` | Calcula el resultado del caso y otorga la recompensa (pin + álbum) si corresponde |
| `CatalogoRecompensasDetective.cs` | `Scripts/Detective/` | Respaldo local de recompensas por caso (umbral, ítem, si no se duplica al repetir) |
| `DetectiveRewardPopup.cs` | `Scripts/Detective/` | Cartel de recompensa obtenida |
| `DetectiveRewardEvents.cs` | `Scripts/Detective/` | Evento estático que avisa cuándo se otorgó una recompensa |
| `DetectiveCaseLoader.cs` / `DetectiveCaseData.cs` | `Scripts/Detective/` | Carga de casos (backend con respaldo local) |

---

### Sistema de autenticación y partida

#### Flujo de login en tres pasos (control parental)

El login dejó de ser un solo paso: ahora la cuenta que se autentica es la del **adulto responsable**, que gestiona uno o más **perfiles de menores** (`UsuarioJugador`), y cada perfil puede tener varias partidas guardadas.

```
AuthScreen.Awake()
  └─► ApiManager.CheckHealth()   GET /api/health/ (timeout 4s)
        ├─► OK  → badge 🟢, formulario habilitado (modo BD real)
        └─► Fallo → useLocalMode = true, badge 🔴 (modo offline)

Paso 1 — cuenta del adulto (modo "Iniciar sesión" / "Crear cuenta", con toggle explícito)
  └─► ApiManager.Login(user, pass) o ApiManager.Registro(user, email, pass)
        └─► OK → OnAuthSuccess()

Paso 2 — perfil de menor
  └─► ApiManager.ListarJugadores() → elegir uno existente o crear uno nuevo
        └─► ChooseProfile(jugador) → ApiManager.SeleccionarJugador(jugador.id)

Paso 3 — partida
  └─► ApiManager.ObtenerPartidasJugador(jugador.id)
        ├─► Sin partidas → EmpezarPartidaNueva() → StartGame()
        └─► Con partidas → ShowSaves() → elegir cuál continuar → StartGame()
```

A diferencia de antes, el registro ya no es un fallback automático del login: son dos modos explícitos con un botón para alternar entre ellos, y crear cuenta pide además un email.

#### Sesión persistente entre escenas

`ApiManager` usa `DontDestroyOnLoad`. Si al volver a la escena de login ya existe token válido, `AuthScreen` lo detecta con `ApiManager.Instance.IsLoggedIn` y va directo al juego sin mostrar el formulario.

---

## Backend Django

### Modelos principales

> El modelo de cuentas se dividió en dos: **`AdultoResponsable`** es la única entidad con login (`AUTH_USER_MODEL`); gestiona uno o más **`UsuarioJugador`** (perfiles de menores, sin credenciales propias), y toda la data de juego cuelga del `UsuarioJugador`, no de la cuenta.

```
AdultoResponsable (AbstractBaseUser)
  └── nombre (unique), email (unique), apellido, edad, password (hashed)

UsuarioJugador
  └── adulto FK, nombre, edad

Partida
  └── usuario_jugador FK, progreso, nivel_riesgo FK, fechas

PersonajeJugador (1 a 1 con Partida)
  └── escena, pos_x, pos_y, zona_actual        ← restaurar a Otto donde quedó

NPC
  └── partida FK, nombre, area, tipo (aliado/neutral/enemigo), confianza

Chat
  └── partida FK, npc FK, categoria_riesgo, fecha_inicio, fecha_termino

Mensaje
  └── chat FK, tipo (start/chain/request/end),
      respuesta, calidad_respuesta (buena/neutral/mala),
      pregunta_banco_id, opcion_banco_id ← vinculan con el banco de preguntas
      timestamp

PosibleRespuesta
  └── mensaje FK, texto, orden, calidad_respuesta

PreguntaBanco / OpcionBanco
  └── igual que antes (pregunta_id, hdu, zona, npc_id, fase... / opcion_id, tipo,
      consecuencia_narrativa, impacto_puntuacion, siguiente_pregunta)

Zona / ZonaProgreso, Mision / MisionProgreso
  └── catálogo + progreso por partida de zonas y misiones (HDU-3/4/1 CA5)

ItemInventario, ObjetoRecogido, NpcProgreso
  └── inventario, objetos ya recogidos y avance por NPC, todos por partida (HDU-15)

RecompensaAlbum / RecompensaObtenida
  └── catálogo y progreso del álbum de evidencias

CasoDetective, MensajeDetective, CasoDetectiveProgreso
  └── casos del Modo Detective y el progreso/resultado por partida (HDU-10/11)

DialogoNPC
  └── diálogos de NPCs neutros (equivalente backend de `dialogos_npc_neutros`), con pista_mision
```

### Migraciones aplicadas

El backend ya pasó de las 5 migraciones originales a 12 (`0001_initial` … `0012_personaje_zona_actual`), que fueron agregando —en este orden aproximado— el split de cuentas y control parental, el modelo Detective, el catálogo de misiones y álbum, el progreso de misiones/zonas, el inventario y los objetos recogidos, y el progreso por NPC. Ver `Backend/backend/api/migrations/` para el detalle migración por migración.

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
│   ├── banco_preguntas.json          ← banco oficial (50 preguntas + diálogos de NPCs neutros)
│   └── misiones.json                 ← respaldo local del catálogo de misiones
│
└── Scripts/
    ├── ApiManager.cs                 ← cliente HTTP central (Login, Partida, NPC, Chat...)
    ├── ColaDeCambios.cs              ← cola de cambios en memoria (ver Sistema de guardado más abajo)
    ├── SaveManager.cs                ← dispara el vaciado de la cola al cambiar de zona / cerrar el juego
    ├── PersonajeBackendSync.cs, InventarioBackendSync.cs, ObjetosRecogidosSync.cs,
    │   NpcTematicaSync.cs, MisionBackendSync.cs           ← encolan cambios en vez de llamar directo al backend
    ├── VariablesJugador.cs, GlobalHelper.cs, IInteractable.cs, ApiSmokeTest.cs
    │
    ├── Camera/
    │   └── CameraFollow2D.cs
    │
    ├── Chat/                         ← HDU-8 (también usado por HDU-2, ver más abajo)
    │   ├── BancoPreguntasData.cs     ← clases serializables (mapeo JSON)
    │   ├── BancoPreguntasLoader.cs   ← carga JSON → ChatConversation en runtime
    │   ├── BancoBackendSync.cs       ← sincroniza el banco con el backend, con respaldo local
    │   ├── ChatBackendLogger.cs      ← graba la conversación y la encola entera al terminar
    │   ├── ChatConversation.cs       ← ScriptableObject: grafo de nodos
    │   ├── ChatDefaultConversations.cs
    │   ├── ChatModuleController.cs   ← lógica de sesión + cálculo de estado Otto
    │   ├── ChatModuleLauncher.cs
    │   ├── ChatModuleUI.cs           ← UI chat, aspecto cara a cara o teléfono según el caso
    │   ├── ChatUITheme.cs            ← colores/medidas compartidos (cara a cara, teléfono, ánimo)
    │   ├── VarianteSegunVariable.cs
    │   └── OttoMoodController.cs
    │
    ├── Zonas/
    │   ├── BosqueDesconocidos/       ← HDU-2 (lo específico de la zona; el diálogo lo pone Chat/)
    │   │   ├── BosqueDesconocidosNPC.cs
    │   │   └── BosqueDesconocidosManager.cs
    │   ├── PolygonColliderVisual.cs
    │   └── Triangulator.cs
    │
    ├── NPC/                          ← diálogo de NPCs neutros (unificado con el chat, ver arriba)
    │   ├── NPC.cs, NPCDialogue.cs
    │   ├── DialogoNpcLoader.cs       ← carga `dialogos_npc_neutros` del banco
    │   └── InteractionDetector.cs
    │
    ├── Otto/                         ← HDU-5
    │   ├── BlockedZone.cs
    │   ├── ZoneUnlockCinematic.cs    ← cinemática de desbloqueo (paneo + cartel)
    │   ├── OttoController.cs
    │   ├── OttoOnScreenButton.cs
    │   ├── PuntoDeAparicion.cs, ZonaActual.cs, ZonaMundo.cs
    │   ├── WorldZoneManager.cs
    │   └── ZonePopupUI.cs
    │
    ├── Phone/                        ← celular diegético
    │   ├── OttoPhone.cs              ← objeto físico: vibración, pantalla on/off
    │   ├── PhoneChatLauncher.cs      ← trigger de zona + secuencia completa
    │   └── PhoneZoomController.cs    ← zoom de cámara + overlay de fade
    │
    ├── Mision/                       ← ver tabla en «Sistema de Misiones»
    │   └── Nucleo/                   ← CatalogoMisiones, CatalogoDesafios, MissionManager
    │
    ├── Detective/                    ← ver tabla en «Modo Detective y recompensas»
    │
    ├── Inventario/
    │   ├── InventoryManager.cs, InventoryManagerUI.cs
    │   ├── Item.cs, ItemData.cs, CatalogoItems.cs, WorldItem.cs
    │   └── AlbumEvidenciasUI.cs      ← ítem mínimo de álbum de evidencias (HDU-11/HDU-12)
    │
    ├── Menu/                         ← menús de inicio, pausa y pestañas de la mochila
    │
    └── UI/                           ← arranque y HUD
        ├── AuthScreen.cs             ← login en 3 pasos (cuenta → perfil → partida) + health check
        ├── LoadingScreen.cs, UiBootstrap.cs
        ├── MenuPausa.cs              ← salir del juego, cartel de "sin conexión" al cerrar
        ├── MarcoTelefono.cs          ← chrome de teléfono compartido (Chat, Detective)
        └── DialogoNeutroSkin.cs, DialogoNeutroTheme.cs  ← aspecto compartido del panel "cara a cara"
```

**Namespaces:**

| Namespace | Scripts |
|-----------|---------|
| `Fishy.Net` | `ApiManager`, `ColaDeCambios`, sincronizadores backend |
| `Fishy.Chat` | Chat, BancoPreguntas |
| `Fishy.Zonas.BosqueDesconocidos` | HDU-2 (`BosqueDesconocidosNPC`/`Manager`) |
| `Fishy.Mision` | Núcleo del sistema de misiones (`CatalogoMisiones`, `MissionManager`...) |
| `Fishy.Detective` | Modo Detective y recompensas |
| `Fishy.Otto` | HDU-5 |
| `Fishy.Phone` | Celular diegético |
| `Fishy.World` | `ZonePopupUI`, `SaveManager`, `VariablesJugador` |
| `Fishy.UI` | `AuthScreen`, `LoadingScreen`, `MenuPausa`, `MarcoTelefono`... |
| `Fishy.Camera` | `CameraFollow2D` |

> Varias carpetas nuevas (`Mision/` fuera de `Nucleo/`, `Inventario/`, `NPC/`, `Menu/`) tienen scripts sin namespace declarado (namespace global) en vez de uno propio.

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
    │   ├── settings.py               AUTH_USER_MODEL = "api.AdultoResponsable"
    │   └── urls.py                   path("api/", include("api.urls"))
    └── api/
        ├── models.py                 AdultoResponsable, UsuarioJugador, Partida, NPC, Chat,
        │                              Mensaje, PreguntaBanco, Zona, Mision, CasoDetective...
        ├── serializers.py
        ├── views.py
        ├── urls.py
        ├── admin.py
        ├── migrations/                0001_initial.py … 0012_personaje_zona_actual.py
        └── management/commands/
            └── cargar_banco.py       comando: python manage.py cargar_banco
```

---

## API REST — Endpoints

Todos los endpoints autenticados requieren el header:
```
Authorization: Token <token>
```

### Auth (sin autenticación) — cuenta del adulto responsable

| Método | URL | Descripción |
|--------|-----|-------------|
| `GET` | `/api/health/` | Health check |
| `POST` | `/api/auth/registro/` | Crear cuenta `{nombre, email, password, ...}` |
| `POST` | `/api/auth/login/` | Login `{nombre, password}` → `{token, adulto_id}` |
| `GET` | `/api/auth/perfil/` | Datos de la cuenta autenticada |

### Perfiles de menores (control parental)

| Método | URL | Descripción |
|--------|-----|-------------|
| `GET/POST` | `/api/jugadores/` | Listar / crear perfiles de menor del adulto autenticado |
| `GET` | `/api/jugadores/{id}/` | Detalle de un perfil |
| `GET` | `/api/jugadores/{id}/partidas/` | Partidas guardadas de ese perfil |

### Partida

| Método | URL | Descripción |
|--------|-----|-------------|
| `POST` | `/api/partidas/` | Crear partida (de un perfil de menor) |
| `GET/PATCH` | `/api/partidas/{id}/` | Ver / actualizar progreso |
| `GET/POST` | `/api/partidas/{id}/npcs/` | Listar / registrar NPC |
| `PATCH` | `/api/npcs/{id}/` | Actualizar confianza del NPC |
| `GET` | `/api/partidas/{id}/misiones/`, `/zonas/`, `/progreso-npcs/` | Progreso de misiones, zonas y NPCs (HDU-1/3/4 CA5) |
| `GET/PATCH/PUT` | `/api/partidas/{id}/personaje/`, `/inventario/`, `/objetos-recogidos/` | Posición de Otto, inventario y objetos recogidos (HDU-15) |
| `GET` | `/api/partidas/{id}/riesgo-por-zona/`, `/presion-social/`, `/oportunidades-mejora/` | Reportes para el adulto |

### Chat (HDU-8, también usado por HDU-2)

| Método | URL | Descripción |
|--------|-----|-------------|
| `POST` | `/api/chats/` | Iniciar chat |
| `GET` | `/api/chats/{id}/mensajes/` | Historial completo |
| `POST` | `/api/chats/{id}/mensajes/registrar/` | Registrar mensaje + `pregunta_banco_id`/`opcion_banco_id` |
| `POST` | `/api/chats/{id}/finalizar/` | Cerrar chat (crea mensaje `end`) |

### Banco de Preguntas

| Método | URL | Descripción |
|--------|-----|-------------|
| `GET` | `/api/banco/preguntas/` | Listar (filtros: `zona`, `npc_id`, `fase`, `hdu`, `solo_riesgo`) |
| `GET` | `/api/banco/preguntas/{pregunta_id}/` | Detalle de una pregunta |
| `GET` | `/api/banco/zonas/`, `/api/banco/zonas/{zona}/preguntas/` | Zonas del banco y sus preguntas |
| `GET` | `/api/dialogos-npc/`, `/api/dialogos-npc/{id}/` | Diálogos de NPCs neutros (HDU-1) |

### Modo Detective (HDU-10)

| Método | URL | Descripción |
|--------|-----|-------------|
| `GET` | `/api/casos-detective/`, `/api/casos-detective/{caso_id}/` | Listar / detalle de casos |
| `POST` | `/api/casos-detective/{caso_id}/progreso/` | Registrar progreso/resultado de un caso |
| `GET` | `/api/partidas/{id}/casos-detective/` | Progreso Detective de la partida |

> No existe (todavía) un `GET /misiones/` de catálogo — el sistema de misiones corre hoy con el respaldo local `Resources/misiones.json` (ver [Sistema de Misiones](#sistema-de-misiones)).

---

## Modelo de datos

### `Mensaje` — campos clave

```python
tipo             = CharField  # "start" | "request" | "chain" | "end"
respuesta        = TextField  # texto enviado
calidad_respuesta = CharField # "buena" | "neutral" | "mala"
pregunta_banco_id = CharField # "HDU2_NPC01_F2_Q01" — vincula con la pregunta del banco
opcion_banco_id   = CharField # "HDU2_NPC01_F2_Q01_R2" — vincula con la opción elegida
timestamp        = DateTimeField(auto_now_add=True)
```

Los campos `pregunta_banco_id` y `opcion_banco_id` son el nexo de trazabilidad entre las respuestas registradas en la BD y las preguntas/opciones pedagógicas del banco — `opcion_banco_id` es además la llave que permite acumular riesgo por zona (se resuelve contra `OpcionBanco` para obtener `impacto_puntuacion` y la zona de su pregunta), permitiendo reportes del tipo:

> *"El 73 % de los jugadores eligió una respuesta insegura ante la pregunta HDU2_NPC01_F2_Q01."*

---

## Puesta en marcha

### Backend

> La base de datos ya no es un contenedor Postgres local: es **Supabase** (Postgres administrado en la nube), y `docker-compose.yml` sólo levanta el servicio `web`. La BD compartida ya está migrada y con el banco cargado, así que normalmente no hace falta `migrate` ni `cargar_banco`.

```bash
cd Backend
cp .env.example .env            # primera vez: completar DB_PASSWORD (Supabase)

# Atajo recomendado (carga el .env solo)
./run.sh                        # servidor en 127.0.0.1:8000
./run.sh --check                # config + drift de migraciones
./run.sh --smoke                # smoke test (con el servidor ya corriendo)

# Verificar que está activo
curl http://127.0.0.1:8000/api/health/
# → {"status": "ok"}

# Cargar banco de preguntas en la BD (solo si hiciera falta)
python backend/manage.py cargar_banco
```

Existe también `docker-compose up --build` para correr el servicio `web` en Docker, y `run.ps1` (equivalente a `run.sh` en PowerShell). Ver `Backend/README.md` para el detalle de configuración de `.env` y el modo local con SQLite (`run.sh --local`, sin depender de Supabase).

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
