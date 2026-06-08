# HDU-2 — Zona 1: Tema Desconocidos (manipulación / grooming)

Sistema de NPCs con guiones de **manipulación progresiva**: ganan confianza,
intercalan temas neutros (discreto) y piden datos personales cada vez más
específicos, escalando hasta proponer un secreto o encuentro a solas.

Se apoya en HDU-5 (movimiento de Otto, zonas, desbloqueo) y en el `ApiManager`
existente (registro `grooming` en el backend, opcional).

## Scripts

| Script | Rol |
|--------|-----|
| `GroomingDialogue.cs` | Datos del guion (grafo de nodos + fases) como ScriptableObject. |
| `DesconocidosDefaultScripts.cs` | Dos guiones listos: **Alex** y **Sam** (sin crear assets). |
| `DesconocidosNPC.cs` | NPC: detecta cercanía de Otto, inicia el diálogo, "se aleja" en caso de éxito. |
| `DialogueController.cs` | Lógica de la conversación (recorre nodos, aplica reglas). |
| `DialogueUI.cs` | Panel de diálogo + botones de respuesta (se autogenera). |
| `GroomingChatLogger.cs` | Registro best-effort en el backend (start/request/chain/end). |
| `ZonaDesconocidosManager.cs` | Marca la temática completada y habilita la siguiente zona. |

## Montaje de la escena

### 1. NPCs (al menos dos)
Por cada NPC de la temática:
1. Crea un GameObject con el sprite del NPC.
2. Añade un `Collider2D` y márcalo **Is Trigger** (es el radio de cercanía).
3. Añade `DesconocidosNPC`:
   - `scriptSource`: **Alex** en uno, **Sam** en otro (o `Custom` + tu `GroomingDialogue`).
   - `reportToBackend`: actívalo sólo si hay login + partida en el `ApiManager`.

> Otto debe tener el Tag `Player` y un `Rigidbody2D` (ya lo tiene de HDU-5); es lo
> que dispara el trigger de cercanía.

### 2. Gestor de la temática
1. Crea un GameObject con `ZonaDesconocidosManager` (recoge solo los NPCs si la
   lista está vacía).
2. `siguienteZonaId`: el `zoneId` de la `BlockedZone` (HDU-5) que da acceso a la
   siguiente temática. Al completar, se desbloquea automáticamente.
3. `progresoAlCompletar` (opcional): progreso 0-100 que se guarda en la partida.
4. `onTematicaCompletada` (opcional): engancha aquí efectos (sonido, marcador…).

### 3. UI de diálogo y mensajes
No requieren montaje: `DialogueUI` y `ZonePopupUI` se crean en runtime. Si quieres
un diseño propio, crea los GameObjects y asigna sus referencias en el inspector.

### 4. EventSystem
Para que los botones de respuesta respondan, la escena necesita un **EventSystem**
(GameObject → UI → Event System). Suele crearse solo al añadir el primer Canvas.

## Crear guiones propios (opcional)
`Create → Fishy → Diálogo Desconocidos`. Define:
- `startNodeId` y la lista de `nodes` (cada uno con `id`, `npcLine`, `kind` y `choices`).
- En cada `choice`: `quality` (Buena/Neutral/Mala), `sharesPersonalData` y `nextNodeId`.
- Usa nodos `EndSuccess` (el niño/a no compartió → éxito) y `EndCaptured` (compartió
  todo → cierre educativo).

## Cobertura de criterios de aceptación
- ✅ Al acercarse a un NPC, este inicia la conversación para ganar confianza
  (halago + info falsa) y empieza a pedir datos discretamente.
- ✅ Si el niño/a **no** comparte datos → el NPC se aleja y se marca el éxito
  (mensaje de felicitación).
- ✅ Si **comparte** → el NPC sigue amistoso y pide datos cada vez más específicos
  (nombre → edad/colegio → dirección/rutina → encuentro a solas).
- ✅ Dos NPCs con ≥2 fases que escalan según las decisiones.
- ✅ Al cerrar la última interacción de la temática → se marca completada y se
  habilita la siguiente zona del mapa.
- ✅ Ruta de captura termina con un cierre educativo de seguridad.
