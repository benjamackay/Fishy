# El sistema de guardado

Cómo llega a la base de datos lo que el niño/a hace en la partida: **cuándo se sube,
por dónde pasa, qué se pierde si algo falla y cómo comprobarlo.**

En una línea: **nada se sube en el momento en que ocurre, y nada se pierde.** Los
cambios se acumulan en una cola, que se va vaciando cada pocos segundos mientras se
juega y además al cambiar de zona y al cerrar. Lo que no alcanza a subir queda **anotado
en disco** y sale al volver a entrar.

> Antes cada cosa se mandaba en cuanto pasaba: un POST por objeto recogido, otro por NPC
> terminado, uno por **cada línea de chat**, más el PATCH de posición y el PUT de
> inventario. Contra Supabase cada petición cuesta **600-800 ms**
> (`Backend/README.md`), así que el juego se pasaba la partida goteando tráfico.

> **Y la cola, en su primera versión, vivía solo en memoria**, con la pérdida de un
> cierre sucio asumida como aceptable. No lo era. Medido sobre los logs del 25 de
> septiembre de 2026, cuatro paradas del Play en una tarde se llevaron **19 cambios**; y
> como las misiones se recuperaban solas por el PlayerPrefs de `MissionManager` pero los
> objetivos no tenían red, la base acabó afirmando que una misión estaba completa con
> **1 de 3** objetivos hechos. Para siempre: el chat que marca el objetivo no se vuelve a
> disparar. De ahí el diario (§2).

---

## 1. Los momentos

| Momento | Quién lo dispara | Tope | Reintenta |
|---|---|---|---|
| **En segundo plano** | `ColaDeCambios.GotearDeFondo()`, cada `intervaloDeFondo` = 10 s | `topeDeFondo` = 15 s | Sí |
| **Cambio de zona** | `ZonaActual.OnZonaCambiada` → `SaveManager.AlCambiarDeZona` | `topeNormal` = 8 s | Sí |
| **Cierre del juego** | `Application.wantsToQuit` → `SaveManager.QuiereCerrar` | `topeDeCierre` = 10 s | No: no hay otra oportunidad **en esta sesión** (pero lo anotado vuelve en la siguiente) |
| **Manual** | Una prueba, o el menú de pausa | `topeNormal` | Sí |
| **Pedido del jugador** | El botón «Intentar ahora» de `AvisoDeGuardado` → `ColaDeCambios.ReintentarAhora()` | `topeDeFondo` | Sí |

**Por qué hay uno de fondo, si ya había dos.** Los dos momentos son *instantes*, y entre
uno y otro puede pasar media partida: quien se queda cuarenta minutos en la misma zona no
guardaba nada en cuarenta minutos. Con el goteo, la ventana deja de ser una lista de
instantes y pasa a ser «siempre, unos segundos por detrás».

**Y no vuelve al goteo de una petición por evento**, que es lo que esta cola vino a
quitar: dentro del intervalo los cambios se siguen agrupando por clave, así que el número
de peticiones es el mismo, repartido en vez de en bloque.

> Vale decirlo, porque este documento antes afirmaba lo contrario: la primera versión quitó
> a propósito un guardado periódico que existía, y el de fondo lo reintroduce. La
> diferencia es que aquel guardaba *cada cosa* en cuanto pasaba y este vacía *la cola
> agrupada*. Lo que se quitó fue el goteo, no la periodicidad.

> «Pedido del jugador» **no pasa por `momentosActivos`**, a diferencia de los demás. Ese
> interruptor es para los momentos automáticos, y `Manual` viene apagado de fábrica, así
> que pedirlo por ese camino no habría hecho nada. `ReintentarAhora()` vacía la cola
> directamente.

### Encenderlos y apagarlos sin tocar código

`SaveManager.momentosActivos` es un `[Flags]` visible en el Inspector:

```csharp
[Flags] public enum Momentos
{
    Ninguno = 0, CambioDeZona = 1, CierreDeAplicacion = 2, Manual = 4, EnSegundoPlano = 8
}
```

Los valores de `Motivo` son las mismas potencias de dos, así que `Preparar()` pregunta
con un `&` y no hay ningún `switch` que haya que acordarse de ampliar. Dejarlo en
`Ninguno` es legítimo para depurar y el juego lo avisa al arrancar con un `LogWarning`.

