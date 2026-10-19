# Fishy! — Diagnóstico del proyecto y flujo completo

> Generado el 10-06-2026 analizando el contenido real de las escenas (`Boot.unity`,
> `SampleScene.unity` — renombrada desde entonces a `Assets/Scenes/MainScene.unity`),
> los scripts de `Assets/Scripts/` y el backend Django.
>
> **Actualizado el 16-09-2026** contra el estado actual del proyecto: catálogo de
> misiones con auto-conexión, modo Detective con recompensas de álbum, control
> parental (cuenta de adulto + perfiles de menor), sistema de guardado reescrito
> alrededor de una cola de cambios (HDU-15) y unificación del módulo de chat.
> Donde el contenido original seguía vigente se dejó igual; donde no, se
> reescribió con la misma estructura.

---

## Parte 1 — Diagnóstico

### Resumen ejecutivo

El proyecto sigue **funcional en sus flujos principales** (login → mundo → NPCs →
chat del celular/módulo unificado → registro en BD), pero creció mucho desde el
diagnóstico original: control parental de dos cuentas, catálogo de misiones,
modo Detective y un sistema de guardado por cola en vez de escritura inmediata.

De los 5 problemas listados el 10-06-2026, **3 ya están resueltos** por el propio
desarrollo del juego (desbloqueo de zona montado, banco sin preguntas huérfanas)
y **2 siguen vigentes** (`SmokeTest` en `Boot.unity`, `OttoMoodController` sin
montar en Otto). Ver el detalle en la tabla de abajo.

Además, **`Boot.unity` —la escena en la que se ancla este documento— está
deshabilitada en File → Build Settings.** El flujo que de verdad viaja en el
build hoy es `MenuUno → Ingresar.unity → MenuDos → MainScene` (script `iniciar`,
en `Assets/Scripts/Menu/iniciar.cs`). Las dos puertas de entrada implementan el
mismo modelo de control parental de dos pasos y son funcionalmente equivalentes;
un cambio de flujo de login hay que aplicarlo en las dos. La Parte 2.1 describe
la de `Boot.unity`/`AuthScreen` porque es la que este documento sigue cubriendo
en detalle, con una nota sobre la otra.

### ✅ Lo que está bien

| Verificación | Estado |
|---|---|
| Otto tiene tag `Player` y Rigidbody2D | ✅ |
| Main Camera tiene tag `MainCamera` + `CameraFollow2D` | ✅ |
| `EventSystem` presente en MainScene | ✅ |
| `WorldZoneManager` y varias `BlockedZone`/`ZonaBloqueada_*` ya montadas en MainScene | ✅ |
| `BosqueDesconocidosManager` (ex `ZonaDesconocidosManager`) presente y conectado | ✅ |
| `banco_preguntas.json` v2.5 en `Resources/` (50 preguntas, sin huérfanas) | ✅ |
| `ApiManager` con `useLocalMode = false` y fallback automático a modo local si el servidor no responde | ✅ |
| Escrituras al backend vía cola (`ColaDeCambios` + `SaveManager`, HDU-15): no dependen de casillas del inspector | ✅ |
| Backend con 12 migraciones y modelo de control parental (`AdultoResponsable` → `UsuarioJugador`) | ✅ |

### ❌ Problemas encontrados

