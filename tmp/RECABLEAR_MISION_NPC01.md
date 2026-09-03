# Recablear MISION_NPC_01 en la escena

Lo único que quedó fuera de los commits. Hay que hacerlo a mano en Unity
porque el GameObject que llevaba el `MissionGiver` (`NPC`, el Huemul) fue
eliminado de `SampleScene.unity` en el commit *"Edición NPCs"*, y arrastrar
la escena vieja habría borrado los NPCs nuevos y sus animaciones idle.

## Pasos

1. Abrir `SampleScene` en la rama `feat/mision-npc01-wiring` (escena actual de dev).
2. Reponer el NPC Huemul en la escena.
3. En su componente de diálogo, apuntar `dialogueData` a
   `Assets/Sprites/NPCs/huemul/HuemulDialogue.asset`.
4. Añadirle el componente **MissionGiver** (`Assets/Scripts/MisionMundo/MissionGiver.cs`).
5. Rellenar con estos valores exactos:

| Campo | Valor |
|---|---|
| `desafio` | `Assets/Data/Mision/DesafioData.asset` (MISION_NPC_01) |
| `soloUnaVez` | ✅ true |
| `requiereVolverParaEntregar` | ✅ true |
| `zonaADesbloquear` | `ZonaBloqueada_zona_2` → componente `BlockedZone` |
| `usarCinematicaDesbloqueo` | ✅ true |
| `mensajeDesbloqueo` | `GRACIAS GABRIEL GARCIA MARQUEZ` |
| `mensajeMisionPendiente` | `Y DONDE ESTAN LAS FLORES CAUSA?` |

6. `objetivos` — 4 entradas, todas `tipo: 0` (recolectar), `cantidad: 1`:

| # | Objeto |
|---|---|
| 1 | `Assets/Items/flor2.asset` |
| 2 | `Assets/Items/flor1.asset` |
| 3 | `Assets/Items/flor3.asset` |
| 4 | `Assets/Items/brujula.asset` |

7. Había además un GameObject de UI que pasaba a activo
   (`m_IsActive: 0 → 1`, fileID `1796791423` = el panel de diálogo) y un
   `RectTransform` reajustado a `anchoredPosition.x: -43.2962`,
   `sizeDelta.x: 363.4076`. Revisar si siguen haciendo falta en la escena nueva.

## Referencia

La escena vieja completa y el parche están respaldados. Para consultar el
bloque original del `MissionGiver`:

    git show codex/setup-mission-page:"Fishy!/Assets/Scenes/SampleScene.unity"

## Ojo

`ZonaBloqueada_zona_2` sí sobrevive en la escena de dev, así que esa
referencia va a resolver bien. El NPC es lo único que hay que reponer.
