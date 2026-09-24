# El sistema de guardado

Cómo llega a la base de datos lo que el niño/a hace en la partida: **cuándo se sube,
por dónde pasa, qué se pierde si algo falla y cómo comprobarlo.**

En una línea: **nada se sube en el momento en que ocurre.** Los cambios se acumulan en
una cola en memoria y salen todos juntos en dos momentos — al cambiar de zona y al
cerrar el juego.

> Antes cada cosa se mandaba en cuanto pasaba: un POST por objeto recogido, otro por NPC
> terminado, uno por **cada línea de chat**, más el PATCH de posición y el PUT de
> inventario. Contra Supabase cada petición cuesta **600-800 ms**
> (`Backend/README.md`), así que el juego se pasaba la partida goteando tráfico.

---

## 1. Los momentos

| Momento | Quién lo dispara | Tope | Reintenta |
|---|---|---|---|
| **Cambio de zona** | `ZonaActual.OnZonaCambiada` → `SaveManager.AlCambiarDeZona` | `topeNormal` = 8 s | Sí, hasta `maxIntentos` = 3 |
| **Cierre del juego** | `Application.wantsToQuit` → `SaveManager.QuiereCerrar` | `topeDeCierre` = 10 s | No: no hay otra oportunidad |
| **Manual** | Una prueba, o el menú de pausa | `topeNormal` | Sí |

Y **solo** esos. Hubo además un guardado periódico y otro al terminar cada interacción;
se quitaron a propósito. Si vuelven a hacer falta, lo que hay que añadir es un valor
nuevo en `SaveManager.Motivo`, no reabrir los de antes.

### Encenderlos y apagarlos sin tocar código

`SaveManager.momentosActivos` es un `[Flags]` visible en el Inspector:

```csharp
[Flags] public enum Momentos { Ninguno = 0, CambioDeZona = 1, CierreDeAplicacion = 2, Manual = 4 }
```

Los valores de `Motivo` son las mismas potencias de dos, así que `Preparar()` pregunta
con un `&` y no hay ningún `switch` que haya que acordarse de ampliar. Dejarlo en
`Ninguno` es legítimo para depurar y el juego lo avisa al arrancar con un `LogWarning`.

> ⚠️ **Para que ese campo sirva hay que poner el `SaveManager` en MainScene.** Hoy se
> autocrea por código, y `GetOrCreate()` busca primero uno existente con
> `FindAnyObjectByType`, así que basta con arrastrarlo a la escena y los valores del
> Inspector mandan. Sin eso, el valor efectivo es siempre el del inicializador.

---

## 2. El recorrido de un cambio

```
  algo pasa en el juego
          │
          ▼
  XxxSync.Marcar…()                    ← no llama al backend
          │
          ▼
  ColaDeCambios.Encolar…(clave, …)     ← coalesce por clave
          │
          │   … el niño/a sigue jugando …
          │
          ▼
  SaveManager.Guardar(motivo)          ← cambio de zona / cierre
          │
          ▼
  ColaDeCambios.Vaciar(motivo, tope)   ← sale todo, en orden
          │
          ▼
  ApiManager.Send<T>()                 ← las peticiones de verdad
```

---

## 3. Las tres familias

Una cola plana perdería datos, así que el almacén es un **diccionario por clave** con
tres tratos distintos:

| Familia | Qué es | Al reencolar la misma clave | Ejemplos |
|---|---|---|---|
| **Snapshot** | Solo importa el último estado | Pisa. **No guarda datos**: guarda la orden de leerlos | `personaje`, `inventario`, `partida.progreso` |
| **Append** | Un hecho idempotente por clave | Pisa con el valor nuevo | `objeto:{id}`, `npc:{id}`, `mision:{id}`, `zona:{slug}`, `detective:{caso}` |
| **Cadena** | Varias peticiones que dependen unas de otras | Pisa | `chat:{n}` |

**Lo importante de Snapshot:** lo que se encola es un *thunk* que lee el estado vivo
**cuando la cola lo ejecuta**, no una copia de ahora. Por eso marcar sucia la mochila mil
veces cuesta lo mismo que una, y por eso siempre sube el estado final y nunca uno
intermedio.

