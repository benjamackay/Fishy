# Misiones — cómo está repartida esta carpeta

Todo lo de misiones vive aquí. Antes estaba en dos carpetas hermanas (`Mision/`
y `MisionMundo/`) y no se entendía por qué; ahora es una sola, con el motivo a
la vista.

```
Mision/
├── Nucleo/                    ← ensamblado Fishy.Mision (su propio .asmdef)
│   ├── DesafioData.cs         ficha de una misión
│   ├── MissionManager.cs      quién está activa, qué está completado
│   ├── CatalogoDesafios.cs    fichas por id
│   ├── CatalogoMisiones.cs    el catálogo: base de datos + archivo de respaldo
│   └── Tests/                 ensamblado Fishy.Mision.Tests (38 pruebas)
│
└── *.cs                       ← Assembly-CSharp: el pegamento con el mundo
    MissionGiver, ObjetivoMision, MissionTracker, MisionInicial,
    MissionUIController, ZoneMarker, QuestPageUI, los disparadores…
```

## Por qué `Nucleo/` es una subcarpeta y no se puede aplanar

Un `.asmdef` manda sobre **su carpeta y todas las de debajo**, salvo que una
subcarpeta traiga el suyo. Eso es lo único que permite que las dos mitades
convivan en un mismo sitio: la raíz no tiene asmdef, así que sus scripts caen en
`Assembly-CSharp`; `Nucleo/` sí lo tiene, así que forma un ensamblado aparte.

Si alguien intenta juntarlas en un solo ensamblado se va a encontrar con esto:

**Subir la raíz a `Fishy.Mision` — imposible.** Los 17 archivos de la raíz
dependen de tipos de Assembly-CSharp: `NPC`, `ItemData`, `PhoneChatLauncher`,
`ZonaActual`, `CatalogoItems`, `DetectiveLauncher`, `OttoController`… y **Unity
no permite que un ensamblado con asmdef referencie a Assembly-CSharp**, sólo al
revés.

**Bajar `Nucleo/` a Assembly-CSharp — compila, pero mata las pruebas.**
`Fishy.Mision.Tests` referencia a `Fishy.Mision`; si ese ensamblado desaparece,
el de pruebas se queda sin nada que referenciar, y tampoco puede apuntar a
Assembly-CSharp. Son 38 pruebas. La única salida sería encender
`playModeTestRunnerEnabled` en ProjectSettings, que mete el framework de pruebas
en las compilaciones de verdad: mal negocio por ordenar carpetas.

**Poner asmdefs a todo — hay un ciclo.** `ObjetivoMision` (raíz) usa
`DetectiveLauncher` y `PhoneChatLauncher`, mientras que `DetectiveLauncher` y
`ChatModuleLauncher` usan `Fishy.Mision`. Convertir esas carpetas en ensamblados
dejaría misión → detective → misión, y Unity no admite ciclos. Habría que
romperlos antes con eventos o interfaces.

## Lo que se gana con el corte

Lo de `Nucleo/` no toca escena, ni cámara, ni NPCs: sólo texto, números y
ScriptableObjects. Por eso **se puede probar sin abrir una escena**, y por eso
las decisiones importantes viven ahí: cuál es la misión activa
(`MissionManager.Activa`), qué zona hay que señalar (`ZonaObjetivoActiva`), qué
misiones existen (`CatalogoMisiones`). La raíz sólo obedece y dibuja.

Cuando escribas algo nuevo, la pregunta es esa: **¿necesita ver la escena?**
Si no, va a `Nucleo/` y se puede probar. Si sí, va a la raíz.

## Los otros documentos

- `README_HDU-1.md` — el panel de misión activa, de donde salió todo esto.
- `README_HDU-16.md` — misión activa y zona objetivo: el cartel y la flecha.
- `README_CATALOGO_MISIONES.md` — el catálogo, el respaldo en archivo y las
  cinco categorías de objetivo.
