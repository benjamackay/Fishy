# HDU-1 — Misión activa (MissionManager)

Primera pieza de **HDU-01 (Interacción con objetos y NPCs)**: el panel de misión
activa que registra desafíos disponibles y completados (criterios de aceptación
4 y 5). Los objetos interactuables y los NPCs neutros (criterios 1-3, aún por
construir) se apoyarán en este módulo.

## Archivos
- `DesafioData.cs` — ScriptableObject con la ficha de un desafío (id, título,
  descripción, ícono). Se crean assets vía **Assets → Create → Fishy → Mision →
  Nuevo Desafio**.
- `MissionManager.cs` — singleton persistente (`DontDestroyOnLoad`) que lleva el
  registro de desafíos disponibles/completados, dispara eventos y persiste el
  progreso en `PlayerPrefs` como fallback local (no hay endpoints de desafíos en
  el backend todavía, igual que pasó con el modo Detective).
- `MissionPanelUI.cs` — panel "Misiones" que se autoconstruye en runtime (botón
  arriba a la derecha + lista), sin necesitar montaje manual en la escena.
- `Tests/MissionManagerTests.cs` — pruebas PlayMode (NUnit) del MissionManager.

## Montaje en la escena
No es obligatorio montar nada: la primera vez que algo llama a
`MissionManager.GetOrCreate()` o `MissionPanelUI.GetOrCreate()` se crean solos.
Aun así, es más prolijo agregar dos GameObjects vacíos en la escena principal
(por ejemplo dentro de un `Systems` o junto a `ApiManager`):
- Uno con el componente `MissionManager`.
- Otro con el componente `MissionPanelUI`.

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

## Configurar los tests PlayMode
Si el proyecto todavía no tiene una carpeta de tests con Assembly Definition:
1. En `Assets/Scripts/Mision/Tests/`, click derecho → **Create → Testing →
   Assembly Definition**, márcala como **Test Assemblies**.
2. Asegúrate de que referencie el ensamblado donde vive `MissionManager`
   (si los scripts del proyecto no usan asmdefs propios, Unity los incluye en
   `Assembly-CSharp` automáticamente y no hace falta referenciar nada extra).
3. Abre **Window → General → Test Runner → PlayMode** y corre los tests.

## Pendiente (siguientes pasos de HDU-1)
- `InteractableObject` — objetos con contorno/brillo, recolección, y llamado a
  `RegistrarDesafioDisponible` al desbloquear un desafío.
- Acción de "Interactuar" en `OttoController` (tecla + botón en pantalla), y
  detección del interactuable más cercano.
- NPC neutro con diálogo narrativo lineal (pista), distinto del árbol de
  decisiones de `Desconocidos` (HDU-2).
- Persistencia de objetos ya recolectados (mismo patrón PlayerPrefs que aquí).