> ⚠️ **Para que ese campo sirva hay que poner el `SaveManager` en MainScene.** Hoy se
> autocrea por código, y `GetOrCreate()` busca primero uno existente con
> `FindAnyObjectByType`, así que basta con arrastrarlo a la escena y los valores del
> Inspector mandan. Sin eso, el valor efectivo es siempre el del inicializador.

---

## 2. El recorrido de un cambio, y el diario

```
  algo pasa en el juego
          │
          ▼
  XxxSync.Marcar…()                      ← no llama al backend
          │
          ▼
  ColaDeCambios.Encolar…(clave, …)       ← coalesce por clave
          │
          ├──────────────▶ DiarioDeCambios.Anotar()   ← a disco, YA
          │                (PlayerPrefs "Fishy.Cola.Diario")
          │
          │   … el niño/a sigue jugando …
          │
          ▼
  Vaciar()  ← fondo cada 10 s / zona / cierre / «Intentar ahora»
          │
          ▼
  ApiManager.Send<T>()                   ← la petición de verdad
          │
          ├─ salió bien ──▶ DiarioDeCambios.Confirmar()  ← recién ahora se borra
          └─ falló ───────▶ se queda anotado, con retardo creciente
```

### Datos y receta, no un closure

Un cambio **no puede llevar un `Action`** si se quiere anotar: un closure no se escribe en
disco. Así que lleva **(nombre de receta, argumentos)**:

```
antes:   objeto:FLOR → closure
ahora:   objeto:FLOR → "objeto" + { ObjetoId: "FLOR" }
```

El patrón no es nuevo: `EncolarZona` ya guardaba un valor y una función para rehacer el
envío al vaciar. Lo único que cambió es que **la función pasa a tener nombre**.

Las ocho recetas viven juntas en `RecetasDeCola.cs`: `mision`, `objetivo`, `zona`,
`objeto`, `npc`, `detective`, `chat`, `progreso`. **En un solo archivo a propósito.** Si
cada sincronizador registrara la suya al despertar, reproducir el diario dependería de qué
componentes se crearon ya, y una receta sin registrar dejaría su cambio muerto en
silencio.

Los argumentos van como JSON anidado dentro del sobre, y no como objeto: primero se lee el
sobre, y el nombre de la receta dice de qué tipo son los argumentos de dentro. Eso evita
resolver tipos polimórficos al deserializar. La familia va como **texto** para que añadir
una familia mañana no corrompa un diario escrito hoy.

### Lo que NO se anota

`inventario` y `personaje`. Son los dos *thunks* de verdad —leen el estado vivo al
subir— y además los únicos **reconstruibles**: el siguiente vaciado manda la mochila y la
posición completas de todas formas. Anotarlos sería guardar una foto vieja de algo que se
puede volver a mirar. Es la línea que mantuvo el cambio acotado, y el log lo dice cuando
entra uno: `Sin diario: no sobrevive al cierre.`

### Cada entrada se sube a SU partida

Todos los métodos de escritura del `ApiManager` aceptan `int? partidaId` y los endpoints
son por partida, así que **un objetivo anotado en la partida 3 se sube a la 3 aunque ahora
se juegue la 4**, y el servidor comprueba que esa partida sea de este adulto. Por eso el
diario no tiene que descartar nada al cambiar de perfil (§7).

`ActualizarPartida` y `RegistrarChatCompleto` eran los dos que fijaban `PartidaId` en la
ruta en vez de recibirlo; ahora lo reciben.

---

## 3. Las tres familias

Una cola plana perdería datos, así que el almacén es un **diccionario por clave** con
tres tratos distintos:

| Familia | Qué es | Al reencolar la misma clave | Ejemplos |
|---|---|---|---|
| **Snapshot** | Solo importa el último estado | Pisa | `personaje`, `inventario` (la orden de leer el estado vivo), `partida.progreso` (que sí lleva datos, y se anota) |
| **Append** | Un hecho idempotente por clave | Pisa con el valor nuevo | `objeto:{id}`, `npc:{id}`, `mision:{id}`, `zona:{slug}`, `detective:{caso}` |
| **Cadena** | Varias peticiones que dependen unas de otras | Pisa | `chat:{n}` |

**Lo importante de Snapshot:** lo que se encola es un *thunk* que lee el estado vivo
**cuando la cola lo ejecuta**, no una copia de ahora. Por eso marcar sucia la mochila mil
veces cuesta lo mismo que una, y por eso siempre sube el estado final y nunca uno
intermedio.

