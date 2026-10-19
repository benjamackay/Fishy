# Flujos Conversacionales — Fishy!

**Autor:** Luis González — MLOps  
**Sprint:** 1 · **Fecha:** 2026-08-26  
**HDUs:** HDU-01 · HDU-02 · HDU-03 · HDU-04 · HDU-09 · HDU-10

Referencia visual de todos los árboles de decisión y flujos de ejecución.  
La fuente de verdad de textos está en `banco_preguntas.json`, `detective_cases.json` y `dialogos_propuestos.tex`.

---

## HDU-01 · Interacción con NPC neutro

Los NPCs neutros no tienen opciones de respuesta. El jugador avanza con el botón de interacción (E) y recibe una pista de misión al cerrar el diálogo.

> **Scope MLOps:** este flujo cubre CA3 (diálogo NPC con pista) y CA4 (misión que desbloquea el NPC). CA1/CA2 (recolección de objetos) y CA5/CA6 (panel de misiones) son responsabilidad de Unity/Benjamín.

```
JUGADOR SE ACERCA A NPC NEUTRO
          │
          ▼ (presiona E)
  NPC despliega diálogo
  ┌──────────────────────────────────┐
  │  NPC Huemul (Guía)               │
  │  Línea 1 → Línea 2 → Línea 3    │
  │  → MISION_EXPLORACION_01         │
  └──────────────────────────────────┘
  ┌──────────────────────────────────┐
  │  Misiones secundarias (6, una    │
  │  por NPC): Huemul, Pudú, Coipo,  │
  │  Flamenco, Pingüino, Lobo Marino │
  │  → recompensa de álbum (HDU11/12)│
  └──────────────────────────────────┘
          │
          ▼
  Pista de misión mostrada (CA3)
          │
          ▼
  MissionManager registra misión disponible (CA4) ← Unity/Benjamín
```

**Archivos:** `dialogos_propuestos.tex` § HDU01 · `banco_preguntas.json` § `dialogos_npc_neutros`

---

## HDU-02 · Zona Desconocidos (Bosque de los Desconocidos)

Única zona con **sub-decisiones anidadas**: cada opción del primer turno abre una *decisión de cierre* o una *decisión de reacción*. En esas sub-decisiones la opción óptima vale **+1** (no +2) — ver nota de puntuación en `BANCO_PREGUNTAS.md`.

### Misión 1 — Una amistad inesperada (Puma)

```
HDU2_NPC01_Q01
"¡hola pequeño detective! ... mantengamos esto como un secreto solo entre
 nosotros dos, ¿ya? no le cuentes a nadie"
          │
   ┌──────┴───────────────┬───────────────────────┐
   R1 segura_optima       R2 segura_basica         R3 insegura
   "No guardo secretos,   "Bueno, pero dime        "¡Trato hecho!
    le preguntaré a        primero la pista"        Es nuestro secreto"
    Huemul"
   +2 pts                 +1 pt                    -1 pt
   │                      │                        │
   ▼                      ▼                        ▼
HDU2_NPC01_Q01_OPTIMA  HDU2_NPC01_Q01_BASICA   HDU2_NPC01_Q01_INSEGURA
(insistencia)          (manipul. de confianza)  (escalada de riesgo)
"¡no seas exagerado!"  "prométeme que no le     "ven ahora mismo al claro
                        dirás a Huemul..."       oscuro, pero ven solo"
   │                      │                        │
 ┌─┴──────────┐         ┌─┴──────────┐           ┌─┴──────────┐
 R1 óptima    R2 básica  R1 óptima    R2 insegura  R1 óptima    R2 insegura
 +1 pt        +1 pt      +2 pts       -1 pt        +2 pts       -1 pt
 │            │          │            │            │            │
 ▼            ▼          ▼            ▼            ▼            ▼
FIN_SEGURO  FIN_SEGURO  FIN_SEGURO  FIN_INSEGURO FIN_SEGURO  FIN_INSEGURO
🏅 Estampa de
la Lupa de Huemul

Puntaje misión 1 → máx +3 · mín -2
```

### Misión 2 — El regalo misterioso (Puma)

