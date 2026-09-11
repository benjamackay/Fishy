# Traspaso — Sistema de misiones y su respaldo en la BD (Fishy)

Fecha: 2026-09-10 · Para retomar en frío.

## Estado del repositorio

- Repo: `/home/black/Documentos/Fishy` · Unity en `Fishy!/` · Django en `Backend/`
- Rama: `feature/guardado-unity-model` · HEAD `bb5ad62`
- **Árbol sucio, y casi nada de eso es de este traspaso.** El usuario está trabajando
  en paralelo en el editor (chat, NPC, detective, `BancoBackendSync.cs` nuevo,
  `FondoAleatorio` renombrado…). Lo único que salió de esta sesión es:
  - `REQUISITOS_BD_MISIONES.md` (nuevo, sin commitear)
  - este archivo
- **No se commiteó nada.** Tampoco se tocó ninguna ficha de misión ni ningún script:
  la sesión fue de verificación y documentación, por decisión explícita del usuario.

## Lo importante en una frase

La BD **sí tiene las 9 misiones** del banco, pero el juego **no puede leerlas**, y
**los objetivos no existen en la BD en absoluto**. El compañero del usuario va a
montar el lado de la base; la especificación de qué debe guardar ya está escrita y
es el entregable de esta sesión.

## Dónde está el entregable

1. `REQUISITOS_BD_MISIONES.md` en la raíz del repo (fuente, versionable).
2. Publicado como página para compartir con el equipo:
   https://claude.ai/code/artifact/7213462b-de17-448b-b238-5c6601248594
   (mismo contenido; para actualizarla, republicar pasando esa `url`).

Dice **qué** guardar, no **cómo** — el usuario pidió explícitamente no especificar
modelos, columnas ni endpoints. Si hay que editarlo, respetar eso.

## Hechos verificados (no re-descubrir)

Todo esto se comprobó en esta sesión; con el comando o el archivo al lado para poder
repetirlo.

**La BD tiene 9 misiones.** `api_mision` en `Backend/backend/local_db.sqlite3`:
3 de exploración (`MISION_EXPLORACION_01/02/03`) y 6 secundarias (`MISION_SEC_*`),
con `mision_id`, `nombre`, `tipo`, `zona`. Las carga
`manage.py cargar_banco` desde `banco_preguntas/banco_preguntas.json`.
`api_misionprogreso` está **vacío**: nunca se subió progreso.

**Supabase no se pudo verificar.** `DB_PASSWORD` en `Backend/.env` está vacía. Lo de
arriba vale para la SQLite local, que es contra la que se puede correr hoy
(`./run.sh --local`, y `settings_local_sqlite.py` es nuevo y aún sin commitear).

**Los objetivos no existen en el backend.** `grep -rin objetivo Backend/backend/api/*.py`
→ cero resultados. Ni modelo, ni columna, ni endpoint. Tampoco están en el JSON del
banco: una misión ahí es un `mision_id` + `nombre_mision` colgando de un diálogo, y
el objetivo está contado en prosa. La de Coipo es literalmente una línea: *"mi
pequeña mascota piedra desapareció cerca de los juncos"*.
Hoy los objetivos viven **sólo** en el Inspector, en `MissionGiver.objetivos`
(`List<ObjetivoMision>`), y es razonable que estén ahí: apuntan a `NPC`, `ItemData` y
`PhoneChatLauncher`, que son objetos de escena.

**No hay endpoint de catálogo de misiones.** `api/urls.py` tiene una sola ruta de
misiones: `partidas/<int:partida_id>/misiones/` (línea 29), y devuelve
`MisionProgreso` — o sea, lo que el juego ya subió. El juego no puede *descubrir*
misiones. Sí hay rutas de catálogo para banco, detective y diálogos NPC; para
misiones no.

**Los ids no calzan, intersección vacía.** Las fichas de Unity en
`Fishy!/Assets/Resources/Misiones/` son `MISION_NPC_01` y `MISION_NPC_02`
(**duplicado en dos assets**, `CatalogoDesafios` descarta el segundo con `LogError`).
Ninguno existe en la BD. Si se bajara progreso, `PrecargarConocidos` llamaría a
`CatalogoDesafios.Buscar("MISION_SEC_MASCOTA_COIPO")`, no encontraría ficha y
descartaría la misión con un warning.

