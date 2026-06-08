# HDU-8 — Herramientas de prevención (módulo de chat)

Chat tipo mensajería donde personajes del juego ponen a prueba al niño/a con
mensajes neutros y mensajes con señal de riesgo. Cada mensaje de riesgo ofrece
2-3 respuestas (lenguaje claro, 1 línea) que ramifican la historia. Al cerrar la
sesión de la zona, **Otto muestra su estado emocional** según el % de respuestas
seguras.

## Scripts

| Script | Rol |
|--------|-----|
| `ChatConversation.cs` | Datos de una conversación (grafo de nodos) como ScriptableObject. |
| `ChatDefaultConversations.cs` | Conversaciones de ejemplo listas (parque / fotos+secreto). |
| `ChatModuleController.cs` | Lógica: recorre nodos, registra historial, calcula % seguras, muestra estado de Otto. |
| `ChatModuleUI.cs` | UI de mensajería (historial + opciones + panel de estado). Se autogenera. |
| `OttoMoodController.cs` | Aplica la animación/sprite del estado emocional a Otto. |
| `ChatBackendLogger.cs` | Registro best-effort en el backend (start/request/chain/end). |
| `ChatModuleLauncher.cs` | Abre el chat desde un botón o al acercarse. |

## Clasificación de seguridad
- **Seguras** (`OptionSafety.Safe`): "Decir que no", "Bloquear", "Consultar a un adulto".
- **Inseguras** (`OptionSafety.Unsafe`): "Dar datos", "Aceptar encuentro", "Guardar secreto".
- `Neutral`: respuesta inocua; no cuenta para el porcentaje.

`% seguras = Safe / (Safe + Unsafe)`. Si **≥ 70 %** → *"Otto se siente seguro"*
(estado tranquilo). Por debajo → estado preocupado (configurable en `moodTiers`).

## Montaje de la escena

### Opción A — botón de UI
1. Crea un Button (p.ej. "Abrir chat").
2. Añade `ChatModuleLauncher` a un GameObject y, en el OnClick del Button, llama a
   `ChatModuleLauncher.OpenChat`.
3. `source`: `ZonaDesconocidosPorDefecto` (usa el contenido incluido) o
   `ConversacionesAsignadas` (tus propios `ChatConversation`).

### Opción B — al acercarse
1. En el GameObject del módulo pon un `Collider2D` (Is Trigger) + `ChatModuleLauncher`.
2. Marca `openOnTriggerEnter`. Otto (tag `Player`) al entrar abre el chat.

### Estado emocional de Otto (opcional pero recomendado)
1. Añade `OttoMoodController` al GameObject de Otto.
2. Si Otto tiene Animator, crea triggers `Seguro` / `Preocupado` (se disparan solos).
   También puedes asignar `seguroSprite` / `preocupadoSprite`.
3. Enlaza ese `OttoMoodController` en el `ChatModuleLauncher` (o se busca solo).

### EventSystem
La escena necesita un **EventSystem** para que respondan los botones (se crea solo
al añadir el primer Canvas, o GameObject → UI → Event System).

## Crear conversaciones propias
`Create → Fishy → Conversación Chat`. Reglas:
- Define `startNodeId` y los `nodes`.
- Incluye al menos un nodo **Neutral** y uno **Risk** (mensaje-solo encadena con
  `nextNodeId`; el de riesgo lleva `options`).
- Cada `option`: `safety` (Safe/Unsafe/Neutral), `text` (1 línea) y `nextNodeId`.
- Nodos de cierre: `isSystem = true` + `closesChat = true` con el mensaje de
  seguridad (p.ej. al bloquear).

## Cobertura de criterios de aceptación
- ✅ Al abrir el chat, el historial muestra ≥2 mensajes (uno neutro y uno con señal
  de riesgo), **sin etiquetar** cuál es peligroso.
- ✅ Cada mensaje de riesgo presenta 2-3 opciones claras; **Otto no responde** hasta
  que el jugador elige.
- ✅ La respuesta elegida queda en el historial y la narrativa **ramifica**
  (aceptar → propone encuentro; rechazar → insiste; bloquear → cierra con mensaje
  de seguridad).
- ✅ Al cerrar tras responder todo: si **≥70 %** de las respuestas fueron seguras,
  *"Otto se siente seguro"* (tranquilo, sonrisa); si no, estado preocupado.
