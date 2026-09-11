# Catálogo de misiones y su respaldo en archivo

De dónde saca el juego **qué misiones existen y qué pide cada una**, y qué pasa
cuando la base de datos no contesta.

## Las dos fuentes

```
Arranque                                   Cuando llegue la base
────────                                   ─────────────────────
Resources/misiones.json  ──▶ CatalogoMisiones  ◀──  GET /misiones/
   síncrono, nunca falla         (memoria)          MisionCatalogoSync
```

1. **Al arrancar** se lee `Resources/misiones.json`, empaquetado con el juego. Es
   síncrono: desde el primer frame hay catálogo, sin red y sin sesión.
2. **Si más tarde responde la base**, reemplaza lo que hubiera y avisa por
   `CatalogoMisiones.OnCatalogoCambiado`. Los `MissionGiver` que todavía no
   entregaron su misión se vuelven a resolver solos.

Si no hay red, no hay sesión, se juega en modo local o el endpoint todavía no
existe, **no pasa nada**: se sigue con el archivo. Ése es el respaldo.

Es el mismo reparto que ya hacían el banco de preguntas (`BancoPreguntasLoader` +
`BancoBackendSync`) y los casos del Modo Detective, y por la misma razón: esperar
a la red para poder empezar a jugar convierte un problema de conexión en un juego
roto.

> **Esto es catálogo, no progreso.** Qué misiones lleva hechas un niño/a concreto
> sigue siendo cosa de `MissionManager` y `MisionBackendSync`, y va por su lado.

## Archivos

| Archivo | Qué hace |
|---|---|
| `Mision/CatalogoMisiones.cs` | El catálogo en memoria, las dos fuentes y la fábrica de fichas |
| `Mision/CatalogoDesafios.cs` | `Registrar` / `Olvidar`, para las fichas que no son assets |
| `MisionMundo/MisionCatalogoSync.cs` | Espera a que haya sesión y baja el catálogo |
| `MisionMundo/ObjetivoMision.cs` | `DesdeRegistro` y `Resolver`: de identificadores a objetos de la escena |
| `MisionMundo/MissionGiver.cs` | Toma ficha y objetivos del catálogo si no están en el Inspector |
| `MisionMundo/MisionInicial.cs` | Entrega una misión al empezar, sin NPC que la dé |
| `Resources/misiones.json` | El respaldo |
| `Mision/Tests/CatalogoMisionesTests.cs` | Pruebas del catálogo y del respaldo |

## Formato del archivo

Los mismos campos que se esperan de `GET /misiones/`, para que un solo lector
sirva a las dos fuentes.

```json
{
  "version": "1",
  "misiones": [
    {
      "mision_id": "MISION_SEC_MASCOTA_COIPO",
      "titulo": "¿Dónde está la mascota?",
      "tipo": "secundaria",
      "zona": "ciberacoso",
      "zona_objetivo": "zona_2",
      "orden": 50,
      "objetivos": [
        { "orden": 1, "tipo": "hablar_npc",       "dialogo_id": "HDU1_SEC_COIPO_MASCOTA" },
        { "orden": 2, "tipo": "recoger_objeto",   "item_id": "ITEM_PIEDRA", "cantidad": 1 },
        { "orden": 3, "tipo": "llegar_zona",      "zona_id": "zona_2" },
        { "orden": 4, "tipo": "chatear_telefono", "escenario_ids": "M4_FASE01,M4_FASE02" }
      ]
    }
  ]
}
```

**`titulo` también se acepta como `nombre`.** La tabla `Mision` que ya existe en el
backend llama `nombre` a ese campo. Se leen los dos, porque el campo que no venga
lo deja vacío el parser sin quejarse, y una misión sin título sale en blanco en el
cartel sin que nadie se entere. Si llega un catálogo entero sin títulos, se avisa
con un error en consola nombrando los campos que se esperaban.

**Las dos zonas no son lo mismo:**
- `zona` es la **temática** (`desconocidos`, `ciberacoso`, `reto_viral`): de qué
  trata el contenido.
