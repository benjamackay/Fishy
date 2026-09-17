# HDU-1 — Misión activa (MissionManager)

Primera pieza de **HDU-01 (Interacción con objetos y NPCs)**: el panel de misión
activa que registra desafíos disponibles y completados (criterios de aceptación
4 y 5). Los objetos interactuables y los NPCs neutros (criterios 1-3, aún por
construir) se apoyarán en este módulo.

## Archivos
- `DesafioData.cs` — ScriptableObject con la ficha de un desafío (id, título,
  descripción, ícono, y desde HDU-16 también `zonaObjetivo` y `orden`). Se crean
  assets vía **Assets → Create → Fishy → Mision → Nuevo Desafio**.
- `MissionManager.cs` — singleton persistente (`DontDestroyOnLoad`) que lleva el
  registro de desafíos disponibles/completados y dispara eventos. Guarda en
  `PlayerPrefs` como respaldo local (sin sesión o en modo local), pero el
  progreso real ya sube al backend: `MisionBackendSync.cs` (en
  `Assets/Scripts/`, fuera de esta carpeta porque hace de puente entre este
  ensamblado y `ApiManager`) baja lo completado al empezar la partida y sube
  cada cambio por la cola de guardado — ver `README_GUARDADO.md`.
- `Tests/MissionManagerTests.cs` — pruebas PlayMode (NUnit) del MissionManager.
- `Tests/MisionActivaTests.cs` — pruebas PlayMode de la misión activa (HDU-16).

`MissionPanelUI.cs`, el panel desplegable con el botón "Misiones" que era la UI
de esta HDU, está en `deprecated/Misiones/`: HDU-16 lo reemplazó por
`Mision/MissionUIController.cs`, un cartel permanente que no hay que abrir.
La lista completa de misiones vive en la pestaña Misión del Tab (`QuestPageUI`).

## Montaje en la escena
No hay que montar nada: `MissionManager.GetOrCreate()` lo crea la primera vez que
alguien lo pide, y el cartel de misión activa se crea solo al cargar una escena
que tenga a Otto. Aun así, es más prolijo tener un GameObject con el componente
`MissionManager` en la escena principal (por ejemplo junto a `ApiManager`).

## Uso desde un objeto/NPC interactuable (próximos pasos de HDU-1)
```csharp
// Al finalizar la interacción que desbloquea un desafío (criterio 4):
MissionManager.Instance.RegistrarDesafioDisponible(miDesafioData);

// Cuando el niño/a termina ese desafío (criterio 5):
MissionManager.Instance.CompletarDesafio(miDesafioData.desafioId);
// o bien: MissionManager.Instance.CompletarDesafio(miDesafioData);
```

Eventos disponibles para engancharse (analíticas, sonidos, cinemáticas, etc.):
```csharp
MissionManager.Instance.onDesafioDisponible.AddListener(desafio => { ... });
MissionManager.Instance.onDesafioCompletado.AddListener(desafio => { ... });
```

## Correr los tests PlayMode
Ya está montado: `Mision/Nucleo/Tests/` tiene su propio Assembly Definition
(`Fishy.Mision.Tests`, marcado **Test Assemblies**), que referencia a
`Fishy.Mision` (donde vive `MissionManager`). Para correrlos, **Window →
General → Test Runner → PlayMode**.

También se pueden correr en modo headless, sin abrir el editor — ver
`README_GUARDADO.md`, sección "Cómo comprobarlo", para el comando de
`-executeMethod` y los arneses (`FishyPruebasCola`, `FishyPruebasPartida`).

## Ya construido (esto era "pendiente" cuando se escribió este documento)

Los cuatro próximos pasos que este documento planteaba ya están hechos, con
nombres distintos a los que se previeron acá:

- **Objetos interactuables**: no existe `InteractableObject`; la pieza real es
  `IInteractable.cs` (interfaz) + `InteractionDetector.cs` (detecta el
  interactuable más cercano) + `Inventario/WorldItem.cs` (la recolección).
- **Acción de "Interactuar"**: la resuelve `InteractionDetector.cs`, no
  `OttoController` directamente.
- **NPC neutro con diálogo lineal**: `NPC.cs` + `DialogoNpcLoader.cs`, que lee
  la sección `dialogos_npc_neutros` del banco de preguntas — distinto del
  árbol de decisiones de `Desconocidos` (HDU-2), tal como se planeó acá.
- **Persistencia de objetos recogidos**: ya no es solo `PlayerPrefs`; sube al
  backend por `ObjetosRecogidosSync.cs` — ver `README_GUARDADO.md`.