**Lo importante de Cadena:** va **sola y esperando**, sin paralelismo, porque
`ApiManager.NpcId` y `ChatId` son estado global mutable y dos cadenas a la vez se
pisarían. Hoy solo la usa el respaldo del chat (ver §12).

Reencolar una clave **reemplaza el contenido pero conserva su posición original**. Si no,
un snapshot que se repite mucho —la posición de Otto, que se marca en cada guardado— se
iría colando por delante de hechos más viejos, y esos serían justo los que se quedan
fuera cuando vence el plazo.

### Las dos excepciones que fusionan en vez de pisar

**`zona:{slug}` fusiona por OR.** Hay dos escritores con significados opuestos:
`MisionBackendSync` manda `completada: false` al desbloquearla y
`BosqueDesconocidosManager` / `RegistrarZonaCompletada` mandan `true` al terminarla. Con
"gana el último", un desbloqueo que llegara después de un completado **degradaría la zona
en el reporte del adulto**. Completar es un camino de ida.

**`partida.progreso` fusiona por máximo.** Cada zona manda un valor absoluto al cerrarse.
Si dos se cerraran entre dos vaciados y la segunda tuviera un valor menor, el progreso
del niño/a retrocedería.

---

## 4. Qué se encola, quién lo encola y dónde acaba

| Clave | Familia | Lo pone | Acaba en |
|---|---|---|---|
| `personaje` | Snapshot | `PersonajeBackendSync.MarcarSucio()` | `PATCH /partidas/{id}/personaje/` |
| `inventario` | Snapshot | `InventarioBackendSync.MarcarSucio()` | `PUT /partidas/{id}/inventario/` |
| `partida.progreso` | Snapshot | `ColaDeCambios.EncolarProgreso()` | `PATCH /partidas/{id}/` |
| `objeto:{id}` | Append | `ObjetosRecogidosSync.Marcar()` | `POST /partidas/{id}/objetos-recogidos/` |
| `npc:{id}` | Append | `NpcTematicaSync.Marcar()` | `POST /partidas/{id}/progreso-npcs/` |
| `mision:{id}` | Append | `MisionBackendSync` | `POST /partidas/{id}/misiones/` |
| `zona:{slug}` | Append (OR) | `MisionBackendSync`, `BosqueDesconocidosManager`, `RegistrarZonaCompletada` | `POST /partidas/{id}/zonas/` |
| `detective:{caso}` | Append | `DetectiveCaseManager` | `POST /casos-detective/{caso}/progreso/` |
| `chat:{n}` | Cadena | `ChatBackendLogger.LogEnd()` | `POST /partidas/{id}/npcs/` + `POST /chats/` + N × `POST /chats/{id}/mensajes/registrar/` + `POST /chats/{id}/finalizar/` |

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
repetible) y **"Cerrar de todas formas"**. Mientras el cartel está a la vista el cierre
sigue cancelado; no hay prisa. Lo que no vale es cerrar en silencio.

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

### Los cuatro finales de un vaciado

| Estado | Qué pasó |
|---|---|
| **Subido** | Entró |
| **Fallido** | Se intentó y el servidor dijo que no |
| **Sin respuesta** | Salió y venció el plazo sin que contestara, ni bien ni mal. **No se reencola**: puede que haya llegado, y repetirlo duplicaría el cambio |
| **Sin intentar** | Se quedó en la cola sin llegar a salir |

`TodoBien` exige que los tres últimos estén en cero. Esa cuarta categoría existe porque
sin ella un envío que no llamara nunca a su callback no aparecía en ningún lado: el
resultado decía *"todo bien"* y el cierre daba por guardado un cambio recién perdido.

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

## 7. Lo que se pierde, y por qué se aceptó

1. **La cola vive solo en memoria y no se persiste a disco.** Un cierre sucio —crash,
   batería, matar el proceso— pierde lo acumulado desde el último vaciado, o sea lo de la
   zona actual. El cierre normal no pierde nada, porque se retiene la aplicación hasta
   que la cola termina.
2. **Un chat abandonado a medias ya no queda ni a medias.** Antes el backend conservaba
   los mensajes ya enviados con `fecha_termino` en NULL — a medias, pero visible en el
   reporte del adulto. Ahora: o la conversación entera o nada. Es consecuencia directa
   de (1).
