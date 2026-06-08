# HDU-5 — Control y exploración libre con Otto

Implementación del movimiento de **Otto (el chungungo)** en cuatro direcciones,
con caminar/correr/saltar, zonas bloqueadas oscurecidas y mensaje emergente.

## Scripts

| Script | Rol |
|--------|-----|
| `OttoController.cs` | Movimiento 4 direcciones (WASD/flechas + botones en pantalla), caminar/correr/saltar, parada inmediata al soltar. |
| `BlockedZone.cs` | Zona/límite bloqueado: collider que detiene a Otto, oscurecido visual y mensaje emergente. |
| `ZonePopupUI.cs` | Cartel emergente «Esta zona aún está cerrada…». Se autogenera si no se monta en la escena. |
| `OttoOnScreenButton.cs` | Botón direccional/correr/saltar para táctil. |
| `WorldZoneManager.cs` | Desbloquea zonas según misiones / progreso de la partida. |

## Montaje rápido de la escena

### 1. Otto (jugador)
1. Crea un GameObject (o usa el sprite de Otto) y asígnale el **Tag `Player`**.
2. Añade `Rigidbody2D`:
   - **Body Type:** `Dynamic`
   - **Gravity Scale:** `0` (lo fuerza el script igualmente)
   - **Freeze Rotation Z:** activado (lo fuerza el script).
3. Añade un `Collider2D` (p.ej. `CapsuleCollider2D`) ajustado al cuerpo de Otto.
4. Añade el componente **`OttoController`**.
   - Pon el sprite de Otto como **hijo** y arrástralo a `Sprite Root` (sube/baja al saltar).
   - `Spawn Point`: opcional, un Transform en la zona de inicio.
   - Velocidades: `walkSpeed` / `runSpeed` a gusto.

> Al iniciar la partida (Start) Otto aparece en la zona de inicio y se habilita el
> movimiento. Si la carga del mundo es asíncrona, desactiva `enableOnStart` y llama
> a `otto.EnableMovement()` cuando el mundo termine de cargar.

### 2. Límites y zonas bloqueadas
Para cada zona/borde que deba bloquearse:
1. Crea un GameObject con uno o más `Collider2D` **sólidos** (NO triggers) cubriendo
   el área prohibida o el muro perimetral del mapa.
2. Añade **`BlockedZone`**:
   - `zoneId`: clave única (p.ej. `playa_norte`).
   - `isLocked`: marcado.
   - `overlay`: un `SpriteRenderer` oscuro encima de la zona (opcional, da el efecto
     "oscurecido"). El script ajusta su alpha (`darkenAlpha`).
   - `mensajeBloqueo`: ya trae el texto pedido por la HDU.

Cuando Otto choca contra la zona bloqueada se detiene en el límite (física) y
aparece el mensaje. Al soltar la tecla, se detiene de inmediato.

### 3. Mensaje emergente
No necesitas montar nada: la primera vez que se llama se autocrea un Canvas con el
cartel. Si quieres un diseño propio, crea un GameObject con `ZonePopupUI` y asigna
`panel` (CanvasGroup) y `label` (Text).

### 4. Controles en pantalla (táctil) — opcional
1. Asegúrate de que la escena tenga un **EventSystem** (GameObject → UI → Event System).
2. Crea botones en un Canvas y añade `OttoOnScreenButton` a cada uno, eligiendo
   `action` (Up/Down/Left/Right/Run/Jump). Enlaza `otto` (o se busca solo).

### 5. Desbloqueo por progreso/misiones — opcional
1. Añade `WorldZoneManager` a un GameObject (recoge solas las `BlockedZone` si la
   lista está vacía).
2. Define `progresoRequerido` por zona, o desbloquea manualmente:
   ```csharp
   WorldZoneManager.Instance.UnlockZone("playa_norte");
   // o, tras actualizar la partida en el backend:
   WorldZoneManager.Instance.SetProgress(partida.progreso);
   ```

## Controles
- **Mover:** `WASD` o **flechas** (también stick/d-pad de mando).
- **Correr:** mantener `Shift`.
- **Saltar:** `Espacio`.
- **Táctil:** botones en pantalla (`OttoOnScreenButton`).

## Cobertura de criterios de aceptación
- ✅ Otto aparece en la zona de inicio y se habilita el movimiento al cargar el mundo.
- ✅ Desplazamiento continuo en 4 direcciones mientras se mantiene la tecla/botón.
- ✅ Se detiene en el límite de la zona accesible (colliders); no avanza al área oscurecida.
- ✅ Al soltar, se detiene de inmediato en la posición actual.
- ✅ Al empujar hacia una zona bloqueada aparece el mensaje:
  «Esta zona aún está cerrada. Completa más misiones para abrirla.»