| # | Severidad | Problema | Estado / Solución |
|---|-----------|----------|----------|
| 1 | **Alta** | El GameObject **`SmokeTest`** (componente `ApiSmokeTest`) sigue en la escena **Boot** (ahora colgando de `Main Camera`). Si se juega `Boot.unity` directo crea un usuario de prueba + partida en la BD. | **Sigue pendiente.** Impacto reducido porque `Boot.unity` está deshabilitada en Build Settings (no viaja en el juego empaquetado), pero sigue contaminando datos al probar la escena en el editor. Abrir `Boot.unity`, borrar el GameObject `SmokeTest`, guardar. |
| 2 | ~~Alta~~ | ~~No hay `ZonaDesconocidosManager` en la escena.~~ | **Resuelto.** `BosqueDesconocidosManager` está montado y conectado en `MainScene`. |
| 3 | ~~Alta~~ | ~~No hay `BlockedZone` ni `WorldZoneManager`.~~ | **Resuelto.** `WorldZoneManager` y varias zonas bloqueadas (`ZonaBloqueada_zona_2`, `ZonaBloqueada_zona_3`, `BlockedArea`) ya están en `MainScene`. |
| 4 | Media | **No hay `OttoMoodController`** en Otto → el estado emocional (😌/😟) se calcula al cerrar un chat pero no se refleja en el sprite/animación de Otto. | **Sigue pendiente.** Add Component `OttoMoodController` en el GameObject Otto (opcional: asignar sprites o triggers de Animator `Seguro`/`Preocupado`). |
| 5 | ~~Baja~~ | ~~1 pregunta HDU-2 sin `npc_id` en el banco.~~ | **Resuelto.** El banco actual (v2.5, 50 preguntas) no tiene preguntas sin `npc_id`. |

### Inventario real por escena