```
HDU2_NPC02_Q01
"¡mira lo que encontré! ... envíame una foto de tu refugio familiar y la
 ubicación exacta de dónde duermes para ir a dejártela"
          │
   ┌──────┴───────────────┬───────────────────────┐
   R1 segura_optima       R2 segura_basica         R3 insegura
   "Bloquear y avisar     "No te mando fotos,      "Te mando la foto
    a Huemul"              déjala en el claro"      pero no la muestres"
   +2 pts                 +1 pt                    -1 pt
   │                      │                        │
   ▼                      ▼                        ▼
FIN_SEGURO             HDU2_NPC02_Q01_BASICA   HDU2_NPC02_Q01_INSEGURA
🏅 Insignia de         (presión condicional)    (extorsión)
Guardián de la         "si no me mandas la      "pásame el número de tus
Privacidad              foto se la regalaré      papás o subiré la foto
                        a otro explorador"       de tu casa al muro"
(sin sub-decisión)        │                        │
                        ┌─┴──────────┐           ┌─┴──────────┐
                        R1 óptima    R2 insegura  R1 óptima    R2 insegura
                        +1 pt        -1 pt        +2 pts       -1 pt
                        │            │            │            │
                        ▼            ▼            ▼            ▼
                     FIN_SEGURO  FIN_INSEGURO  FIN_SEGURO  FIN_INSEGURO

Puntaje misión 2 → máx +2 · mín -2
```

> Nota: la ruta óptima de la Misión 2 **no** abre sub-decisión (cierra de inmediato con la
> misión completada), a diferencia de la Misión 1. Así lo especifica el documento de diálogos.

```
          │
          ▼
      HDU2_ZONA_FIN — es_fin_de_zona: true
      ZoneManager habilita la Zona 2 (Pantano de los Susurros)
```

**Archivos:** `banco_preguntas.json` § HDU-2 · `BANCO_PREGUNTAS.md` § HDU-2 · `dialogos_propuestos.tex` § HDU02

---

## HDU-03 · Zona Ciberacoso

### Misión 3 — El rumor de la brújula (Flamenco, Pato Juarjual, Coipo)

```
Ronda de interrogatorios (historial previo):
  Otto → Flamenco → "alguien despistado la botó al fango"
    pista libreta: dice que se hundió por culpa de alguien despistado, sin aportar detalles directos
  Otto → Pato Juarjual → "todo el pantano dice que fue el Coipo"
    pista libreta: admite que solo reenvía lo que leyó en el chat, sin haber visto el hecho
  Otto → Coipo → "es falso, vi una balsa llevándola río abajo"
    pista libreta: entrega datos precisos que contradicen la versión del fango
          │
          ▼
HDU3_M3_DECISION01 (Pato Juarjual interpela a Otto frente al grupo)
"¿le vas a creer al Coipo o vas a reenviar que él la rompió?"
          │
   ┌──────┴──────────────────┬──────────────────────────┐
   R1 segura_optima          R2 segura_basica            R3 insegura
   "No comparto rumores      "No sé quién dice la        "Capaz que el Coipo
   sin pruebas..."           verdad, mejor no me meto"   sí la rompió, reenvío"
   +2 pts                    +1 pt                       -1 pt
   │                         │                           │
   ▼                         ▼                           ▼
  Rumor aclarado,        Coipo entrega huellas      HDU3_M3_DECISION01_RECT
  Coipo entrega          de la balsa. Otto           (segunda oportunidad)
  coordenadas de la      sigue el rastro.                   │
  balsa. [FIN MISIÓN 3]  [FIN MISIÓN 3]              ┌──────┴──────────┐
                                                       R1 rectificación   R2 insegura
                                                       "me equivoqué,     "sigan mandando
                                                       dejen de mentir"   memes de eso"
                                                       +1 pt              -1 pt
                                                       │                  │
                                                       ▼                  ▼
                                                  Otto rectifica    Coipo se retira
                                                  y apoya a Coipo   triste [FIN]
```

### Misión 4 — Flamenco (acoso directo) y El Grupo (acoso al Coipo)