- `zona_objetivo` y `zona_id` son la **espacial** (`zona_1`, `zona_2`, `zona_3`):
  un sitio del mapa. Es la que vale para señalar destino y detectar llegada.

Hoy el mapeo entre ambas, sacado de la escena, es
`desconocidos → zona_1`, `ciberacoso → zona_2` (Pantano de los susurros),
`reto_viral → zona_3` (Arrecife de los desafíos).

## Las cinco categorías de objetivo

| `tipo` | Campos que usa | Cómo se resuelve contra la escena |
|---|---|---|
| `recoger_objeto` | `item_id`, `cantidad` | `CatalogoItems.Buscar(item_id)` |
| `hablar_npc` | `dialogo_id` | El `NPC` del mapa cuyo `dialogoId` coincide |
| `chatear_telefono` | `escenario_ids` | El `PhoneChatLauncher` que tenga ese escenario |
| `llegar_zona` | `zona_id` | No hace falta resolver nada: el dato es el id |
| `completar_caso_detective` | `caso_id` | El `DetectiveLauncher` cuyo `CasoId` coincide |

El quinto no viene del banco de preguntas —es del Modo Detective, HDU-10— y cuenta
con cualquier resultado del caso: no exige superar el umbral de aciertos, porque
para eso ya está la recompensa propia del caso (HDU-11).

El `tipo` va **en texto y no como número** a propósito: el enum `TipoObjetivo` se
serializa por índice, y el texto sobrevive a que alguien lo reordene.

### Resolver es un reintento, no un momento

`ObjetivoMision.Resolver()` se puede llamar muchas veces y así se usa. Un objetivo
que apunta a un NPC de una zona todavía cerrada **no se puede resolver al
entregar la misión**: ese NPC no existe aún. `MissionTracker` lo reintenta en cada
revisión y engancha el evento cuando por fin aparece; `_suscritos` evita que se
enganche dos veces, que contaría el progreso doble.

Buscar por escena es caro, así que sólo se hace para los objetivos que lo
necesitan: `recoger_objeto` se resuelve con una consulta al catálogo de objetos, y
`llegar_zona` no busca nada.

### Lo del Inspector siempre manda

Ni `MissionGiver` ni `ObjetivoMision` pisan nada puesto a mano. Una ficha
arrastrada, una lista de objetivos con algo dentro, un `NPC` asignado: se
respetan. El catálogo sólo rellena huecos. Así conviven las misiones cableadas a
mano —que apuntan a objetos concretos de la escena— con las que vienen de datos.

## Fichas: cuándo se fabrican y cuándo no

`CatalogoMisiones.Ficha(id)` devuelve el `DesafioData` de esa misión:

- **Si existe un asset con ese id, gana el asset** y no se le toca nada. Un
  ScriptableObject modificado en juego se queda modificado **en el disco** al
  correr desde el editor. Si el asset y el catálogo no coinciden se avisa en
  consola, para que la discrepancia se vea en vez de resolverse a escondidas.
- **Si no hay asset**, se fabrica una ficha en memoria (`HideAndDontSave`) y se
  registra en `CatalogoDesafios`, para que `MissionManager.PrecargarConocidos`
  pueda resolver el id al retomar la partida.

Cuando llega un catálogo nuevo, las fichas de memoria **se actualizan en el sitio**
en vez de reemplazarse: `MissionManager` las guarda por referencia, y soltarlas
dejaría el panel enseñando "(desafío desconocido)" para una misión en curso.

## Configurar un NPC que entregue una misión del catálogo

En el `MissionGiver` del NPC, dejar `Desafio` vacío y escribir el id en
**`Mision Id`**:

```
Mision Id: MISION_SEC_MASCOTA_COIPO
Desafio:   (vacío)
Objetivos: (vacía)
```

Con eso la ficha y los objetivos salen del catálogo. Si se prefiere cablear los
objetivos a mano pero tomar el título de la base, basta con llenar la lista de
objetivos: sólo se toman del catálogo si está vacía.