**Lo importante de Cadena:** va **sola y esperando**, sin paralelismo, porque
`ApiManager.NpcId` y `ChatId` son estado global mutable y dos cadenas a la vez se
pisarían. Hoy solo la usa el respaldo del chat (ver §12).

> Ojo a la diferencia entre familia y diario: la **familia** decide la coalescencia y el
> paralelismo; lo que decide si algo se anota es tener **receta**. `partida.progreso` es
> `Snapshot` y se anota; `inventario` es `Snapshot` y no.

Reencolar una clave **reemplaza el contenido pero conserva su posición original**. Si no,
un snapshot que se repite mucho —la posición de Otto, que se marca en cada guardado— se
iría colando por delante de hechos más viejos, y esos serían justo los que se quedan
fuera cuando vence el plazo.

### Las dos excepciones que fusionan en vez de pisar

`RecetasDeCola.FusionDe(receta)` dice cómo, y la fusión se aplica en **tres** sitios: al
encolar, al reencolar un fallo y **al escribir en el diario**. Ese tercero no es
paranoia — ver el aviso al final de esta sección.

**`zona:{slug}` fusiona por OR.** Hay dos escritores con significados opuestos:
`MisionBackendSync` manda `completada: false` al desbloquearla y
`BosqueDesconocidosManager` / `RegistrarZonaCompletada` mandan `true` al terminarla. Con
"gana el último", un desbloqueo que llegara después de un completado **degradaría la zona
en el reporte del adulto**. Completar es un camino de ida.

**`partida.progreso` fusiona por máximo.** Cada zona manda un valor absoluto al cerrarse.
Si dos se cerraran entre dos vaciados y la segunda tuviera un valor menor, el progreso
del niño/a retrocedería.

> ⚠️ **Fusionar solo en memoria no alcanzaba, y esto costó una prueba en rojo.** La zona
> queda «completada» y no alcanza a subirse; a la sesión siguiente, al recalcular, se
> encola como «desbloqueada». En memoria no hay nada —la cola arrancó vacía—, así que sin
> fusionar **contra el diario** se escribiría «desbloqueada» encima de la «completada»
> anotada, y el reporte del adulto pasaría a mostrar como pendiente una zona ya terminada.
> Por eso `DiarioDeCambios.Anotar()` recibe la fusión. Fusionar dos veces no molesta: OR y
> máximo dan lo mismo repetidos. Lo cubre
> `FishyPruebasCola.ProbarDiarioNoDegradaUnaZona`.

---

## 4. Qué se encola, quién lo encola y dónde acaba

| Clave | Familia | Receta | Lo pone | Acaba en |
|---|---|---|---|---|
| `personaje` | Snapshot | — *(no se anota)* | `PersonajeBackendSync.MarcarSucio()` | `PATCH /partidas/{id}/personaje/` |
| `inventario` | Snapshot | — *(no se anota)* | `InventarioBackendSync.MarcarSucio()` | `PUT /partidas/{id}/inventario/` |
| `partida.progreso` | Snapshot | `progreso` (máx.) | `ColaDeCambios.EncolarProgreso()` | `PATCH /partidas/{id}/` |
| `objeto:{id}` | Append | `objeto` | `ObjetosRecogidosSync.Marcar()` | `POST /partidas/{id}/objetos-recogidos/` |
| `npc:{id}` | Append | `npc` | `NpcTematicaSync.Marcar()` | `POST /partidas/{id}/progreso-npcs/` |
| `mision:{id}` | Append | `mision` | `MisionBackendSync` | `POST /partidas/{id}/misiones/` |
| `objetivo:{mision}:{orden}` | Append | `objetivo` | `ObjetivosBackendSync` | `POST /partidas/{id}/objetivos/` |
| `zona:{slug}` | Append | `zona` (OR) | `MisionBackendSync`, `BosqueDesconocidosManager`, `RegistrarZonaCompletada` | `POST /partidas/{id}/zonas/` |
| `detective:{caso}` | Append | `detective` | `DetectiveCaseManager` | `POST /casos-detective/{caso}/progreso/` |
| `chat:{n}` | Cadena | `chat` | `ChatBackendLogger.LogEnd()` | `POST /partidas/{id}/chats/completo/` (y la cadena antigua como respaldo, §12) |

Cada sincronizador tiene su método de encolar (`EncolarMision`, `EncolarObjetivo`,
`EncolarObjeto`, `EncolarNpc`, `EncolarDetective`, `EncolarChat`, `EncolarZona`,
`EncolarProgreso`), así que el sitio que provoca el cambio no arma cuerpos de peticiones:
solo dice qué pasó.