**El puente existe en los datos y nadie lo cruza.** `NPC.dialogoId` ↔
`dialogos_npc_neutros[].mision_desbloquea` ↔ `api_dialogonpc.mision_id`. El campo
está declarado en `BancoPreguntasData.cs:43` y **no lo lee ningún script** — se
re-verificó al final de la sesión, después de que el usuario tocara `NPC.cs` y
`DialogoNpcLoader.cs`, y sigue igual.

**Los dos bloques del banco usan espacios de nombres distintos.** Esto importa
mucho y es contraintuitivo:
- `dialogos_npc_neutros` → se identifica por `dialogo_id` (`HDU1_SEC_COIPO_MASCOTA`).
  npc_ids: `NPC_COIPO`, `NPC_PUDU`, `NPC_FLAMENCO_SEC`, `NPC_GUIA`, `NPC_GUIA_2`, …
- `preguntas` → se identifica por `escenario_id` (`M1_CHAT01`, `M4_FASE01`). **Ahí no
  existen los `dialogo_id`.** npc_ids: `NPC_01`…`NPC_09`, `NPC_GUIA*`, `NPC_TESTIMONIOS`.
- El mismo animal tiene ids distintos por bloque: Flamenco es `NPC_FLAMENCO_SEC` en
  uno y `NPC_03` en el otro.
- **`NPC_GUIA`, `NPC_GUIA_2` y `NPC_GUIA_3` aparecen en los DOS bloques** señalando
  conversaciones diferentes. Guardar "un id de NPC" sin decir de qué bloque sale es
  ambiguo.
- Para el chat, el criterio preciso es `escenario_id`, **no** `npc_id`: dentro de
  `preguntas` un mismo `npc_id` se repite entre conversaciones (`NPC_01` y `NPC_02`
  son los dos "Puma"), y el propio `PhoneChatLauncher` lo documenta recomendando
  `SoloEscenario`.

**El avance dentro de una misión se pierde al cerrar el juego, salvo en un caso.**
`ObjetivoMision.cumplido` es `[NonSerialized]`; sólo persiste el estado de la misión
completa. Medido categoría por categoría:

| Categoría | ¿Sobrevive? | Por qué |
|---|---|---|
| Recoger objeto | **Sí** | `Evaluar()` lo recalcula del inventario, que sí se guarda |
| Hablar con NPC | No | Se cumple por `onDialogueEnded`; no queda registro |
| Chatear por celular | No | Igual, por `onChatClosed` |
| Llegar a zona | No | Se recalcula de `ZonaActual.Instance.Actual`: si ya salió, se pierde |

**El modo detective NO es una categoría de objetivo.** `TipoObjetivo` tiene cuatro:
`RecogerObjeto`, `HablarConNpc`, `ChatearPorTelefono`, `LlegarAZona`.
`DetectiveLauncher` va por su lado. El usuario preguntó por esto; se le dijo que
añadirlo es tocar código del juego, no sólo datos, y quedó **sin decidir**.

## Decisiones que ya tomó el usuario (no volver a preguntar)

1. **El orden de la historia se define con un campo `orden` numérico** en la ficha,
   menor va antes. Ya está implementado en Unity (`DesafioData.orden`) y es lo que
   usa `MissionManager.Activa`.
2. **Manda el vocabulario de zona espacial** (`zona_1`, `zona_2`, `zona_3`) sobre el
   temático (`desconocidos`, `ciberacoso`, `reto_viral`). El espacial responde a
   "¿dónde?" y es el que se guarda para señalar destino y detectar llegada; el
   temático se queda como etiqueta de contenido. Ojo: el campo `zona` que ya existe
   en `api_mision` es del tipo temático.
3. **La descripción NO se guarda en la BD.** El campo sigue existiendo en Unity
   (`DesafioData.descripcion`, lo usa el HUD cuando una misión no tiene objetivos que
   listar), pero queda fuera de la especificación. No borrarlo de Unity.
4. **"Hablar con NPC" y "chatear por celular" siguen siendo dos categorías**, tras
   verificar que no comparten identificador (ver arriba).
5. **El id duplicado `MISION_NPC_02` se deja como está** por ahora.
6. **No se reemplazan las fichas todavía**: se espera a que el compañero termine la
   BD, y *después* se rellena con las 9 misiones reales.

## Lo que sigue

**Bloqueado esperando al compañero del usuario**, que está montando el lado de la
base según `REQUISITOS_BD_MISIONES.md`.

