# HDU-16 — Misión activa y zona objetivo

Que el niño/a sepa **qué toca ahora** y **hacia dónde ir**, sin abrir ningún menú
y sin tener que acordarse.

## La idea en una frase

Antes había un conjunto de desafíos, todos iguales, con estado `Disponible` o
`Completado`. Ahora hay **una misión activa** — la disponible de menor `orden` —
y todo lo demás cuelga de ella: el cartel la nombra, la flecha señala su zona, y
al completarla ambas cosas pasan solas a la siguiente.

## Archivos

| Archivo | Qué hace |
|---|---|
| `Mision/MissionManager.cs` | `Activa`, `HayMisionActiva`, `ZonaObjetivoActiva`, `onMisionActivaCambiada`. **Aquí se decide todo.** |
| `Mision/DesafioData.cs` | Campos nuevos `zonaObjetivo` y `orden`. |
| `MisionMundo/MissionUIController.cs` | El cartel permanente arriba a la izquierda (CA1, CA6). |
| `MisionMundo/ZoneMarker.cs` | La flecha que señala la zona (CA2, CA4). Sólo dibuja. |
| `MisionMundo/MisionHudTheme.cs` | Colores, tamaños y textos de los dos. Sólo variables. |
| `MisionMundo/ObjetivoMision.cs` | Tipo de objetivo `LlegarAZona` (CA3). |
| `MisionMundo/MissionTracker.cs` | Escucha el cambio de zona y avisa de cada avance. |
| `Otto/ZonaMundo.cs` | `nombreVisible`, `Centro`, `De(id)`, `NombreDe(id)`. |
| `Mision/Tests/MisionActivaTests.cs` | Pruebas de la misión activa y del interruptor de la flecha. |

## Cómo se reparte el trabajo

```
MissionManager.Activa            ← decide cuál es la misión (assembly Fishy.Mision)
        │
        ├── ZonaObjetivoActiva   ← decide qué zona señalar, o null
        │
        └── onMisionActivaCambiada
                │
                ▼
        MissionUIController      ← pinta el cartel (Assembly-CSharp)
                │
                └── ZoneMarker.Apuntar(zona)   ← dibuja la flecha, no decide nada
```

El corte está donde está por una razón concreta: `Fishy.Mision` tiene su propio
`.asmdef` y **no puede ver Assembly-CSharp** (ni Unity permite lo contrario). El
resumen de objetivos necesita `NPC`, `ItemData` y `PhoneChatLauncher`, así que la
UI tiene que vivir del lado de Assembly-CSharp. A cambio, la parte que sí se
puede probar sin escena ni cámara —qué misión es la activa y qué zona señalar—
está entera en `MissionManager`, y es lo que cubren los tests.

## Configurar una misión

En la ficha del desafío (`Assets → Create → Fishy → Mision → Nuevo Desafio`):

- **`orden`** — el lugar en la historia. Menor va antes. Deja huecos (10, 20,
  30…) para poder intercalar una misión nueva sin renumerar las demás.
  > Cuidado: una ficha creada **antes** de HDU-16 trae `orden: 0` al abrirla,
  > porque el campo no estaba en el `.asset` y Unity rellena con el valor por
  > defecto del tipo, no con el del código. Con todas en 0 el desempate vuelve a
  > ser alfabético. Revisa las fichas viejas.
- **`zonaObjetivo`** — id de la zona a la que hay que ir (`zona_1`, `zona_2`,
  `zona_3`). Vacío = esta misión no dibuja flecha.

Y para un objetivo que literalmente sea llegar a un sitio, en el `MissionGiver`
del NPC: tipo **Llegar A Zona** + el id en `zonaDestino`. Si lo usas, puedes
dejar `zonaObjetivo` vacío: el cartel y la flecha caen en el objetivo pendiente.

## Cableado que hay que hacer en el editor

El cartel, la flecha, `ZonaActual` y `MissionTracker` se crean solos. Lo que **no**
se puede crear solo es la geometría del mapa:

1. **`ZonaMundo` en cada zona.** Ponlo en el mismo GameObject que la
   `BlockedZone` (`ZonaBloqueada_zona_2` y `ZonaBloqueada_zona_3`) y deja `areas`
   vacío: toma sus polígonos. Para `zona_1`, que no se bloquea nunca, hace falta
   un objeto propio con su `PolygonCollider2D`.
   - Rellena `nombreVisible` ("La Playa", "El Bosque"): es lo que lee el niño/a
     en el cartel y bajo la flecha. Sin él sale `zona_2`.
   - **Sin esto CA3 no funciona**: `ZonaActual` no puede saber dónde está Otto si
     ninguna zona sabe contenerlo.
2. **Un `PuntoDeAparicion` por zona**, marcado `porDefecto`. Es a dónde apunta la
   flecha. Si falta, apunta al centro del polígono, que en una zona con forma de
   L puede caer en el agua.
3. **`orden` y `zonaObjetivo`** en las fichas de `Resources/Misiones/`.

## Por qué la flecha orbita alrededor de Otto

No se pega al borde de la pantalla: se coloca en la línea que va de Otto al
destino, a la distancia que haya, con un tope de 190 px. Es una sola fórmula que
vale igual con el destino dentro y fuera de la pantalla, y deja la guía cerca de
donde el niño/a ya está mirando. Mientras está **dentro** de la zona de destino
la flecha se esconde —señalar el sitio donde ya estás es ruido—, pero el marcador
sigue de servicio: lo que falta hacer ahí lo cuenta el cartel.

## Deuda conocida que toca esta HDU

- **`DesafioData 1.asset` y `DesafioData 2.asset` comparten el id
  `MISION_NPC_02`.** `CatalogoDesafios` descarta el segundo con un `LogError`, y
  el `Neutral_NPC (2)` que lo entrega pierde sus objetivos en silencio. Mientras
  siga así, "cuál es la siguiente misión" se calcula sobre una misión menos de
  las que hay. Se dejó a propósito para no mover ids que ya están en la base.
- **La zona objetivo no se persiste.** El backend no tiene columna `zona_actual`
  (`models.py:147`) y `SaveManager` deja la zona en `ZonaGuardada` sin subirla.
  No hace falta para esta HDU —la misión activa se recalcula sola al restaurar el
  progreso—, pero si algún día se quiere reabrir el juego con la flecha ya
  puesta antes de que Otto se mueva, hay que agregar la columna.
- La página del Tab sigue usando `✔` y `•`, que no están en las fuentes del
  juego (ver la nota de `FishyUIKit.Aspa`). El cartel nuevo no los usa.
