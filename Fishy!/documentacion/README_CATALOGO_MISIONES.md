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
| `Mision/Nucleo/CatalogoMisiones.cs` | El catálogo en memoria, las dos fuentes y la fábrica de fichas |
| `Mision/Nucleo/CatalogoDesafios.cs` | `Registrar` / `Olvidar`, para las fichas que no son assets |
| `Mision/MisionCatalogoSync.cs` | Espera a que haya sesión y baja el catálogo |
| `Mision/ObjetivoMision.cs` | `DesdeRegistro` y `Resolver`: de identificadores a objetos de la escena |
| `Mision/MissionGiver.cs` | Toma ficha y objetivos del catálogo si no están en el Inspector |
| `Mision/MisionInicial.cs` | Entrega una misión al empezar, sin NPC que la dé |
| `Resources/misiones.json` | El respaldo |
| `Mision/Nucleo/Tests/CatalogoMisionesTests.cs` | Pruebas del catálogo y del respaldo |

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

## Qué se muestra en el panel: automático o `descripcion`

Por defecto cada objetivo se describe solo, a partir de su tipo y sus datos
(`ObjetivoMision.Describir()`: "Juntar Concha (1/3)", "Hablar con Huemul"...). Dos
campos opcionales cambian eso:

- **`descripcion` en un objetivo** reemplaza SOLO el texto de ESE objetivo por el
  que se escriba ahí. El resto de la misión sigue mostrando la lista normal.
- **`descripcion` en la misión** reemplaza TODA la lista de objetivos por ese único
  párrafo — ninguno de los objetivos se lista aparte, tengan o no su propia
  `descripcion`. Es para una misión que se explica mejor de corrido
  ("recorre el pantano y habla con cada criatura") que como una lista de tareas.

Las dos son opcionales y vacías por defecto: sin ninguna, el comportamiento es el
de siempre. `MissionUIController` (el cartel) y `QuestPageUI` (la pestaña Misión)
aplican el mismo criterio, para que el cartel y la lista completa nunca se
contradigan.

## Las cinco categorías de objetivo

| `tipo` | Campos que usa | Cómo se resuelve contra la escena |
|---|---|---|
| `recoger_objeto` | `item_id`, `cantidad` | `CatalogoItems.Buscar(item_id)` |
| `hablar_npc` | `dialogo_id` | El `DialogoNeutroNPC` del mapa cuyo `dialogoId` coincide |
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
arrastrada, una lista de objetivos con algo dentro, un `DialogoNeutroNPC` asignado: se
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

`MissionGiver` va en el mismo objeto que la interacción del NPC y entrega la
misión cuando esa interacción termina. Sirve cualquiera de las tres:

| Interacción | Componente | Entrega al… |
|---|---|---|
| NPC neutro | `DialogoNeutroNPC` | cerrar el diálogo |
| NPC sospechoso | `PhoneChatLauncher` o `ChatModuleLauncher` | cerrar el chat |
| Caso detective | `DetectiveLauncher` | cerrar el caso, lo apruebe o no |

Si el objeto no tiene ninguna, avisa en consola, y se puede llamar a `Entregar`
desde cualquier evento del Inspector. "Entrega al volver" necesita que la
interacción se pueda repetir: un chat sin `repetible` y un caso ya aprobado no
se vuelven a abrir.

En el `MissionGiver`, dejar `Desafio` vacío y escribir el id en
**`Mision Id`**:

```
Mision Id: MISION_SEC_MASCOTA_COIPO
Desafio:   (vacío)
Objetivos: (vacía)
```

Con eso la ficha y los objetivos salen del catálogo. Si se prefiere cablear los
objetivos a mano pero tomar el título de la base, basta con llenar la lista de
objetivos: sólo se toman del catálogo si está vacía.

### Ni siquiera hace falta escribir el id: puede salir del propio diálogo

Si `Mision Id` y `Desafio` se dejan los dos vacíos, `MissionGiver` prueba una
última cosa antes de rendirse: busca en el banco (`dialogos_npc_neutros`) la
entrada cuyo `id` sea el `dialogoId` de este NPC, y si esa entrada trae
`mision_desbloquea`, usa eso. Es la misma relación que ya carga el backend en
`DialogoNPC.mision` — un NPC cuyo diálogo ya dice qué misión desbloquea no
necesita que nadie repita ese id a mano en el `MissionGiver`; alcanza con que
el NPC tenga su `Dialogo Id` puesto (`HDU1_SEC_COIPO_MASCOTA`, etc.).

## Conexión automática: encadenar misiones y dar recompensas sin tocar la escena

