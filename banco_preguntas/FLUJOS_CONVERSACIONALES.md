# Flujos Conversacionales — Fishy!

**Autor:** Luis González — MLOps  
**Sprint:** 1 · **Fecha:** 2026-08-23  
**HDUs:** HDU-01 · HDU-03 · HDU-09 · HDU-10

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
  │  NPC Vizcacha (Informante)       │
  │  Línea 1 → Línea 2               │
  │  → MISION_INVESTIGACION_01       │
  └──────────────────────────────────┘
          │
          ▼
  Pista de misión mostrada (CA3)
          │
          ▼
  MissionManager registra misión disponible (CA4) ← Unity/Benjamín
```

**Archivos:** `dialogos_propuestos.tex` § HDU01

---

## HDU-03 · Zona Ciberacoso

### NPC03 — Flamenco (acoso al jugador)

```
HDU3_NPC03_Q01
"oye te sacamos del grupo del chat del pantano, todos votaron y fue mayoría jaja"
          │
   ┌──────┴──────────────┬──────────────────────┐
   R1 segura_optima      R2 segura_basica        R3 insegura
   "Reportar y bloquear" "oye eso está muy mal" "ja igual el grupo..."
   +2 pts                +1 pt                  -1 pt
   │                     │                      │
   ▼                     └──────────┬───────────┘
  "Flamenco desaparece               ▼
   de la pantalla.       HDU3_NPC03_Q02
   Otto levanta          "jajajaja mira como se pone..."
   el sombrero."         │
   [FIN NPC03]     ┌─────┴─────────────────┬──────────────────────┐
                   R1 segura_optima         R2 segura_basica       R3 insegura
                   "Reportar y bloquear"    "me voy a quejar       "ya, hagan lo
                   +2 pts                   con la profe"          que quieran"
                   │                        +1 pt                  -1 pt
                   └─────────────────────────┴──────────────────────┘
                                             │
                                             ▼
                                         [FIN NPC03]
```

### NPC04 — El Grupo (Flamenco, Pato Juarjual, Cisne → acosan al Cormorán)

```
HDU3_NPC04_Q01
"oye viste que le mandamos mil mensajes al Cormorán diciéndole que es raro..."
          │
   ┌──────┴──────────────┬──────────────────────┐
   R1 segura_optima      R2 segura_basica        R3 insegura
   "voy a avisarle a     "oye eso que hicieron  "jajaja sí era
   la profe ahora mismo" está súper mal"         medio raro igual"
   +2 pts                +1 pt                  -1 pt
   │                     │                      │
   ▼                     └──────────┬───────────┘
  "Avisaste a un adulto.             ▼
   Otto anota: nueva     HDU3_NPC04_Q02
   pista en el cuaderno" "ahora dicen que el Cormorán se va a ir del pantano..."
   [FIN NPC04, CA4]      │
                   ┌─────┴─────────────────┬──────────────────────┐
                   R1 segura_optima         R2 segura_basica       R3 insegura
                   "Reportar y bloquear     "eso no está bien,     "ahh igual medio
                   el grupo y avisarle      el Cormorán no se      dramático si fue
                   a la profe"              merece esto"           solo un chat"
                   +2 pts                  +1 pt                  -1 pt
                   │                        │                      │
                   └─────────────────────────┴──────────────────────┘
                                             │
                                             ▼
                                         [FIN ZONA — es_fin_de_zona: true]
                                         ZoneManager habilita siguiente zona (CA5)
```

**Archivos:** `banco_preguntas.json` § HDU-3 · `BANCO_PREGUNTAS.md` § HDU-3

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
| DC_CASO_01 — El nuevo amigo del Chingue | Pudú | desconocidos | 4 | 1 |
| DC_CASO_02 — El grupo de Flamenco | Cormorán | ciberacoso | 4 | 1 |

**Archivos:** `detective_cases.json` · `dialogos_propuestos.tex` § HDU10

---

## Resumen de dependencias entre archivos

| Archivo | HDU | Responsable | Consume |
|---------|-----|-------------|---------|
| `banco_preguntas.json` | HDU-2, HDU-3, HDU-8 | Luis | DialogueSystem (Benjamín) |
| `detective_cases.json` | HDU-10 | Luis | DetectiveCaseManager (Benjamín) |
| `NarrativeReactions.cs` | HDU-09 | Luis | NarrativeController (Benjamín) |
| `dialogos_propuestos.tex` | HDU-01, HDU-03, HDU-09, HDU-10 | Luis | Referencia de diseño |
| `BANCO_PREGUNTAS.md` | HDU-2, HDU-3, HDU-8, HDU-9, HDU-10 | Luis | Documentación |
