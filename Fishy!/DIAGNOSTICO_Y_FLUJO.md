# Fishy! — Diagnóstico del proyecto y flujo completo

> Generado el 10-06-2026 analizando el contenido real de las escenas (`Boot.unity`,
> `SampleScene.unity`), los scripts de `Assets/Scripts/` y el backend Django.

---

## Parte 1 — Diagnóstico

### Resumen ejecutivo

El proyecto está **funcional en sus flujos principales** (login → mundo → NPCs →
chat del celular → registro en BD). Los problemas encontrados son de **montaje de
escena**, no de código: faltan piezas en `SampleScene` para el desbloqueo de zonas,
y sobra una pieza de prueba en `Boot`.

### ✅ Lo que está bien

| Verificación | Estado |
|---|---|
| Otto tiene tag `Player` y Rigidbody2D | ✅ |
| Main Camera tiene tag `MainCamera` + `CameraFollow2D` | ✅ |
| `EventSystem` presente en SampleScene | ✅ |
| 2 NPCs físicos configurados (`NPC_Alex_Fisico` = guion Alex, `NPC_Sam` = guion Sam) | ✅ |
| 2 zonas de celular configuradas (`NPC_Alex` → banco NPC_01, `NPC_Valen` → banco NPC_02, modo `SoloNpc`) | ✅ |
| `banco_preguntas.json` v1.3 en `Resources/` (26 preguntas) | ✅ |
| `ApiManager` con `useLocalMode = false` y fallback automático a modo local si el servidor no responde | ✅ |
| Auto-registro en BD al haber sesión (no depende de casillas del inspector) | ✅ |
| Backend con 5 migraciones, incluida `pregunta_banco_id` para trazabilidad | ✅ |

### ❌ Problemas encontrados

| # | Severidad | Problema | Solución |
|---|-----------|----------|----------|
| 1 | **Alta** | El GameObject **`SmokeTest`** (componente `ApiSmokeTest`) está en la escena **Boot**. En cada arranque crea un usuario de prueba + partida en la BD (contamina los datos y llena la consola). | Abrir `Boot.unity`, borrar el GameObject `SmokeTest`, guardar. |
| 2 | **Alta** | **No hay `ZonaDesconocidosManager`** en SampleScene → nadie detecta cuándo terminan los 2 NPCs físicos y nunca se desbloquea la siguiente zona. | Menú **Fishy → Configurar Zona Desconocidos** y guardar la escena (Ctrl+S). |
| 3 | **Alta** | **No hay `BlockedZone` ni `WorldZoneManager`** en SampleScene → no existe zona sombreada que desbloquear (la cinemática no tiene destino). | El mismo menú del punto 2 los crea y conecta. Luego mover/redimensionar la zona. |
| 4 | Media | **No hay `OttoMoodController`** en Otto → al cerrar el chat del celular, el estado emocional (😌/😟) se calcula pero no se refleja en el sprite/animación de Otto. | Add Component `OttoMoodController` en el GameObject Otto (opcional: asignar sprites o triggers de Animator `Seguro`/`Preocupado`). |
| 5 | Baja | En el banco hay **1 pregunta HDU-2 sin `npc_id`** (queda fuera de las conversaciones de Alex/Valen). | Revisar con el autor del banco (MLOps) si es intencional. |

### Inventario real por escena