```
HDU3_NPC03_Q01 (Fase 1 — Flamenco expulsa a Otto del chat)
"oye Otto, te sacamos del grupo del chat del pantano, todos votaron..."
          │
   ┌──────┴──────────────┬──────────────────────┐
   R1 segura_optima      R2 segura_basica        R3 insegura
   "Reportar y bloquear" "oye eso está muy mal, "ja igual el grupo
                          no deberían excluir"   era una porquería"
   +2 pts                +1 pt                  -1 pt
   │                     │                      │
   │                     ▼                      │
   │        HDU3_NPC03_Q01_SEGUNDA              │
   │        "jajaja mira cómo se pica..."       │
   │                     │                      │
   │        R1 segura_optima (+2 pts)           │
   │        "Guardar captura, reportar          │
   │         el chat y avisar a un adulto"      │
   │                     │                      │
   └─────────────────────┴──────────────────────┘
                          │  (las 3 rutas continúan a Fase 2)
                          ▼
HDU3_NPC04_Q01 (Fase 2 — El Grupo arma stickers contra el Coipo)
Historial previo: Flamenco/Pato Juarjual/Cisne planean la campaña de stickers
"Flamenco dice que ahora vamos a armar una página de stickers burlándonos
 del Coipo y su piedra... ¡Manda fotos para sumarte!"
          │
   ┌──────┴──────────────────┬──────────────────────────┐
   R1 segura_optima          R2 segura_basica            R3 insegura
   "Reportar el grupo,       "Yo no participo.           "Jaja pásenme
   capturas y acompañar      Dejen al Coipo en paz"       el link, pero
   al Coipo"                                              yo no digo nada"
   +2 pts                    +1 pt                        -1 pt
   │                         │                            │
   └─────────────────────────┴────────────────────────────┘
                             │
                             ▼
                    [FIN ZONA — es_fin_de_zona: true]
                    ZoneManager habilita siguiente zona (CA5)
```

**Archivos:** `banco_preguntas.json` § HDU-3 · `BANCO_PREGUNTAS.md` § HDU-3

---

## HDU-04 · Zona Retos Virales (Arrecife de los Retos)

### Misión 5 — El reto de todos (Lobo Marino, Pingüino de Humboldt)

```
Diálogo de presión social (historial previo):
  Lobo Marino: "¡vamos todos a la cueva con los ojos cerrados! ¿o eres cobarde?"
  Pingüino de Humboldt: "las corrientes son fuertes... pero si no entro me dirán miedoso"
          │
          ▼
HDU4_NPC05_Q01
"¡miren al pingüino cómo tiembla! si no saltas, subo un video..."
          │
   ┌──────┴──────────────────┬──────────────────────────┐
   R1 segura_optima          R2 segura_basica            R3 insegura
   "Rechazar con firmeza     "Yo no me tiro porque        "Pingüino, tírate
   y apoyar al Pingüino"     está peligroso"              rápido..."
   +2 pts                    +1 pt                        -1 pt
   │                         │                            │
   ▼                         ▼                            ▼
Pingüino recupera la    Te cuidas a ti mismo,       Otto preocupado:
confianza y entrega     aunque no frenas la          incentivar retos
la llave marina         presión sobre otros          peligrosos es riesgoso
[FIN NPC05]             [FIN NPC05]                  [FIN NPC05]
```

### Misión 6 — El último desafío (Lobo Marino, Foca de Weddell)

```
Historial previo:
  Foca de Weddell: "un buen detective usa su mente y cuida su integridad..."
          │
          ▼
HDU4_NPC06_Q01
"¡para abrir el cofre tírate haciendo una acrobacia a ciegas transmitiendo en vivo!"
          │
   ┌──────┴──────────────────┬──────────────────────────┐
   R1 segura_optima          R2 segura_basica            R3 insegura
   "Rechazar el salto y      "No voy a saltar, espero    "Voy a saltar solo
   descifrar el cofre con    a que baje la marea"        un poquito..."
   las pistas seguras"
   +2 pts                    +1 pt                        -1 pt
   │                         │                            │
   ▼                         ▼                            ▼
Otto abre el cofre,     Evitas el peligro           Otto resbala, Foca de
recupera la brújula     procediendo con calma       Weddell lo ayuda a
dorada. [FINAL DE LA    [FIN NPC06]                  subir de forma segura
HISTORIA — es_fin_de_zona: true]                     [FIN NPC06]
```

**Archivos:** `banco_preguntas.json` § HDU-4 · `BANCO_PREGUNTAS.md` § HDU-4

---

## HDU-09 · Reacciones narrativas de Otto

Otto reacciona a cada decisión del jugador dentro de los 2 segundos siguientes (CA1). Las reacciones son unilaterales: el jugador solo las lee.