3. **El aspa ahora retrasa el cierre.** Si el vaciado tiene un fallo, el juego parece
   colgado unos segundos. Por eso el tope es duro sobre `realtimeSinceStartup` y un
   segundo Alt+F4 cierra en seco.

### El sello de partida

Cada entrada se sella con `ApiManager.PartidaId` al encolar. Al vaciar, si no coincide con
la partida activa, se descarta con un aviso que la nombra. Este proyecto ya se quemó con
la contaminación entre perfiles de hermanos; con una cola que sobrevive a un cambio de
perfil, el riesgo vuelve. Descartar **no** cuenta como cambio perdido.

---

## 8. Configuración

### `SaveManager`

| Campo | Por defecto | Qué hace |
|---|---|---|
| `momentosActivos` | `CambioDeZona \| CierreDeAplicacion` | En qué momentos se vacía |
| `esperaMinima` | 1 s | Mínimo entre dos guardados. Caminar sobre el borde de dos zonas dispara cambios en cadena; esto los agrupa. **El cierre se la salta** |
| `topeNormal` | 8 s | Plazo de un vaciado por cambio de zona |
| `topeDeCierre` | 10 s | Cuánto se retiene el cierre antes de preguntar |
| `verboseLogs` | true | Escribe cada guardado y su motivo |

### `ColaDeCambios`

| Campo | Por defecto | Qué hace |
|---|---|---|
| `paralelismo` | 4 | Peticiones a la vez. Las cadenas van de una en una pase lo que pase |
| `maxIntentos` | 3 | Reintentos de un cambio que falló, cuando el vaciado admite reintento |
| `avisarPorEncimaDe` | 200 | Avisa si la cola crece tanto sin vaciarse: señal de que algún vaciado dejó de ocurrir |
| `verboseLogs` | true | Escribe cada cambio que entra y cada vaciado |

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

`Assets/Editor/FishyPruebasCola.cs` — 22 comprobaciones sobre lo que **no se ve en un log
de red**: las dos fusiones, el orden, que reencolar no adelante, el sello de partida, los
reintentos, que el tope de tiempo se restaure, que el thunk lea al vaciar y no al encolar,
los interruptores de `momentosActivos`, y que lo que sale y no contesta cuente como
perdido y no como guardado.

También está `FishyPruebasPartida.Ejecutar` (guardado e inventario en modo local).

> **Trampa del modo edición:** Unity no llama a `Awake` fuera del Play, así que los
> singletons están en null y una prueba mal montada pasa en verde sin haber vaciado nada.
> El arnés escribe `Instance` por reflexión — ver `FishyPruebasCola.FijarInstancia`, que
> explica por qué no se llama al `Awake` entero.

### Con el backend local

```bash
bash Backend/run.sh --local     # SQLite, no necesita la clave de Supabase
```

Con `verboseLogs` puesto, contar las líneas `[API] POST` / `[API] PUT` entre dos cambios
de zona: **cero durante el juego**, todas de golpe en el instante del vaciado.

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
[Cola] Vaciando por {motivo}: {n} cambios.
[Cola] Vaciada en {t} s: {n} subidos.
[Cola] Descartado '{qué}' de la partida {x}: ahora se juega la {y}.
```

Y cuando se pierde algo, **`LogError` y no `LogWarning`, listando las claves**:

```
[Cola] Vaciada por CierreDeAplicacion en 10,1 s con cambios sin guardar:
       3 subidos, 1 fallidos, 1 sin respuesta, 2 sin intentar.
       Quedan: objeto:FLOR_03, chat:2, inventario
```

En un build eso es lo único que queda en `Player.log`, y es la diferencia entre *"no se
guardó y nadie sabe por qué"* y un diagnóstico.

---

## 11. Archivos

| Archivo | Rol |
|---|---|
| `ColaDeCambios.cs` | La cola: el almacén, las tres familias, el vaciado |
| `SaveManager.cs` | Los momentos, `wantsToQuit`, el cierre retenido |
| `ApiManager.cs` | `PeticionesEnVuelo` y `TopeDeTiempoParaPeticiones` en `Send<T>` |
| `UI/MenuPausa.cs` | El botón de salir y el cartel de "sin conexión" |
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
Inspector manden (ver §1).