### El chat es el caso especial

`ChatBackendLogger` **graba la conversación entera en memoria** y encola **una sola**
entrada al terminarla. No manda nada durante la conversación.

Encolar solo en `LogEnd()` es **correcto por construcción**: `PhoneChatLauncher.
bloquearMovimiento` es configurable, o sea que Otto puede caminar durante un chat y
cruzar de zona a media conversación. Si se encolaran mensajes sueltos, ese vaciado
mandaría media cadena y el `FinalizarChat` nunca llegaría. Así, una conversación sin
terminar sencillamente no está en la cola.

Esto además **quitó** código: la `Queue<Action>` que tenía el logger existía solo porque
el `ChatId` no llegaba hasta que contestaba `IniciarChat`. Mandando al final, ese baile
desaparece.

### Los dedup cambiaron de significado

`MisionBackendSync.misionesEnServidor`, `ObjetosRecogidosSync.yaRecogidos`,
`NpcTematicaSync.terminados` e `InventarioBackendSync.ultimoSubido` antes querían decir
*"lo que el servidor confirmó"* y se escribían en el `onSuccess`. Bajo una cola eso no
filtra nada, porque el `onSuccess` llega al vaciar. Ahora quieren decir **"encolado o
confirmado"** y se escriben al encolar.

> Ese cambio tenía un agujero mientras la cola era volátil: un cambio que se **abandonaba**
> tras `maxIntentos` dejaba el dedup diciendo «ya está» y no se volvía a encolar en toda la
> sesión. Se cerró por construcción al dejar de abandonar (§7): «encolado» ya quiere decir
> duradero.

---

## 5. El cierre del juego

El gancho es **`Application.wantsToQuit`**: devolver `false` cancela el cierre. La ventaja
decisiva es que **el aspa de la ventana y Alt+F4 pasan por ahí**, así que quedan cubiertos
sin tocar ningún botón.

```
wantsToQuit  →  ¿ya vaciamos?        → sí: cerrar
             →  ¿se está vaciando?   → sí: cancelar (segundo Alt+F4)
             →  arrancar CerrarCuandoTermine() y cancelar este cierre
```

`CerrarCuandoTermine()` vacía con tope de 10 s. Si al vencer quedan cambios **pregunta en
vez de decidir**: `MenuPausa.PreguntarSiEsperar()` muestra *"No se pudo conectar con el
servidor"*, cuántos cambios quedan, y dos botones — **"Seguir esperando"** (otros 10 s,
repetible) y el de cerrar. Mientras el cartel está a la vista el cierre sigue cancelado;
no hay prisa. Lo que no vale es cerrar en silencio.

**El cartel cambia según si hay algo que lamentar**, que es `ColaDeCambios.TodoAnotado`:

```
todo anotado:   "Quedan 3 cambio(s) por subir, y se guardan solos
                 la próxima vez que entres."          [Cerrar]

algo sin anotar: "Quedan 3 cambio(s) sin guardar."    [Cerrar de todas formas]
```

Antes decía siempre lo segundo. Confundir «no se subió» con «se perdió» es justo lo que
hizo creer durante semanas que el guardado estaba roto, así que la distinción va en el
texto y también en el nivel de los logs (§10).

**Y al cerrar no se respeta el retardo.** `Vaciar()` salta lo que está esperando su turno
o atascado, salvo cuando el motivo es el cierre: es la última oportunidad de la sesión, así
que se intenta todo. El propio `reintentarSiFalla` distingue los dos casos, sin otro
parámetro.

Los tres caminos de salida terminan en el mismo sitio:

| Camino | Pasa por | Quién pregunta |
|---|---|---|
| Botón "Guardar y salir" | `MenuPausa.GuardarYSalir()` | El propio menú; después llama a `SaveManager.MarcarCierreListo()` para que no se arranque un segundo vaciado |
| Aspa de la ventana | `wantsToQuit` | `SaveManager` se lo pide a `MenuPausa` |
| Alt+F4 | `wantsToQuit` | ídem |

`OnApplicationPause` se queda como estaba: en móvil el sistema mata la app pausada sin
avisar y es la única señal fiable. No se puede retrasar, así que es best-effort.

### Ninguna petición sobrevive a su plazo

Con un tope de cierre de 10 s y un `timeout` de red de 15 s había una mentira:

```
t=0s    se lanza la petición
t=10s   el vaciado se rinde        →  "no se pudo guardar"
t=15s   la petición daría timeout  →  pero ya nadie escucha
```

El juego afirmaba que falló algo que a los 12 s todavía podía tener éxito. Se arregla
**acotando cada petición a lo que le quede al vaciado**, no con un aviso mejor:

```csharp
// ApiManager
public int? TopeDeTiempoParaPeticiones { get; set; }   // null = usar timeoutSeconds
req.timeout = TopeDeTiempoParaPeticiones ?? timeoutSeconds;
```

La cola lo fija antes de cada envío y lo devuelve a `null` en un `finally`: es estado
global, y si un fallo lo dejara puesto, **todas** las peticiones del resto de la sesión
heredarían un timeout de segundos y empezarían a fallar sin motivo aparente.

### Los cinco finales de un vaciado

| Estado | Qué pasó |
|---|---|
| **Subido** | Entró. Se borra del diario |
| **Fallido** | Se intentó y el servidor dijo que no. Vuelve a la cola con retardo |
| **Sin respuesta** | Salió y venció el plazo sin que contestara, ni bien ni mal |
| **Sin intentar** | Se quedó en la cola sin llegar a salir |
| **Esperando** | Se saltó a propósito: aguardaba su retardo, o está atascado |

`TodoBien` exige que fallido, sin respuesta y sin intentar estén en cero. **«Esperando» no
cuenta**: no es una pérdida, es que no era su momento, y si contara el cartel del cierre
saltaría cada vez que algo estuviera en pleno retardo.

Esa categoría de «sin respuesta» existe porque sin ella un envío que no llamara nunca a su
callback no aparecía en ningún lado: el resultado decía *"todo bien"* y el cierre daba por
guardado un cambio recién perdido. **Lo que cambió con el diario:** ya no se da por
perdido, porque sigue anotado y vuelve en la sesión siguiente. Puede que hubiera llegado y
se mande dos veces — y da igual, porque todas las escrituras son idempotentes a propósito
(`get_or_create` + camino de ida en el backend).

---

## 6. Lo que NO pasa por la cola

**Prerrequisitos síncronos**, porque sin ellos no hay a dónde guardar: `Login`,
`Registro`, `CrearJugador`, `CrearPartida`, `SeleccionarJugador`, `RetomarPartida`.

**Todos los GET.** Bajar el estado al entrar (`ObjetosRecogidosSync.Bajar()`,
`NpcTematicaSync.Bajar()`, el inventario, la posición) no cambió nada.

**Las herramientas**: `ApiSmokeTest.cs` y `Assets/Editor/FishyPruebas*.cs` llaman directo
a propósito.

**El modo local** (`ApiManager.useLocalMode`) escribe en PlayerPrefs de forma síncrona:
`Send` nunca corre, `PeticionesEnVuelo` queda en 0 y el vaciado termina al instante.

---

## 7. Qué se pierde y qué ya no

**Nada de lo que está anotado se pierde.** Ni parando el Play, ni con un crash, ni
quedándose sin batería. Vuelve a la cola al arrancar (`ColaDeCambios.Reproducir()`), y como
cada entrada lleva su partida, vuelve también si entretanto se cambió de perfil.

**Nada se abandona, tampoco.** `maxIntentos` dejó de significar «se tira» y pasó a
significar «deja de insistir solo»: el cambio queda **atascado**, sigue en el diario, sale
en el aviso de pantalla y vuelve con «Intentar ahora» o en la sesión siguiente. Los
retardos entre intentos son 10 s, 30 s, 60 s y después 120 s, porque insistir cada pocos
segundos contra un servidor caído no arregla nada y llena el log.

Y al reponer del diario los intentos **arrancan de cero**: algo que se atascó ayer merece
sus intentos completos hoy, cuando el servidor caído o el wifi del colegio ya pueden no ser
el problema.

### Lo que sí se queda en el camino

1. **La mochila y la posición de Otto**, si el proceso muere. No se anotan a propósito
   (§2): el siguiente guardado las manda completas, así que se recalculan solas. Es el
   único caso que todavía provoca un `LogError`.
2. **Un chat abandonado a medias no queda ni a medias.** Antes el backend conservaba los
   mensajes ya enviados con `fecha_termino` en NULL — a medias, pero visible en el reporte
   del adulto. Ahora: o la conversación entera o nada. Es consecuencia de encolar solo en
   `LogEnd()`, no del diario.