**Boot.unity** (deshabilitada en Build Settings — ver Resumen ejecutivo)
| GameObject | Componentes | Nota |
|---|---|---|
| Main Camera | Camera, `AuthScreen`, `LoadingScreen` | genera su UI en runtime; ya no hay un GameObject `Bootstrap` separado, `AuthScreen`/`LoadingScreen` cuelgan directo de la cámara |
| Global Light 2D | Light2D | |
| **SmokeTest** | `ApiSmokeTest` | ⚠️ eliminar (problema #1) |

**MainScene.unity** (ex `SampleScene.unity`)

La escena dejó de ser un mapa único con 2 NPCs: ahora agrupa varias zonas
temáticas bajo sus propios GameObjects raíz (`Arrecife`, `Bosque`, `Pantano`, y
las que sigan según `Resources/misiones.json`), cada una con su manager y sus
propios NPCs físicos (`BosqueDesconocidosNPC`) y/o zonas de celular
(`PhoneChatLauncher`). Piezas que siguen fijas a nivel de escena, no de zona:

| GameObject | Componentes | Configuración |
|---|---|---|
| Otto | `OttoController`, Rigidbody2D | tag `Player` |
| Main Camera | Camera, `CameraFollow2D` | tag `MainCamera` |
| EventSystem | EventSystem + InputSystemUIInputModule | |
| WorldZoneManager | `WorldZoneManager` | coordina las `BlockedZone` de la escena |
| `ZonaBloqueada_zona_2`, `ZonaBloqueada_zona_3`, `BlockedArea` (×2) | `BlockedZone` | zonas sombreadas a desbloquear |
| `BosqueDesconocidosManager` (dentro de `Bosque`) | `BosqueDesconocidosManager` | cuenta los NPCs físicos del Bosque |
| `Mision Listener`, `Dador de mision inicial`, `MisionOnStep`, `MisionArrecife` | disparadores del catálogo de misiones | ver Parte 2.3 |

**Objetos que NO están en la escena porque se crean solos en runtime** (patrón
`GetOrCreate`/`RuntimeInitializeOnLoadMethod`): la lista creció mucho desde el
diagnóstico original. Siguen `ApiManager` (lo crea `AuthScreen`, DontDestroyOnLoad),
`ChatModuleController`/`ChatModuleUI`, `PhoneZoomController`, `ZonePopupUI`,
`ZoneUnlockCinematic`; se sumaron `SaveManager`, `ColaDeCambios`, `MisionCatalogoSync`,
`ConexionAutomaticaMisiones`, `MissionUIController`/`MissionManager`,
`DisparadorDeMision`, y del lado Detective `DetectiveUI`/`DetectiveRewardPopup`.
**No hay que añadirlos a mano.**

---

## Parte 2 — Flujo en Unity

### 2.1 Arranque (escena Boot)

> Ver también el Resumen ejecutivo: esta es una de las dos puertas de entrada del
> proyecto y la única que este documento sigue en detalle. La otra
> (`MenuUno → Ingresar.unity → MenuDos → MainScene`, script `iniciar.cs`) es la
> que hoy viaja en el build; implementa el mismo modelo de dos pasos descrito
> abajo. Un cambio de flujo de login hay que aplicarlo en las dos.

```
Play en Boot
│
├─ AuthScreen.Awake()
│   ├─ crea ApiManager si no existe (DontDestroyOnLoad)
│   ├─ si YA hay sesión (volver al menú) → salta directo al paso correspondiente
│   └─ CheckHealth(): GET /api/health/ con timeout ~4 s
│        ├─ responde  → badge 🟢 Conectado (modo BD real)
│        └─ no responde → useLocalMode = true, badge 🔴 (modo offline, PlayerPrefs)
│
├─ PASO 1 — Cuenta del adulto responsable (modelo AdultoResponsable)
│   ├─ modo Login: nombre + contraseña → POST /auth/login/
│   └─ modo Registro: nombre + email + contraseña → POST /auth/registro/
│        (ya no existe el "login inteligente" que auto-registraba al fallar el
│         login: el email es obligatorio y único, así que login y registro son
│         dos modos separados en la UI, no un intento en cascada)
│
├─ PASO 2 — Perfil de menor (modelo UsuarioJugador)
│   ├─ GET /jugadores/ → perfiles del adulto (crea el primero si no hay ninguno)
│   └─ se elige/crea un perfil → SeleccionarJugador() fija JugadorId
│        (fijar el perfil ANTES de pedir sus partidas es a propósito: cambiar de
│         menor descarta el estado de sesión anterior)
│
├─ PASO 3 — Continuar o empezar partida
│   ├─ GET /jugadores/{id}/partidas/
│   ├─ sin partidas guardadas → POST /partidas/ y entra directo (no se ofrece
│   │    "Continuar" con una lista vacía)
│   └─ con partidas → lista las N más recientes (más reciente primero); el
│        jugador elige una o pulsa "Empezar partida nueva"
│
└─ StartGame() → LoadingScreen.LoadScene("MainScene")   (barra con progreso real)
```

**Estado que persiste entre escenas** (en `ApiManager`, DontDestroyOnLoad):
`Token`, `JugadorId` (perfil de menor elegido), `PartidaId`, `NpcId`, `ChatId`.

### 2.2 Mundo (MainScene): los encuentros

El juego pasó de una escena única con 2 NPCs a **varias zonas temáticas** dentro
de `MainScene` (`Arrecife`, `Bosque` —la ex "Desconocidos"—, `Pantano`, y las que
sigan según el catálogo de misiones). Además, **el diálogo ya no tiene dos
implementaciones separadas**: tanto el NPC físico como la conversación de
celular corren sobre el mismo módulo de chat (`ChatModuleLauncher` →
`ChatModuleController` → `ChatModuleUI`), presentado distinto según el caso
(burbuja en el mundo vs. pantalla de celular con zoom). `DialogueController` y
`GroomingChatLogger`, que hacían este trabajo en el diagnóstico original, **ya
no existen en el proyecto** — los reemplazó este módulo genérico más
`ChatBackendLogger`. `ChatModuleLauncher` puede tomar su contenido de tres
fuentes (`source`): conversaciones fijas asignadas a mano, el banco de
preguntas por `npc_id`, o el set por defecto de la zona — así que un NPC físico
puede usar el mismo banco que antes solo usaban las zonas de celular.

#### A. NPC físico de zona (p. ej. Bosque de los Desconocidos) — HDU-2

```
Otto entra al trigger del NPC
└─ ChatModuleLauncher (en el mismo GameObject que BosqueDesconocidosNPC)
   └─ ChatModuleController.OpenSession()
      ├─ bloquea movimiento de Otto
      ├─ corre la conversación (banco de preguntas o conversación asignada)
      │    cada elección la anota ChatBackendLogger EN MEMORIA
      │    (ya no hay un POST por elección — ver 2.2.D y Parte 3.3)
      └─ al cerrar la sesión:
           ├─ ChatBackendLogger.LogEnd() encola la conversación COMPLETA en
           │    ColaDeCambios (familia "Cadena": chat + mensajes de un tirón)
           └─ BosqueDesconocidosNPC decide éxito/fracaso por el % de respuestas
                seguras (umbralExito), aleja al NPC si corresponde, y avisa a
                BosqueDesconocidosManager → "Avance: N/M NPCs"
```

#### B. Zona de celular (`PhoneChatLauncher`) — HDU-2/HDU-8 vía teléfono

```
Otto entra al trigger de la zona
└─ PhoneChatLauncher.PhoneSequence()
   1. encuentra/crea OttoPhone (hijo de Otto)
   2. bloquea movimiento
   3. notificación "📱 Tienes un mensaje nuevo…" + vibración del celular
   4. espera previewDuration
   5. PhoneZoomController: zoom de cámara al celular + fade a negro
   6. ChatModuleUI en MODO TELÉFONO (mismo módulo que 2.2.A, con el tema visual
      de celular: bisel, notch, reloj, status bar)
        └─ conversación del banco de preguntas (banco_preguntas.json, ver 3.5)
   7. al cerrar: % de respuestas seguras → OttoMoodController (si está montado;
      ver problema #4)
   8. zoom de regreso al mundo + fade, Otto recupera el movimiento
```

*(El diagnóstico original documentaba `NPC_Alex`/`NPC_Valen` como las únicas dos
zonas de celular, sobre el banco `NPC_01`/`NPC_02`. El banco creció a 50
preguntas en 9 NPCs más guías y testimonios, repartidas en 3 zonas temáticas
— ver Parte 3.5 —, así que hoy hay más zonas de celular que las que caben en un
diagrama corto; el mecanismo del flujo es el mismo.)*

#### C. Modo Detective (HDU-10) — nuevo desde el diagnóstico original

```
Otto entra al trigger de DetectiveLauncher
└─ carga el caso (CasoDetective en backend, o Resources/<caso>.json de respaldo
     si no hay sesión/backend — y ahí el resultado NO se guarda)
   └─ DetectiveCaseManager: el jugador marca los mensajes sospechosos de un
        chat simulado; % de aciertos decide el resultado
        ├─ < 50% de aciertos → se puede ofrecer repetir el caso
        └─ ≥ umbral de recompensa → CatalogoRecompensasDetective entrega un
             ítem de álbum (pin), mostrado por DetectiveRewardPopup
   Si hay sesión, el progreso queda en CasoDetective / MensajeDetective /
   CasoDetectiveProgreso, y la recompensa en RecompensaAlbum / RecompensaObtenida.
```

#### D. Desbloqueo de zona (cinemática) — ya montado en MainScene

```
Termina la temática de una zona (p. ej. los NPCs del Bosque)
└─ BosqueDesconocidosManager.CompleteTheme()
   ├─ log resumen (a salvo=X, capturas=Y)
   ├─ ZoneUnlockCinematic.Play(zona):
   │    1. paneo + zoom de cámara a la zona sombreada (BlockedZone)
   │    2. cartel "✨ ¡Nueva zona desbloqueada!"
   │    3. el oscurecido se desvanece y zone.Unlock() desactiva los colliders
   │    4. paneo de regreso a Otto
   └─ RegistrarZonaCompletada encola el hito en ColaDeCambios (familia "Zona")
        — WorldZoneManager y las BlockedZone ya están montadas en la escena, así
        que el problema original ("no hay zona que desbloquear") está resuelto.
```

### 2.3 Cadena de gatillantes

```
NPCs de una temática terminados ──► zona desbloqueada ──► siguiente misión del catálogo
        │                                  │                        │
   NpcProgreso en BD              ZonaProgreso / Partida.progreso   ConexionAutomaticaMisiones
   (confianza/estado)                    en BD                     entrega recompensa_item_id
                                                                     y/o desbloquea_mision
                                                                     (Resources/misiones.json)
```

El catálogo de misiones (`Resources/misiones.json`, HDU-1/HDU-16) es la pieza
nueva desde el diagnóstico original: antes el desbloqueo de zona era el único
gatillante documentado; ahora `MisionCatalogoSync` + `ConexionAutomaticaMisiones`
+ `EntregarMisionDelCatalogo` encadenan misión → recompensa → siguiente misión
sin tocar la escena, y `DisparadorDeMision` detecta tanto lo que se completa en
vivo como lo que ya venía completo al restaurar una partida guardada.

---

## Parte 3 — Flujo en el backend

### 3.1 Infraestructura

```
web   Django 6 + DRF, puerto 8000 (migra al arrancar)
  └─ se conecta por red a Supabase (Postgres administrado, proyecto cgolqpchpvyuvnyejsyq)
```

Desde la migración a Supabase (`Backend/MIGRACION_SUPABASE.md`, 09-08-2026) **ya
no hay un contenedor de base de datos local**: se eliminó el servicio `db` de
`docker-compose.yml` junto con el volumen `postgres_data`. El contenedor `web`
lee las credenciales de `.env` (plantilla versionada en `.env.example`; `.env`
no se commitea) y se conecta a Supabase con `sslmode=require`. En el día a día
el backend se corre más seguido con el venv (`./run.sh`) que con Docker.

Comandos de operación:
```bash
cd Backend/backend && ./run.sh              # servidor en 127.0.0.1:8000 (venv, contra Supabase)
# alternativa: docker compose up --build    (levanta solo el contenedor "web")
curl http://127.0.0.1:8000/api/health/      # → {"status":"ok"}
python manage.py cargar_banco               # carga el banco en BD (no funciona dentro de Docker)
# ⚠️ las credenciales de Supabase viven en Backend/backend/.env — nunca se commitea
```

### 3.2 Modelo de datos (qué guarda cada tabla)

El modelo creció de 8 a más de 20 tablas desde el diagnóstico original, sobre
todo por el control parental, el modo Detective y el catálogo de
misiones/inventario:

```
AdultoResponsable   la cuenta que se loguea (AUTH_USER_MODEL; nombre único + email + password)
  └─ UsuarioJugador  un perfil de menor del adulto (sin login propio)
       └─ Partida    una sesión de juego (progreso 0-100, nivel_riesgo, zona_actual)
            ├─ PersonajeJugador                 posición/apariencia de Otto en esa partida
            ├─ NPC                              cada personaje con quien interactuó
            │                                     tipo: enemigo · confianza: 0=a salvo / 100=capturado
            ├─ Chat                              cada conversación (categoria_riesgo, fechas)
            │    └─ Mensaje                      tipo: start | request | chain | end
            │                                      calidad_respuesta: buena | neutral | mala
            │                                      pregunta_banco_id / opcion_banco_id (trazabilidad)
            │         └─ PosibleRespuesta         las opciones que se le mostraron
            ├─ MisionProgreso, ZonaProgreso, NpcProgreso   avance del catálogo
            ├─ ItemInventario, ObjetoRecogido              mochila
            ├─ CasoDetective → MensajeDetective, CasoDetectiveProgreso   modo Detective
            └─ RecompensaAlbum / RecompensaObtenida        pines del álbum (HDU-11)

PreguntaBanco / OpcionBanco   espejo del banco_preguntas.json (consulta/ML)
Mision / DialogoNPC / Zona    espejo de los catálogos (misiones.json, diálogos neutros)
NivelRiesgo                   catálogo de niveles de riesgo
```

### 3.3 Cuándo se sube cada cosa

Desde HDU-15 (`SaveManager` + `ColaDeCambios`) **la mayoría de las escrituras ya
no son un POST inmediato por evento del juego**, a diferencia de lo que describía
el diagnóstico original. Cada sincronizador (posición, inventario, objetos
recogidos, NPC, misión, chat) encola su cambio en memoria, y `SaveManager` lo
sube recién en dos momentos: **al cambiar de zona y al cerrar el juego.**

| Qué encola el cambio | Familia en `ColaDeCambios` | Llamada HTTP que sale al vaciar |
|---|---|---|
| Login / registro / elegir perfil / crear o continuar partida | (inmediato: define la sesión, no pasa por la cola) | `POST /auth/login/` o `/registro/`, `GET/POST /jugadores/`, `POST /partidas/` |
| Posición e inventario de Otto | Snapshot (solo importa el último valor) | escrituras a `personaje`/`inventario` de la partida |
| Objeto recogido, confianza de NPC, misión completada | Append (idempotente; reencolar pisa con el valor nuevo) | endpoints de objetos recogidos / NPC / misión |
| Zona completada o desbloqueada | Append fusionado (se manda el acumulado, no cada evento) | endpoint de zonas de la partida |
| Una conversación (NPC físico o celular) | Cadena (varias peticiones dependientes, una por una) | `POST /chats/` → `POST /chats/{id}/mensajes/registrar/` ×N → `POST /chats/{id}/finalizar/` |

Todas las llamadas autenticadas llevan `Authorization: Token <token>`. Si el
backend falla al vaciar la cola, el intento queda contado y el juego sigue —
nunca se bloquea la partida —, pero a diferencia del diagnóstico original **lo
que se pierde ahora es lo acumulado desde el último vaciado** (la cola vive solo
en memoria, no se persiste a disco): un cierre sucio del proceso o una
conversación sin terminar puede perder toda la zona actual, no solo el último
mensaje.

### 3.4 Trazabilidad pedagógica

`Mensaje.pregunta_banco_id` (y ahora también `opcion_banco_id`, que además
permite acumular riesgo por zona resolviendo contra `OpcionBanco`) enlaza cada
respuesta con la pregunta/opción exacta del banco:

```sql
-- ¿Qué porcentaje eligió respuestas inseguras en cada pregunta?
SELECT pregunta_banco_id,
       COUNT(*) FILTER (WHERE calidad_respuesta = 'mala') * 100.0 / COUNT(*) AS pct_inseguras
FROM api_mensaje
WHERE tipo = 'chain' AND pregunta_banco_id IS NOT NULL
GROUP BY pregunta_banco_id;
```

A diferencia del diagnóstico original, un NPC físico ya no implica
necesariamente un guion fijo sin `pregunta_banco_id`: `ChatModuleLauncher`
puede configurarse para tomar sus preguntas del banco (`source =
BancoPorNpcId`) igual que una zona de celular. Solo los NPCs configurados con
una conversación fija (`source = ConversacionesAsignadas`) siguen registrando
`pregunta_banco_id = NULL`, con su desenlace en `NPC.confianza`.

### 3.5 Banco de preguntas (contenido actual, v2.5 — 50 preguntas)

| Zona (temática) | Preguntas | NPCs / roles |
|---|---|---|
| `desconocidos` (grooming, HDU-2) | 17 | NPC_01/NPC_02 Puma, NPC_07 Puma 2, NPC_GUIA Huemul |
| `ciberacoso` (HDU-8) | 16 | NPC_03 Flamenco, NPC_08 Pato Juarjual 2, NPC_GUIA_2 Coipo, NPC_TESTIMONIOS Pato Juarjual |
| `reto_viral` (HDU-8) | 17 | NPC_05/NPC_06/NPC_09 Lobo Marino, NPC_GUIA_3 Foca de Weddell |

Las tres zonas del banco original (`ZonaChatSimulado`, marcadas "sin montar
aún") están completas hoy: cada una tiene sus propios NPCs, guías y
testimonios, no solo los 2 ejemplos por temática del diagnóstico original.
Tampoco quedan preguntas sin `npc_id` (problema #5, resuelto).

---

## Parte 4 — Checklist para dejar todo operativo

1. [ ] (Solo si se prueba `Boot.unity` directo) Abrir `Boot.unity` → **eliminar
       el GameObject `SmokeTest`** (ahora colgando de `Main Camera`) → guardar.
       No es urgente para el build empaquetado: `Boot.unity` está deshabilitada
       en Build Settings y el juego se entra por `MenuUno → Ingresar → MenuDos`.
2. [ ] (Opcional) Añadir `OttoMoodController` al GameObject Otto para que el
       estado emocional de Otto (😌/😟) se refleje en su sprite/animación.
3. [ ] Preparar el backend: `.env` con las credenciales de Supabase
       (`Backend/backend/.env`, no versionado) y `./run.sh` o
       `docker compose up --build` antes de Play.
4. [ ] Probar el ciclo completo: cuenta de adulto → perfil de menor →
       continuar/empezar partida → hablar con los NPCs de una zona → ver la
       cinemática de desbloqueo → revisar la BD.