**Boot.unity**
| GameObject | Componentes | Nota |
|---|---|---|
| Main Camera | Camera | |
| Global Light 2D | Light2D | |
| Bootstrap | `AuthScreen`, `LoadingScreen` | genera su UI en runtime |
| **SmokeTest** | `ApiSmokeTest` | ⚠️ eliminar (problema #1) |

**SampleScene.unity**
| GameObject | Componentes | Configuración |
|---|---|---|
| Otto | `OttoController`, Rigidbody2D | tag `Player` |
| └ OttoPhone | `OttoPhone` | celular físico (hijo de Otto) |
| Main Camera | Camera, `CameraFollow2D` | tag `MainCamera` |
| EventSystem | EventSystem + InputSystemUIInputModule | |
| Grid / Suelo / Objetos | Tilemaps | escenario |
| NPC_Alex_Fisico | `DesconocidosNPC` | guion **Alex** (en persona) |
| NPC_Sam | `DesconocidosNPC` | guion **Sam** (en persona) |
| NPC_Alex | `PhoneChatLauncher` | `SoloNpc` · banco `NPC_01` |
| NPC_Valen | `PhoneChatLauncher` | `SoloNpc` · banco `NPC_02` |

**Objetos que NO están en la escena porque se crean solos en runtime** (patrón
`GetOrCreate` / autocreación): `ApiManager` (lo crea AuthScreen y persiste con
DontDestroyOnLoad), `DialogueController`, `DialogueUI`, `ChatModuleController`,
`ChatModuleUI`, `PhoneZoomController`, `ZonePopupUI`, `ZoneUnlockCinematic`.
**No hay que añadirlos a mano.**

---

## Parte 2 — Flujo en Unity

### 2.1 Arranque (escena Boot)

```
Play en Boot
│
├─ AuthScreen.Awake()
│   ├─ crea ApiManager si no existe (DontDestroyOnLoad)
│   ├─ si YA hay sesión (volver al menú) → salta directo al juego
│   └─ CheckHealth(): GET /api/health/ con timeout 4 s
│        ├─ responde  → badge 🟢 Conectado (modo BD real)
│        └─ no responde → useLocalMode = true, badge 🔴 (modo offline)
│
├─ El jugador escribe nombre + contraseña y pulsa "Entrar"
│   └─ Smart login:
│        1. POST /auth/login/        → OK → continúa
│        2. si falla → POST /auth/registro/ (auto-registro)
│        3. si "ya existe" → "Contraseña incorrecta."
│
├─ OnAuthSuccess()
│   ├─ ResetSessionState() limpió PartidaId/NpcId/ChatId viejos
│   └─ POST /partidas/ → guarda PartidaId (la sesión de juego en la BD)
│
└─ LoadingScreen.LoadScene("SampleScene")   (barra con progreso real)
```

**Estado que persiste entre escenas** (en `ApiManager`, DontDestroyOnLoad):
`Token`, `UsuarioId`, `PartidaId`, `NpcId`, `ChatId`.

### 2.2 Mundo (SampleScene): los tres tipos de encuentro

#### A. NPC físico de grooming (NPC_Alex_Fisico, NPC_Sam) — HDU-2

```
Otto entra al trigger del NPC
└─ DesconocidosNPC.OnTriggerEnter2D
   └─ DialogueController.StartConversation()
      ├─ bloquea movimiento de Otto
      ├─ GroomingChatLogger.Begin() si hay sesión (AUTOMÁTICO)
      │    └─ POST /partidas/{id}/npcs/  +  POST /chats/
      │
      ├─ FASE CONFIANZA (a0–a1 / s0–s1): solo halagos.
      │    Ambas respuestas continúan — NO se puede cortar todavía.
      ├─ FASE RIESGO (a2+ / s2+): pide nombre → colegio/edad → dirección/secreto.
      │    Cada nodo ofrece negarse (Buena) o compartir (Mala).
      │    Cada elección → POST mensajes/registrar/ (tipo chain + calidad)
      │
      ├─ EndSuccess (se negó) → LogOutcome(true)  → PATCH npc confianza=0
      │    el NPC se aleja y se desactiva
      └─ EndCaptured (compartió todo) → LogOutcome(false) → PATCH confianza=100
           cierre educativo antes de cerrar
└─ DesconocidosNPC.OnConversationFinished
   └─ ZonaDesconocidosManager.NotifyNpcFinished  → "Avance: 1/2 NPCs"
```

#### B. Zona de celular (NPC_Alex, NPC_Valen) — HDU-2 vía teléfono

```
Otto entra al trigger de la zona
└─ PhoneChatLauncher.PhoneSequence()
   1. encuentra/crea OttoPhone (hijo de Otto)
   2. bloquea movimiento
   3. notificación "📱 Tienes un mensaje nuevo…"
   4. el celular VIBRA (animación física)
   5. espera previewDuration
   6. PhoneZoomController: zoom de cámara al celular + fade a negro
   7. ChatModuleUI en MODO TELÉFONO (bisel, notch, reloj, status bar)
      └─ conversación del banco de preguntas (NPC_01=Alex / NPC_02=Valen)
         · cada pregunta es un nodo cuyo id = pregunta_banco_id
         · cada opción muestra su consecuencia narrativa (nodo CONS_)
         · cada elección → POST mensajes/registrar/ con pregunta_banco_id
   8. al cerrar: % de respuestas seguras → estado de Otto (😌 ≥70 % / 😟)
   9. pantalla del celular se apaga
   10. zoom de regreso al mundo + fade
   11. Otto recupera el movimiento
```

#### C. Desbloqueo de zona (cinemática) — requiere problemas #2 y #3 resueltos

```
Termina el 2º NPC físico
└─ ZonaDesconocidosManager.CompleteTheme()
   ├─ log resumen (a salvo=X, capturas=Y)
   ├─ ZoneUnlockCinematic.Play(zona):
   │    1. paneo + zoom de cámara a la zona sombreada
   │    2. cartel "✨ ¡Nueva zona desbloqueada!"
   │    3. el oscurecido se desvanece (la zona "se ilumina")
   │    4. zone.Unlock() desactiva los colliders (Otto ya puede entrar)
   │    5. paneo de regreso a Otto
   └─ PATCH /partidas/{id}/ progreso=25  (registro del hito en la BD)
```

### 2.3 Cadena de gatillantes

```
2 NPCs físicos terminados ──► zona_2 desbloqueada ──► (siguiente temática…)
        │                            │
   NPC.confianza en BD        Partida.progreso en BD
   (0=a salvo, 100=captura)
```

---

## Parte 3 — Flujo en el backend

### 3.1 Infraestructura

```
Docker Compose
├─ backend-db-1   PostgreSQL 16   puerto 5433 (host) → 5432 (contenedor)
└─ backend-web-1  Django 6 + DRF  puerto 8000        (migra al arrancar)
```

Comandos de operación:
```bash
docker start backend-db-1 backend-web-1    # uso diario
curl http://127.0.0.1:8000/api/health/     # → {"status":"ok"}
docker compose exec web python manage.py cargar_banco   # cargar banco en BD
# ⚠️ nunca `docker compose down -v` — borra la base de datos
```

### 3.2 Modelo de datos (qué guarda cada tabla)

```
Usuario          quién juega (nombre único + password hasheada)
  └─ Partida     una sesión de juego (progreso 0-100, nivel_riesgo)
       ├─ NPC    cada personaje con quien interactuó
       │           tipo: enemigo · confianza: 0=a salvo / 100=capturado
       └─ Chat   cada conversación (categoria_riesgo, fechas inicio/término)
            └─ Mensaje  cada línea del chat:
                 tipo: start | request | chain | end
                 calidad_respuesta: buena | neutral | mala   (en chain)
                 pregunta_banco_id: ej. "HDU2_NPC01_F2_Q01"  (trazabilidad)
                 └─ PosibleRespuesta  las opciones que se le mostraron

PreguntaBanco / OpcionBanco   espejo del banco_preguntas.json (consulta/ML)
```

### 3.3 Secuencia de llamadas de una sesión completa

| Paso del juego | Llamada HTTP | Tabla afectada |
|---|---|---|
| Pantalla de login | `POST /api/auth/login/` (o `/registro/`) | Usuario (+ Token) |
| Entrar al juego | `POST /api/partidas/` | Partida |
| Acercarse a un NPC / abrir chat | `POST /api/partidas/{id}/npcs/` | NPC |
| Inicia la conversación | `POST /api/chats/` | Chat |
| Primer mensaje del NPC | `POST /api/chats/{id}/mensajes/registrar/` `tipo=start` | Mensaje |
| Mensaje con opciones | ídem `tipo=request` + `posibles_respuestas` | Mensaje + PosibleRespuesta |
| El jugador elige | ídem `tipo=chain` + `calidad_respuesta` + `pregunta_banco_id` | Mensaje |
| Desenlace del NPC físico | `PATCH /api/npcs/{id}/` `confianza=0/100` | NPC |
| Cierre del chat | `POST /api/chats/{id}/finalizar/` (crea `end`) | Mensaje + Chat.fecha_termino |
| Zona desbloqueada | `PATCH /api/partidas/{id}/` `progreso=25` | Partida |

Todas las llamadas autenticadas llevan `Authorization: Token <token>`. Todas las
escrituras desde Unity son **best-effort**: si el backend falla, el juego continúa
y solo se pierde el registro (nunca se bloquea la partida).

### 3.4 Trazabilidad pedagógica

`Mensaje.pregunta_banco_id` enlaza cada respuesta con la pregunta exacta del banco:

```sql
-- ¿Qué porcentaje eligió respuestas inseguras en cada pregunta?
SELECT pregunta_banco_id,
       COUNT(*) FILTER (WHERE calidad_respuesta = 'mala') * 100.0 / COUNT(*) AS pct_inseguras
FROM api_mensaje
WHERE tipo = 'chain' AND pregunta_banco_id IS NOT NULL
GROUP BY pregunta_banco_id;
```

Los diálogos de los NPC físicos (guiones Alex/Sam, no provienen del banco) se
registran con `pregunta_banco_id = NULL`; su desenlace queda en `NPC.confianza`.

### 3.5 Banco de preguntas (contenido actual, v1.3 — 26 preguntas)

| Grupo | Preguntas | Usado por |
|---|---|---|
| HDU-2 · NPC_01 (Alex) | 9 | zona celular `NPC_Alex` |
| HDU-2 · NPC_02 (Valen) | 10 | zona celular `NPC_Valen` |
| HDU-2 · sin npc_id | 1 | ⚠️ huérfana (problema #5) |
| HDU-8 · Grooming / Ciberacoso / Reto viral | 2 c/u | zonas `ZonaChatSimulado` (sin montar aún) |

---

## Parte 4 — Checklist para dejar todo operativo

1. [ ] Abrir `Boot.unity` → **eliminar el GameObject `SmokeTest`** → guardar.
2. [ ] Abrir `SampleScene` → menú **Fishy → Configurar Zona Desconocidos** →
       mover/redimensionar la zona → clic derecho en `BlockedZone` →
       *Ajustar overlay al collider* → **guardar (Ctrl+S)**.
3. [ ] (Opcional) Añadir `OttoMoodController` al GameObject Otto.
4. [ ] Levantar el backend (`docker start backend-db-1 backend-web-1`) antes de Play.
5. [ ] Probar el ciclo completo: login → hablar con los 2 NPCs físicos →
       ver la cinemática → revisar la BD (`/api/partidas/{id}/npcs/`).