Cuando termine, lo acordado es:
1. Rellenar la BD con las 9 misiones reales (y sus objetivos, que hay que **escribir
   como contenido nuevo** — el banco no los trae).
2. Crear las 9 fichas `DesafioData` del lado de Unity con los `mision_id` del banco,
   y jubilar `MISION_NPC_01` / `MISION_NPC_02`.
3. Re-apuntar los `MissionGiver` de las escenas a las fichas nuevas.

Si el usuario pide empezar el paso 2, ojo con `tmp/RECABLEAR_MISION_NPC01.md`: es una
nota previa con el cableado manual pendiente del Huemul y sus 4 objetivos de
recolección. Está escrita contra `Assets/Data/Mision/DesafioData.asset`, ruta que hoy
**no** es donde viven las fichas (`Assets/Resources/Misiones/`), así que puede estar
desactualizada — verificar antes de seguirla al pie de la letra.

## Operativa útil

**Correr los tests de Unity sin abrir el editor** (funciona, 21/21 pasaban al cerrar
HDU-16):

```bash
~/Unity/Hub/Editor/6000.4.9f1/Editor/Unity -batchmode -nographics \
  -projectPath "/home/black/Documentos/Fishy/Fishy!" \
  -runTests -testPlatform PlayMode \
  -testResults /ruta/results.xml -logFile /ruta/unity.log
```

Dos trampas con esto:
- Si `-testResults` se ignora, el XML aparece en
  `~/.config/unity3d/Fishy!/Fishy!/TestResults.xml`.
- Un test asmdef con `includePlatforms: ["Editor"]` es **EditMode**, y una corrida
  `-testPlatform PlayMode` devuelve `testcasecount="0"` **sin quejarse**. Ya se
  arregló en `Fishy.Mision.Tests.asmdef` (ahora `[]`), pero si aparece otro asmdef de
  tests, mirar esto primero.
- Cada corrida reescribe un espacio en blanco en
  `ProjectSettings/ProjectSettings.asset`. Es ruido, conviene revertirlo.

**Mirar la BD local sin Django**:
```bash
python3 -c "import sqlite3;c=sqlite3.connect('Backend/backend/local_db.sqlite3');
print([dict(zip([d[0] for d in c.execute('select * from api_mision').description], r))
       for r in c.execute('select * from api_mision')])"
```

## Convenciones del proyecto (siguen vigentes)

- Comentarios, nombres y documentación **en español**.
- Código grande que se borra va a `deprecated/` en la raíz con `git mv`, nunca
  `git rm` (y se mueve también el `.meta`).
- Temas visuales = **solo variables, cero métodos** (`UI/FishyUIKit.cs` con `Paleta`,
  `Menu/MenuTabsTheme.cs`, `MisionMundo/MisionHudTheme.cs`, …).
- **Las fuentes del juego son Latin-1 y poco más.** `✔`, `•`, `…`, `—`, `◆` salen como
  cuadritos huecos. Está documentado en `FishyUIKit.Aspa`. Para texto en pantalla,
  usar `·` y palabras. (`QuestPageUI` todavía usa `✔` y `•`; es deuda conocida.)
- El enum `TipoObjetivo` se serializa **por índice**: un valor nuevo va siempre al
  final, o recablea en silencio todos los objetivos ya configurados.
- Los commits terminan con `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- Unity 6000.4.9f1 · 2D URP · **solo New Input System** (`Keyboard.current`).

## Contexto de asambleas (condiciona dónde va el código nuevo)

```
Fishy.Mision (asmdef propio)      Assembly-CSharp (todo lo demás)
├── DesafioData.cs                ├── MisionMundo/*  (objetivos, HUD, ZoneMarker,
├── MissionManager.cs             │                   disparadores, QuestPageUI)
├── CatalogoDesafios.cs           ├── Otto/*  (ZonaActual, ZonaMundo, BlockedZone)
└── Tests/                        └── MisionBackendSync.cs  (el puente con la API)
```

`Fishy.Mision` **no puede ver Assembly-CSharp** (Unity no permite referenciar el
ensamblado por defecto desde un asmdef), y por eso `MisionBackendSync` existe: vive
del lado que ve a los dos y se engancha por eventos. Cualquier cosa que necesite
objetivos *y* misiones a la vez va en Assembly-CSharp.