## La misión inicial: que Otto arranque con algo que hacer

Todas las demás misiones las entrega un `MissionGiver` colgado del
`onDialogueEnded` de un NPC. Eso no sirve para la primera de la historia, que es
justamente la que dice a dónde ir y con quién hablar: sin nadie que la entregue,
el cartel arranca diciendo "por ahora no hay nuevas misiones".

Para eso está **`MisionInicial`**. Un GameObject vacío en la escena del mundo con
ese componente, y ya.

```
Mision:       (vacío)
Mision Id:    MISION_EXPLORACION_01     ← o vacío, y toma la primera del catálogo
Objetivos:    1 entrada
  └ Tipo:     Hablar Con Npc
    Npc:      (arrastrar el NPC)   ← o dejarlo vacío y poner Dialogo Npc Id
Espera Maxima: 15
```

### No lleva cuenta de "primera vez", y es a propósito

`RegistrarDesafioDisponible` ya respeta el estado que traiga la partida: una
misión ya completada se registra completada y **no se anuncia**. Así que entregarla
en cada arranque da el comportamiento correcto sin guardar nada extra, y sin el
riesgo de una marca propia de "ya la di" que se desincronice del progreso real.

### Espera a que la partida esté lista, y por dos motivos

1. `ConfigurarPersistenciaParaPartida` **vacía** la lista de misiones al atar el
   progreso a una partida. Entregar antes de eso es entregar a la basura.
2. Hasta que no llega el progreso del servidor no se sabe si esta misión ya estaba
   hecha (`MisionBackendSync.ProgresoDeMisionesAplicado`). Entregarla antes la
   anunciaría como novedad a un niño/a que la terminó la semana pasada.

Pasado `Espera Maxima` entrega igual: más vale una misión anunciada de más que un
niño/a mirando una pantalla sin nada que hacer. Si no había partida atada, lo dice
en consola.

También sigue los objetivos de una misión **restaurada**, que es un hueco que sólo
se nota aquí: `PrecargarConocidos` repuebla el panel al retomar la partida pero no
vuelve a engancharle los objetivos a nadie. Para las misiones normales eso lo
arregla el `MissionGiver` en la siguiente conversación; ésta no tiene NPC que la
entregue.

### Ojo con el objetivo "hablar con NPC"

Se resuelve buscando el `NPC` del mapa cuyo `dialogoId` coincida. **Hoy ninguno de
los NPCs de `SampleScene` tiene `dialogoId` puesto**, así que por la vía de datos no
resuelve nada. Dos salidas:

- **Arrastrar el NPC** al campo `Npc` del objetivo. Funciona ya, sin tocar datos.
- **Rellenar el `dialogoId`** del NPC en la escena (`HDU1_NPC_HUEMUL` para el
  Huemul guía) y dejar el objetivo apuntando a ese id. Es la vía que escala al
  catálogo.

## Probar el respaldo sin apagar el servidor

En el componente `MisionCatalogoSync`, menú contextual →
**"Usar solo el archivo de respaldo"**. Descarta lo que haya bajado y relee el
archivo. `CatalogoMisiones.DeDonde` dice en todo momento de dónde salió lo que
está en memoria (`Ninguno` · `Archivo` · `Base`).

## Pendiente

- **Los objetivos del archivo están vacíos.** El banco no los trae: una misión
  ahí es un id y un nombre, y lo que hay que hacer está contado en prosa. Hay que
  escribirlos como contenido nuevo, en la base o aquí.
- **El `orden` del archivo es una suposición**: progresión de zonas, y dentro de
  cada una la de exploración antes que las secundarias. Nadie lo ha confirmado
  como el orden narrativo.
- **El título de `MISION_EXPLORACION_01` es un relleno**, porque en el banco esa
  misión no tiene nombre.
- **El endpoint `GET /misiones/` todavía no existe.** Está especificado en
  `REQUISITOS_BD_MISIONES.md`; hasta que exista, el juego corre con el respaldo y
  lo dice en consola.