Dos campos más, opcionales, en el catálogo (`Resources/misiones.json` o la
base): **`desbloquea_mision`** e **`recompensa_item_id`** (+ `recompensa_cantidad`,
1 si se omite). Los resuelve `ConexionAutomaticaMisiones`, que se crea sola al
arrancar y revisa el catálogo cada vez que el panel se actualiza:

```json
{
  "mision_id": "MISION_EXPLORACION_02",
  "...": "...",
  "desbloquea_mision": "MISION_M3_TESTIMONIOS",
  "recompensa_item_id": "ITEM_BRUJULA",
  "recompensa_cantidad": 1
}
```

Al completarse `MISION_EXPLORACION_02`, sola: se entrega `MISION_M3_TESTIMONIOS` (por
`EntregarMisionDelCatalogo.EntregarPorId`, la misma lógica que ya usaban los
disparadores de escena) y se agrega `ITEM_BRUJULA` al inventario (con el
mismo guard de `CatalogoRecompensasDetective` — `GetQuantity` antes de
`AddItem`, porque una misión no se repite y por eso no hace falta un "no
duplica al repetir").

**No reemplaza a `DisparadorDeMision` + `EntregarMisionDelCatalogo` /
`EntregarMisionAlEntrarZona`.** Esos siguen siendo el camino para lo que
necesita algo más que "dar esta misión" o "dar este ítem" —cinemática,
desbloqueo de zona, mensaje propio—. Dejar los dos campos vacíos es la forma
de decir "esta misión se conecta a mano, en la escena". En `misiones.json` hoy
solo lo usa `MISION_EXPLORACION_02`, que abre los testimonios al terminar la
presentación de Coipo; el resto se entrega desde la escena. `recompensa_item_id`
no lo usa ninguna misión todavía.

### Bifurcación: `desbloquea_si_acepta`

Un tercer campo convierte el encadenado en una bifurcación según **cómo terminó el
reto** de la misión: si el jugador acabó **rechazando** (su última elección en el
chat fue segura) sigue `desbloquea_mision`; si acabó aceptando o dudando, sigue
`desbloquea_si_acepta`. Vacío = sin bifurcación.

```json
{
  "mision_id": "Z3_03_PRIMER_RETO",
  "desbloquea_mision":    "Z3_04_SEGUNDO_RETO",
  "desbloquea_si_acepta": "Z3_05_RETO_FINAL"
}
```

Es el "paso 4 solo si Otto rechazó el paso 3" de la cadena del orden de narración
(archivada, ver la sección siguiente); el catálogo actual no lo usa. "Rechazar"
es la misma regla del contador de presión social del backend (última decisión del
reto: segura = rechazo, insegura = aceptó, dudosa = corta la racha), pero **se
calcula en Unity, en el momento**: las decisiones del chat no llegan al servidor
hasta el siguiente guardado, así que preguntarle a él justo al terminar el reto
daría siempre "no rechazó". Lo anota `RechazosEnChats` desde `PhoneChatLauncher`,
antes de avisar que el chat cerró, y lo guarda por partida en PlayerPrefs.

`ConexionAutomaticaMisiones.ElegirSiguiente` decide en este orden:

1. Si una de las dos ramas **ya se entregó**, esa. La decisión se toma una vez y
   queda guardada en el propio progreso; sin esto, al restaurar se podrían entregar
   las dos.
2. Si se sabe cómo terminó el reto, la rama que corresponda.
3. Si no se sabe y **la partida se está restaurando**, espera: al cargar, la misión
   completada puede registrarse antes que la rama que ya se había elegido.
4. Si no se sabe y no queda nada por llegar (chat cerrado sin elegir, o partida
   retomada en otro equipo justo después del reto), la rama de "no rechazó": las
   escenas de la otra dan por hecho el rechazo ("ya van dos veces que dices que no").

La tabla `Mision` no tiene este campo. Si algún día `cargar_banco` sube estas
misiones a la base, `CatalogoMisiones` lo sigue tomando del archivo cuando la base
lo trae vacío, para que la bifurcación no desaparezca en silencio.

## La cadena del orden de narración (archivada)

> **No está en uso.** El juego trabaja con el conjunto de 13 misiones de antes
> (`MISION_EXPLORACION_*`, `MISION_NPC_03/04`, `MISION_PANTANO_CRIATURAS`,
> `MISION_M3_TESTIMONIOS` y las seis secundarias), que es el mismo que tiene el
> servidor. La cadena quedó en git: su última versión, con los testimonios v2.6, es
> la de `c4ba9b0`. Para volver a ella:
> `git show c4ba9b0:'Fishy!/Assets/Resources/misiones.json' > 'Fishy!/Assets/Resources/misiones.json'`,
> y ajustar `CatalogoMisionesTests` y la misión inicial de la escena. Lo que sigue
> describe cómo estaba armada.

Esa versión de `misiones.json` seguía el documento *Fishy! – Orden de Narración en Unity*: **una
misión por paso, con un solo objetivo**, encadenadas con `desbloquea_mision`. Como
el panel muestra solo la misión disponible de menor `orden`, eso da "un objetivo
activo a la vez, que se reemplaza al completar" sin tocar la UI.

- **Ids:** zona y paso del documento — `Z2_03_TESTIMONIOS` es la Zona 2, paso 3.
  `Z2_03B_DECIDE_RUMOR` es la confrontación del rumor (`M3_DECISION01`), que el
  documento incluye en el paso 3 pero su tabla de panel no lista aparte.
- **`orden`:** la posición en la cadena (1 a 18). Las secundarias van desde 901 para
  no quitarle nunca el panel a un paso principal.
- **Título y línea:** el título es el nombre narrativo ("Una amistad inesperada") y
  la descripción del objetivo es el texto de panel del documento ("Atiende el chat
  de Puma"), así no se repite el mismo texto dos veces en el HUD.
- **Testimonios (0/3):** tres objetivos `hablar_npc` con la misma descripción, que
  el panel junta en una línea con contador. Los diálogos son
  `HDU3_M3_TESTIMONIO_FLAMENCO`, `_PATO` y `_COIPO` del banco v2.6. Flamenco y Pato
  tienen un NPC propio para el testimonio; el de Coipo es su segundo diálogo
  (`dialogosSiguientes`), así que la presentación cumple `Z2_02_HABLA_COIPO` y la
  conversación siguiente, el testimonio.
- **Reto final:** dos misiones, `Z3_05_RETO_FINAL` (escenario `M6_DECISION01_BASE`)
  y `Z3_05_RETO_FINAL_INTENSO` (`M6_DECISION01`), elegidas por las bifurcaciones de
  los dos retos anteriores: dos rechazos seguidos llevan a la intensa.
- **Fuera de la cadena:** el Diccionario de cada zona (paso 1), que todavía no
  existe, y el Sistema de Finales (`Finales/SistemaDeFinales.cs`), que no es una
  misión: se dispara al llegar a cualquier FIN de la Misión 6.

`Fishy ▸ Probar cadena de misiones` comprueba que todo destino exista, que todo paso
sea alcanzable, que el `orden` siempre avance y que cada diálogo y escenario esté en
el banco: un error de tipeo en un id corta la cadena sin dar ningún error.

## La misión inicial: que Otto arranque con algo que hacer

Todas las demás misiones las entrega un `MissionGiver` colgado del
`onDialogueEnded` de un NPC. Eso no sirve para la primera de la historia, que es
justamente la que dice a dónde ir y con quién hablar: sin nadie que la entregue,
el cartel arranca diciendo "por ahora no hay nuevas misiones".

Para eso está **`MisionInicial`**. Un GameObject vacío en la escena del mundo con
ese componente, y ya.

```
Mision:       (vacío)
Mision Id:    (vacío)                   ← toma la primera del catálogo: MISION_EXPLORACION_01
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

Se resuelve buscando el `DialogoNeutroNPC` del mapa cuyo `dialogoId` coincida, así que el NPC
tiene que tenerlo puesto. En `MainScene` lo tienen los tres guías (Huemul, Coipo,
Foca); los de los testimonios de la Zona 2 (`HDU3_M3_TESTIMONIO_*`) los arma
**Fishy → Configurar testimonios del pantano**. Un objetivo puesto a mano en el Inspector también puede
arrastrar el NPC al campo `Npc`, sin tocar datos.

## Probar el respaldo sin apagar el servidor

En el componente `MisionCatalogoSync`, menú contextual →
**"Usar solo el archivo de respaldo"**. Descarta lo que haya bajado y relee el
archivo. `CatalogoMisiones.DeDonde` dice en todo momento de dónde salió lo que
está en memoria (`Ninguno` · `Archivo` · `Base`).

## Pendiente

- **Los objetivos de las seis secundarias están vacíos.** Se completan a mano o
  quedan informativas hasta que se escriban como contenido.
- **El Diccionario de cada zona** no existe todavía.
- **Partidas jugadas con la cadena archivada** (ids `Z1_…`, `Z2_…`, `Z3_…`) conservan
  esas misiones en su progreso, y ya no están en el catálogo. Para probar, partida
  nueva.
- **`GET /misiones/` ya existe** en `dev` (migración 0016, ver `REQUISITOS_BD.md`).
  Si la base compartida todavía no la tiene aplicada, o no hay sesión, el juego corre
  con este archivo de respaldo y lo dice en consola.
- **El avance por objetivo sí se guarda** (`ObjetivosBackendSync`), salvo
  `recoger_objeto`, que se recalcula de la mochila.
