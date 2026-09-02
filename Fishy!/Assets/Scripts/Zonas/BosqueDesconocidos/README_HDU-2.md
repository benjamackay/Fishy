# HDU-2 — Zona 1: Bosque de los Desconocidos (manipulación / grooming)

Sistema de NPCs con guiones de **manipulación progresiva**: ganan confianza,
intercalan temas neutros (discreto) y piden datos personales cada vez más
específicos, escalando hasta proponer un secreto o encuentro a solas.

Se apoya en HDU-5 (movimiento de Otto, zonas, desbloqueo) y en el `ApiManager`
existente (registro `grooming` en el backend, opcional).

> **Actualización:** el diálogo en sí (guion, UI, ramificación, registro en el
> backend) ya no vive acá — corre por el módulo de chat genérico de HDU-8
> (`Assets/Scripts/Chat/`, `ChatModuleController`/`ChatModuleLauncher`), que
> desde agosto también sabe armar las conversaciones de esta zona a partir del
> `banco_preguntas.json` compartido (`BancoPreguntasLoader.CreateHDU2Conversations`).
> Los scripts propios de esta carpeta ahora son solo lo que es **específico de
> la zona** — resultado de la interacción, "el NPC se aleja", y desbloqueo de
> la siguiente zona. La versión anterior (`DialogueController`, `DialogueUI`,
> `GroomingDialogue`, `DesconocidosDefaultScripts`, `GroomingChatLogger`) quedó
> en `deprecated/Desconocidos/` en la raíz del repo.

## Scripts

| Script | Rol |
|--------|-----|
| `BosqueDesconocidosNPC.cs` | Reacciona cuando el `ChatModuleLauncher` de este NPC cierra su sesión: decide éxito/fracaso según % de respuestas seguras, hace que el NPC "se aleje" si corresponde, y avisa al manager. |
| `BosqueDesconocidosManager.cs` | Lleva la cuenta de los NPCs de la zona; al completarse todos, marca la temática y habilita la siguiente zona del mapa. |

## Montaje de la escena

### 1. NPCs (al menos dos)
Por cada NPC de la temática:
1. Crea un GameObject con el sprite del NPC.
2. Añade un `Collider2D` y márcalo **Is Trigger** (es el radio de cercanía).
3. Añade `ChatModuleLauncher` (HDU-8):
   - `source`: `ZonaDesconocidosPorDefecto` (usa el banco de preguntas oficial)
     o `ConversacionesAsignadas` + tus propios `ChatConversation`.
   - `openOnTriggerEnter`: activado, con `ottoTag = "Player"`.
   - `reportToBackend`: actívalo sólo si hay login + partida en el `ApiManager`.
4. Añade `BosqueDesconocidosNPC` (requiere el `ChatModuleLauncher` del paso 3
   en el mismo GameObject — se agrega solo si falta):
   - `umbralExito`: % mínimo de respuestas seguras para contar como éxito (70 por defecto).
   - Los campos de "al alejarse" (`leaveDistance`, `successMessage`, etc.) igual que antes.

> Otto debe tener el Tag `Player` y un `Rigidbody2D` (ya lo tiene de HDU-5); es lo
> que dispara el trigger de cercanía.

### 2. Gestor de la temática
1. Crea un GameObject con `BosqueDesconocidosManager` (recoge solo los
   `BosqueDesconocidosNPC` de la escena si la lista está vacía).
2. `siguienteZonaId`: el `zoneId` de la `BlockedZone` (HDU-5) que da acceso a la
   siguiente temática. Al completar, se desbloquea automáticamente.
3. `progresoAlCompletar` (opcional): progreso 0-100 que se guarda en la partida.
4. `onTematicaCompletada` (opcional): engancha aquí efectos (sonido, marcador…).

También puedes usar el menú **Fishy → Configurar Zona Desconocidos (manager +
zona bloqueada)**, que arma el manager y la `BlockedZone` conectada de un tiro.

### 3. UI de diálogo y mensajes
No requieren montaje: `ChatModuleUI` y `ZonePopupUI` se crean en runtime.

### 4. EventSystem
Para que los botones de respuesta respondan, la escena necesita un **EventSystem**
(GameObject → UI → Event System). Suele crearse solo al añadir el primer Canvas.

## Crear conversaciones propias (opcional)
`Create → Fishy → Conversación Chat` (ver README de HDU-8 para el detalle del
grafo de nodos y las opciones `Safe`/`Unsafe`/`Neutral`).

## Cobertura de criterios de aceptación
- ✅ Al acercarse a un NPC, este inicia la conversación para ganar confianza
  (halago + info falsa) y empieza a pedir datos discretamente.
- ✅ Si el niño/a mantiene ≥70% de respuestas seguras → el NPC se aleja y se
  marca el éxito (mensaje de felicitación).
- ✅ Si comparte datos de más → el NPC sigue amistoso y pide datos cada vez más
  específicos (nombre → edad/colegio → dirección/rutina → encuentro a solas).
- ✅ Dos NPCs con ≥2 fases que escalan según las decisiones.
- ✅ Al cerrar la última interacción de la temática → se marca completada y se
  habilita la siguiente zona del mapa.
- ✅ Ruta de captura termina con un cierre educativo de seguridad (nodo de
  cierre `isSystem` + `closesChat` en el `ChatConversation`).