3. **El aspa retrasa el cierre.** Si el vaciado tiene un fallo, el juego parece colgado
   unos segundos. Por eso el tope es duro sobre `realtimeSinceStartup` y un segundo Alt+F4
   cierra en seco.
4. **El diario crece si el servidor está caído mucho tiempo.** `avisarPorEncimaDe` lo
   advierte por consola, y el aviso de pantalla lo hace visible para quien juega.

### El sello de partida ya no descarta

Cada entrada sigue sellada con la partida en que ocurrió, pero el sello pasó de **descartar
a dirigir**: se usa para subir el cambio a su partida, no para tirarlo.

Antes era lo único que evitaba escribirle el avance de un hermano al otro —este proyecto ya
se quemó con eso—, y el precio era perder el cambio. Ahora no hay precio: los endpoints de
escritura son todos por partida y el servidor comprueba que la partida sea de este adulto,
así que el cambio va donde debe.

Lo que **no** está anotado sí se sigue descartando si es de otra partida: un snapshot lee
el estado vivo, y ese estado ya es de la partida de ahora.

---

## 8. Configuración

### `SaveManager`

| Campo | Por defecto | Qué hace |
|---|---|---|
| `momentosActivos` | `CambioDeZona \| CierreDeAplicacion \| EnSegundoPlano` | En qué momentos se vacía |
| `esperaMinima` | 1 s | Mínimo entre dos guardados. Caminar sobre el borde de dos zonas dispara cambios en cadena; esto los agrupa. **El cierre se la salta** |
| `topeNormal` | 8 s | Plazo de un vaciado por cambio de zona |
| `topeDeCierre` | 10 s | Cuánto se retiene el cierre antes de preguntar |
| `verboseLogs` | true | Escribe cada guardado y su motivo |

### `ColaDeCambios`

| Campo | Por defecto | Qué hace |
|---|---|---|
| `paralelismo` | 4 | Peticiones a la vez. Las cadenas van de una en una pase lo que pase |
| `maxIntentos` | 6 | Intentos antes de dejar de insistir solo. **No se pierde**: queda atascado y anotado |
| `intervaloDeFondo` | 10 s | Cada cuánto se vacía la cola mientras se juega |
| `topeDeFondo` | 15 s | Plazo de un vaciado de fondo o de un «Intentar ahora» |
| `avisarPorEncimaDe` | 200 | Avisa si la cola crece tanto sin vaciarse: señal de que algún vaciado dejó de ocurrir |
| `verboseLogs` | true | Escribe cada cambio que entra y cada vaciado |

### `AvisoDeGuardado`

| Campo | Por defecto | Qué hace |
|---|---|---|
| `segundosAntesDeAvisar` | 30 s | Cuánto tiene que llevar algo sin subir antes de avisar en pantalla. Por debajo no se dice nada: la cola se vacía sola cada 10 s y un pendiente de paso no significa nada |
| `cadaCuantoSeRevisa` | 0,5 s | Cada cuánto se mira si hay algo pendiente |
| `iconoPlaceholder` | vacío | **PLACEHOLDER.** Vacío dibuja un círculo con un `!`. Poner aquí el sprite definitivo es todo lo que hace falta para reemplazarlo |

Ambos se autocrean con `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` +
`DontDestroyOnLoad`, y un segundo `SubsystemRegistration` limpia los estáticos entre
corridas del editor. **Para tocar los campos hay que ponerlos en la escena.**

---

## 9. Cómo comprobarlo

### Headless, sin servidor ni editor abierto

```bash
"$HOME/Unity/Hub/Editor/6000.4.9f1/Editor/Unity" -batchmode -nographics -quit \
  -projectPath 'Fishy!' \
  -executeMethod Fishy.EditorTools.FishyPruebasCola.Ejecutar -logFile -
```

`Assets/Editor/FishyPruebasCola.cs` — **37 comprobaciones** sobre lo que **no se ve en un
log de red**: las dos fusiones, el orden, que reencolar no adelante, el sello de partida,
los reintentos con su retardo, que el tope de tiempo se restaure, que el thunk lea al
vaciar y no al encolar, los interruptores de `momentosActivos`, y que lo que sale y no
contesta cuente como pendiente y no como guardado.

Las del diario, en particular:

| Prueba | Qué fija |
|---|---|
| `ProbarDiarioSobreviveAlReinicio` | El caso que costó 19 cambios: se vacía la cola en memoria y lo anotado vuelve con sus datos |
| `ProbarSnapshotsNoSeAnotan` | La mochila y la posición no se anotan; el objeto recogido sí |
| `ProbarDiarioConservaSuPartida` | Lo anotado vuelve con **su** partida, no con la que se juega |
| `ProbarIdaYVueltaDeLasRecetas` | Las ocho recetas sobreviven al viaje por disco con sus argumentos. Es la red de la conversión de closures a datos, que tocó nueve sitios |
| `ProbarDiarioNoDegradaUnaZona` | Reponer no degrada una zona ya completada (§3) |
| `ProbarFalloSeReintentaConRetardo` | Tras fallar espera; tras `maxIntentos` se atasca **sin perderse**; «Intentar ahora» lo desatasca |

También están `FishyPruebasPartida` (guardado e inventario en modo local),
`FishyPruebasChat`, `FishyPruebasObjetivos` y `FishyPruebasDetectiveRecompensa`.

> ⚠️ **Si las pruebas se corren sobre una copia del proyecto, comparten PlayerPrefs con el
> juego de verdad.** Se indexa por company/product name, no por la ruta del proyecto, así
> que las dos escriben en `~/.config/unity3d/DefaultCompany/Fishy!/prefs`. Como encolar
> ahora escribe el diario, una prueba que no limpiara le dejaría al juego real cambios
> inventados para subir al backend. El arnés limpia en `Vaciar()`, y conviene comprobarlo
> con un `grep Fishy.Cola.Diario` al archivo de prefs al terminar.

> **Trampa del modo edición:** Unity no llama a `Awake` fuera del Play, así que los
> singletons están en null y una prueba mal montada pasa en verde sin haber vaciado nada.
> El arnés escribe `Instance` por reflexión — ver `FishyPruebasCola.FijarInstancia`, que
> explica por qué no se llama al `Awake` entero.

### Con el backend local

```bash
bash Backend/run.sh --local     # SQLite, no necesita la clave de Supabase
```

Con `verboseLogs` puesto, las entradas salen **a los pocos segundos y sin cambiar de
zona**, y la clave del diario queda vacía. Después: matar el servidor a media partida, ver
el retardo creciente y el aviso en pantalla, levantarlo otra vez y comprobar que se pone al
día **sin tocar nada**.

**La prueba decisiva**, que es exactamente el caso que originó todo esto: parar el Play con
cambios pendientes, volver a darle Play y comprobar en la base que se subieron.

```sql
SELECT mision_id, orden FROM api_objetivoprogreso WHERE partida_id = <N> ORDER BY orden;
```

### A mano

Los tres caminos de salida —botón, aspa, Alt+F4— cada uno con el backend arriba y abajo.
Son seis casos. Con el backend abajo tiene que salir el cartel a los 10 s.

> En el editor, que `wantsToQuit` cancele la salida del Play Mode **hay que verificarlo en
> la máquina, no asumirlo**: ha cambiado entre versiones de Unity. El camino que sí se
> comprueba en editor es el botón de `MenuPausa`.

---

## 10. Los logs

```
[Cola] +{clave} ({familia}) — {n} pendientes.          (solo con verboseLogs)
[Cola] +{clave} … Sin diario: no sobrevive al cierre.  (la mochila y la posición)
[Cola] {n} cambio(s) quedaron sin subir la última vez y vuelven a la cola.
[Cola] Vaciando por {motivo}: {n} cambios.
[Cola] ✓ {qué}                                         (uno por cambio confirmado)
[Cola] Vaciada en {t} s: {n} subidos. {m} esperan su turno.
```

El `✓` reemplazó los `LogWarning` casi idénticos que tenía cada sincronizador. Informa la
cola, que es la única que sabe si algo se va a reintentar ahora, dentro de un rato o en la
sesión siguiente.

**El nivel del aviso distingue retraso de pérdida**, que es el punto:

```
# hay pendientes, pero todos anotados → LogWarning
[Cola] Vaciada por CierreDeAplicacion en 10,1 s: 3 subidos, 1 fallidos, …
       Quedan: objeto:FLOR_03, chat:2
       Todo lo que queda está anotado: se sube solo en cuanto haya conexión,
       o al volver a entrar al juego.

# algo sin anotar → LogError
[Cola] Vaciada por CierreDeAplicacion en 10,1 s: 3 subidos, 1 fallidos, …
       1 cambio(s) NO están anotados y se pierden (la mochila y la posición
       de Otto, que se recalculan solas al siguiente guardado).
```