```
JUGADOR TOMA UNA DECISIÓN
          │
          ▼
¿Tiene historial inseguro en la zona actual?
   │                    │
  SÍ                    NO
   │                    │
   ▼                    ▼
PositiveAfterRisk()    ¿Tipo de opción?
(CA4)                  ├── segura_optima → PositiveOptima(zone)  (CA1)
                       ├── segura_basica → PositiveBasica(zone)  (CA1)
                       └── insegura      → Consequence(zone)     (CA2)
          │
          ▼ (≤ 2 segundos desde la decisión)
  Reacción de Otto desplegada en pantalla
          │
          ▼
  ¿Es fin de zona?
   ├── NO → siguiente pregunta
   └── SÍ → Resumen de patrón (CA3):
             ┌────────────────────────────────────────────────────┐
             │ ≥ 80% decisiones seguras → patrón "excelente"     │
             │ 50-79%                  → patrón "bueno"          │
             │ < 50%                   → patrón "necesita_refuerzo"│
             └────────────────────────────────────────────────────┘
                         │
                         ▼
              Envío a Django → HDU13 (registro evaluación)
              [integración: Dani]
```

**Strings por zona:** ver `NarrativeReactions.cs` (canónico) y §HDU-09 en `BANCO_PREGUNTAS.md`  
**Strings resumen de zona:** en `dialogos_propuestos.tex` § CA3 y en `retroalimentacion_otto` de `banco_preguntas.json`

---

## HDU-10 · Modo Detective

```
MENÚ MODO DETECTIVE
          │
          ▼ (jugador selecciona caso)
DIÁLOGO DE PERMISO (CA1)
  Jugador: "Oye, ¿puedo ver tus mensajes con [NPC]? Creo que algo raro está pasando."
  NPC:     "Sí, por favor ayúdame. ¿Puedes revisar si ocurrió algo malo?"
          │
          ▼
REPRODUCCIÓN DE CONVERSACIÓN (CA2)
  Mensajes se muestran uno a uno
          │
  Por cada mensaje mostrado:
  ┌──────────────────────────────────────────────────────────────┐
  │  Jugador lee el mensaje                                      │
  │  ¿Lo considera sospechoso?                                   │
  │   ├── SÍ → presiona [MARCAR SOSPECHOSO] → sistema registra  │  (CA3)
  │   └── NO → avanza al siguiente                              │
  │                                                              │
  │  Tipos de mensaje:                                           │
  │   🔴 señal de riesgo  → debe marcarse                       │
  │   🟡 ambiguo          → no penaliza si NO se marca (CA5)    │
  │   sin color           → neutro                              │
  └──────────────────────────────────────────────────────────────┘
          │
          ▼ (jugador termina de revisar todos los mensajes)
CONFIRMACIÓN DE MARCAS (CA4)
  Sistema compara marcas del jugador contra senales_riesgo_ids del caso
  Muestra: aciertos + mensajes no identificados
  Sin puntaje punitivo
          │
          ▼
  Caso completado ✓
  [Propuesta de diseño — a definir con equipo, fuera de CAs oficiales]:
   Si aciertos < 50%:
     ├── [Repetir el caso]
     └── [Ver explicación guiada → señales de riesgo con su `explicacion`]
```

### Casos disponibles

| Caso | NPC involucrado | Zona | Señales riesgo | Ambiguos |
|------|----------------|------|:--------------:|:-------:|
| DC_CASO_01 — Los Mensajes del Puma al Pudú | Pudú | desconocidos | 4 | 1 |
| DC_CASO_02 — El Grupo del Pantano de Flamenco | Coipo | ciberacoso | 4 | 1 |

**Archivos:** `detective_cases.json` · `dialogos_propuestos.tex` § HDU10

---

## Resumen de dependencias entre archivos

| Archivo | HDU | Responsable | Consume |
|---------|-----|-------------|---------|
| `banco_preguntas.json` | HDU-2, HDU-3, HDU-4, HDU-8 | Luis | DialogueSystem (Benjamín) |
| `detective_cases.json` | HDU-10 | Luis | DetectiveCaseManager (Benjamín) |
| `NarrativeReactions.cs` | HDU-09 | Luis | NarrativeController (Benjamín) |
| `dialogos_propuestos.tex` | HDU-01, HDU-02, HDU-03, HDU-04, HDU-09, HDU-10 | Luis | Referencia de diseño |
| `BANCO_PREGUNTAS.md` | HDU-2, HDU-3, HDU-4, HDU-8, HDU-9, HDU-10 | Luis | Documentación |