En un build el log es lo único que queda en `Player.log`, así que reservar `LogError` para
lo que de verdad se pierde es lo que lo mantiene útil.

Y al parar el Play en el editor, `SaveManager.AlCambiarElPlay` dice qué pasó. Antes era un
`LogError` porque los cambios se perdían; ahora solo se retrasan:

```
[SaveManager] Se paró el Play con 6 cambio(s) en la cola. No se pierden: están
              anotados en el diario y se suben al volver a darle Play.
```

---

## 11. Archivos

| Archivo | Rol |
|---|---|
| `ColaDeCambios.cs` | La cola: el almacén, las tres familias, el vaciado, el goteo de fondo |
| `RecetasDeCola.cs` | Nombre de receta → la llamada al backend, y sus argumentos serializables |
| `DiarioDeCambios.cs` | Lo pendiente, escrito en disco. Anota al encolar, borra al confirmar |
| `UI/AvisoDeGuardado.cs` | El aviso en pantalla y el botón «Intentar ahora» |
| `SaveManager.cs` | Los momentos, `wantsToQuit`, el cierre retenido |
| `ApiManager.cs` | `PeticionesEnVuelo` y `TopeDeTiempoParaPeticiones` en `Send<T>` |
| `UI/MenuPausa.cs` | El botón de salir y el cartel de "sin conexión", que ahora distingue retraso de pérdida |
| `Chat/ChatBackendLogger.cs` | Graba la conversación y la manda entera, en un POST atómico |
| `ObjetivosBackendSync.cs` | Guarda y restaura el avance por objetivo |
| `PersonajeBackendSync.cs`, `InventarioBackendSync.cs`, `ObjetosRecogidosSync.cs`, `NpcTematicaSync.cs`, `MisionBackendSync.cs` | Encolan en vez de llamar |
| `Detective/DetectiveCaseManager.cs`, `Mision/RegistrarZonaCompletada.cs`, `Zonas/BosqueDesconocidos/BosqueDesconocidosManager.cs` | Ídem, escrituras sueltas |
| `../Editor/FishyPruebasCola.cs` | Las pruebas headless |

---

## 12. Pendiente

**El chat ya sube en un solo POST atómico** (`POST /api/partidas/{id}/chats/completo/`,
~0,7 s en vez de ~6 s, y o entra la conversación entera o no entra nada).
`ChatBackendLogger.Subir()` lo usa; si el servidor todavía no tiene ese endpoint responde
404 y cae a la cadena antigua (`RegistrarNPC` → `IniciarChat` → N × `RegistrarMensaje` →
`FinalizarChat`) para no dejar la conversación reintentándose para siempre. Lo que se graba
y cuándo se encola no cambió. Contrato completo en `REQUISITOS_BD.md`, sección A.3.

**El avance por objetivo también se guarda** (`ObjetivosBackendSync`, `GET/POST
/partidas/{id}/objetivos/`): cada objetivo cumplido de una misión del catálogo se encola
con `EncolarAppend` (clave `objetivo:<mision>:<orden>`) y al entrar a la partida se baja y
se aplica a `MissionTracker`. `recoger_objeto` queda fuera a propósito: se recalcula de la
mochila.

**También pendiente:** arrastrar el `SaveManager` a MainScene para que sus campos del
Inspector manden (ver §1). Hoy no está en ninguna escena, así que los valores efectivos son
los del inicializador — lo cual, de momento, es justo lo que se quiere: es lo que hace que
`EnSegundoPlano` esté encendido sin tocar ninguna escena.

**Reemplazar el icono del aviso.** `AvisoDeGuardado.iconoPlaceholder` está vacío y se
dibuja un círculo con un `!`. El panel y el botón ya están con la tipografía y los colores
del juego; falta solo el sprite.

**Sin conexión, todavía no.** El diario recoge únicamente lo que hoy se encola, o sea el
modo conectado: en modo local los sincronizadores cortan antes de encolar y escriben en
PlayerPrefs por su cuenta. Con el diario ya en disco, hacer que el modo local encole y
sincronice al reconectar es un paso corto, y arreglaría de paso que hoy el avance **por
objetivo** no se guarde en absoluto sin servidor (`ObjetivosBackendSync.AlCumplirObjetivo`
corta por `IsLocalMode`). Queda fuera a propósito: habría que decidir qué pasa si se juega
en local y luego se entra con otra cuenta.
