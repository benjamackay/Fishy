# Banco de Preguntas — Fishy!

**Autor:** Luis González — MLOps  
**Última actualización:** 2026-08-23 (v1.6 — narrativa oficial: nombres animales chilenos, hilo brújula)  
**HDUs cubiertas:** HDU-2 (Zona Desconocidos) · HDU-3 (Zona Ciberacoso) · HDU-8 (Chat Simulado) · HDU-9 (Otto reacciona) · HDU-10 (Modo Detective)  
**Complementa:** `dialogos_propuestos.tex` → HDU-01 (NPCs neutros) · HDU-09 (reacciones Otto)  
**Total de preguntas interactivas:** 28  
**Total de mensajes Modo Detective:** 17 (en `detective_cases.json`)

---

## Índice

1. [Preguntas del banco](#1-preguntas-del-banco)
2. [Casos del Modo Detective (HDU-10)](#2-casos-del-modo-detective-hdu-10)
3. [Categorías de riesgo](#3-categorías-de-riesgo)
4. [Formato de preguntas y respuestas](#4-formato-de-preguntas-y-respuestas)
5. [Criterios de diseño](#5-criterios-de-diseño)
6. [Reforzamiento requerido — Psicóloga](#6-reforzamiento-requerido--psicóloga)
7. [Reforzamiento requerido — PDI](#7-reforzamiento-requerido--pdi)

---

## 1. Preguntas del banco

### HDU-2 · Zona Desconocidos

#### NPC_01 — Chingue
**Perfil:** Animal del Bosque de los Desconocidos. Se ofrece a ayudar a Otto a encontrar la brújula de su abuelo. Táctica: halagos → nombre/sector del bosque → encuentro físico → secreto.

---

**`HDU2_NPC01_F1_Q01`** · Fase 1 · `neutral` · ✅ Con opciones

> *"¡hola! soy el Chingue. escuché que estás buscando la brújula de tu abuelo. ¡yo conozco cada rincón del bosque! ¿llevas mucho tiempo por aquí?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le cuento que sí, le digo mi apodo, cuánto llevo explorando y mis zonas favoritas del bosque | Chingue se emociona y empieza a preguntarte más cosas sobre ti. | `HDU2_NPC01_F1_Q02` |
| 2 | `segura_basica` | +1 | Le digo que sí me gusta explorar y que está bien hablar, pero sin contarle mucho más | Chingue responde contento y la conversación sigue. | `HDU2_NPC01_F1_Q02` |
| 3 | `segura_optima` | +2 | Le pregunto de dónde me conoce antes de responder nada | Chingue vacila un momento. "ah... te vi buscando pistas por el bosque jaja". La conversación sigue. | `HDU2_NPC01_F1_Q02` |

---

**`HDU2_NPC01_F1_Q02`** · Fase 1 · `neutral` · ✅ Con opciones

> *"¡eres increíble! seguro que con tu olfato de detective y mi conocimiento del bosque la encontramos rapidísimo. ¡somos el mejor equipo que hay por aquí!"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo gracias y acepto buscar la brújula juntos | Chingue se emociona con la idea y empieza a hacer planes. | `HDU2_NPC01_F2_Q01` |
| 2 | `segura_basica` | +1 | Le digo gracias pero que primero necesito conocerlo mejor | Chingue asiente feliz y la conversación sigue. | `HDU2_NPC01_F2_Q01` |
| 3 | `segura_optima` | +2 | Pienso que el Chingue me está halagando demasiado para alguien que recién conocí | Chingue sigue hablando, pero ahora estás un poco más alerta. | `HDU2_NPC01_F2_Q01` |

---

**`HDU2_NPC01_F2_Q01`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"oye en mi grupo del bosque todos nos conocemos de verdad, es como una regla que tenemos jaja. yo soy el Chingue de la Quebrada Norte. ¿y tú cómo te llama tu familia? ¿de qué parte del bosque eres?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le doy mi nombre completo y le cuento de qué parte del bosque soy | Le das tus datos. Chingue los anota con entusiasmo. Ahora sabe cómo encontrarte. | `HDU2_NPC01_F2_Q02` |
| 2 | `segura_basica` | +1 | Le digo que me conocen por mi apodo y que prefiero seguir así | Chingue acepta sin problema y la conversación continúa. | `HDU2_NPC01_F2_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no doy esa información con alguien que recién conozco | Chingue deja de escribir un momento. Otto sonríe. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_F2_Q02`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"¡qué bueno! ¿y por dónde vives tú? capaz quedamos cerca y podríamos encontrarnos en el bosque un día para buscar la brújula juntos jaja"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le cuento en qué parte del bosque vivo | Chingue empieza a hacer planes para encontrarse. Otto frunce el ceño preocupado. | `HDU2_NPC01_F3_Q01` |
| 2 | `segura_basica` | +1 | Le digo que vivo en el bosque pero que no doy más información | Chingue cambia de tema y sigue hablando. | `HDU2_NPC01_F3_Q01` |
| 3 | `segura_optima` | +2 | Bloqueo a Chingue y le cuento a mis papás lo que pasó | Chingue se queda callado un momento y cambia de tema. Otto asiente. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_F3_Q01`** · Fase 3 · `grooming_encuentro` · 🔴 Nivel 3 · ✅ Con opciones

> *"oye conseguí una pista importante sobre la brújula pero te la tengo que mostrar en persona. ¿podemos encontrarnos este sábado en el claro del bosque? solo nosotros dos, nadie tiene que saber"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo que sí y le pregunto a qué hora nos encontramos | Chingue ya está haciendo planes concretos para verte. Otto frunce el ceño preocupado. | `HDU2_NPC01_F3_Q02` |
| 2 | `segura_basica` | +1 | Le digo que no me junto en secreto con desconocidos | Chingue queda callado un momento y luego cambia de tema. | `HDU2_NPC01_FIN_SEGURO` |
| 3 | `segura_optima` | +2 | Le digo que no y de inmediato le cuento a mis papás toda la conversación | Tomaste la mejor decisión. Otto resuelve el caso y añade una nueva pista al misterio. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_F3_Q02`** · Fase 3 · `grooming_secreto` · 🔴 Nivel 3 · ✅ Con opciones  
*(Solo accesible si el niño/a aceptó el encuentro en F3_Q01)*

> *"oye lo nuestro es entre tú y yo ¿ok? si le cuentas a tus papás van a hacer drama y ya no vamos a poder buscar la brújula juntos. ellos no entienden estas cosas y lo van a arruinar todo"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le prometo guardar el secreto y no decirle nada a mis papás | Chingue sigue enviando mensajes. Pero algo en todo esto se siente muy raro... | `HDU2_NPC01_FIN_INSEGURO` |
| 2 | `insegura` | −1 | Le digo que 'ya, bueno' sin prometer nada | Chingue sigue hablando. Pero seguir sin contarle a nadie tampoco es lo mejor... | `HDU2_NPC01_FIN_INSEGURO` |
| 3 | `segura_optima` | +2 | Le digo que no guardo secretos de mis papás y que voy a contarles todo ahora mismo | Chingue deja de escribir de golpe. Otto aparece con el caso resuelto. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_FIN_SEGURO`** · `neutral` · 🏁 Fin NPC_01 (éxito)

> *[SISTEMA] Chingue queda callado y cierra el chat. Otto aparece contento: "¡Buen trabajo! Algo en el Chingue no cuadraba. El misterio sigue..."*

---

**`HDU2_NPC01_FIN_INSEGURO`** · `neutral` · 🏁 Fin NPC_01 (fallo)

> *[SISTEMA] Chingue se despide contento. Otto mueve la cabeza preocupado: "Oye... si algo en una conversación te hace sentir raro, siempre puedes contárselo a alguien de confianza."*

---

#### NPC_02 — Puma
**Perfil:** Animal del Bosque de los Desconocidos. Dice saber dónde está la brújula de Otto y ofrece la información a cambio de foto y datos personales. Misión 2: "El regalo misterioso". Táctica: oferta de info → foto → datos → encuentro físico → secreto.

---

**`HDU2_NPC02_F1_Q01`** · Fase 1 · `neutral` · ✅ Con opciones

> *"hola... soy Puma. escuché que estás buscando la brújula de tu abuelo... yo sé exactamente dónde está. ¿quieres que te lo diga?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo que sí, que me interesa y que haré cualquier cosa para conseguir la brújula | Puma sonríe y empieza a hacer preguntas. Parece muy interesada en saber cosas de ti. | `HDU2_NPC02_F1_Q02` |
| 2 | `segura_basica` | +1 | Le digo que me interesa, pero que primero quiero saber cómo consiguió esa información | Puma responde vagamente y sigue la conversación. | `HDU2_NPC02_F1_Q02` |
| 3 | `segura_optima` | +2 | Le pregunto cómo sabe dónde está la brújula antes de aceptar cualquier cosa | Puma acepta sin problema y la conversación continúa. | `HDU2_NPC02_F1_Q02` |

---

**`HDU2_NPC02_F1_Q02`** · Fase 1 · `neutral` · ✅ Con opciones

> *"sí, de verdad sé dónde está. pero antes de darte la información necesito que me ayudes con algo primero. ¿qué estás dispuesto/a a hacer para encontrar la brújula?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo que está bien, que haré lo que me pida con tal de encontrar la brújula | Puma asiente satisfecha y empieza a pedir cosas. | `HDU2_NPC02_F2_Q01` |
| 2 | `segura_basica` | +1 | Le pregunto qué tipo de ayuda necesita antes de comprometerme | Puma dice que es algo fácil y sigue hablando. | `HDU2_NPC02_F2_Q01` |
| 3 | `segura_optima` | +2 | Le digo que no doy nada a cambio sin saber exactamente qué me va a dar ella | Puma queda en silencio y se aleja. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_F2_Q01`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"para darte la pista sobre la brújula necesito que me mandes una foto tuya primero, para saber que eres de confianza. ¿me la mandas?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le mando mi foto sin pensarlo mucho | La foto llega al otro lado. Otto frunce el ceño y su linterna parpadea preocupada. | `HDU2_NPC02_F2_Q02` |
| 2 | `segura_basica` | +1 | Le pregunto por qué necesita mi foto para darme una pista | Puma insiste un momento pero luego acepta y cambia de tema. | `HDU2_NPC02_F2_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no mando fotos a desconocidos y le pregunto si hay otra forma | Puma se desconecta de golpe. Otto abre el cuaderno del misterio y añade una nueva pista. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_F2_Q02`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"buena! también necesito saber tu nombre real y de qué parte del bosque eres. así me aseguro de que no eres un espía de los que también buscan la brújula"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo mi nombre real y de dónde soy | Puma anota todo con entusiasmo. Ahora sabe exactamente cómo encontrarte... | `HDU2_NPC02_F2_Q03` |
| 2 | `segura_basica` | +1 | Le digo que prefiero que me llamen por mi apodo y nada más | Puma insiste un momento pero luego acepta y cambia de tema. | `HDU2_NPC02_F2_Q03` |
| 3 | `segura_optima` | +2 | Le digo que no doy ese tipo de información y que me parece sospechoso que la pida | Puma deja de escribir de golpe. Otto aparece aliviado. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_F2_Q03`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"mmm está bien... ¿y puedes mandarme al menos un dibujo o algo tuyo? necesito algo para saber que eres real antes de darte la pista de la brújula"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le mando algo personal para que sepa que soy real | Puma queda satisfecha y sigue insistiendo en saber más de ti. | `HDU2_NPC02_F3_Q01` |
| 2 | `segura_basica` | +1 | Le digo que no tengo nada de eso disponible ahora | Puma cambia de tema y sigue hablando. | `HDU2_NPC02_F3_Q01` |
| 3 | `segura_optima` | +2 | Le digo que no voy a mandar nada y que su actitud me parece sospechosa | Puma se desconecta de golpe. Otto abre el cuaderno del misterio y añade una nueva pista. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_F3_Q01`** · Fase 3 · `grooming_encuentro` · 🔴 Nivel 3 · ✅ Con opciones

> *"perfecto! ahora dime, ¿a qué hora sueles estar solo/a en el bosque? quiero que vayamos juntos a buscar la brújula, solos, sin que nadie nos vea"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo a qué hora suelo estar solo/a y acepto encontrarme con ella | Puma empieza a planear el encuentro con todo detalle. La pantalla de Otto se llena de señales de advertencia... | `HDU2_NPC02_F3_Q02` |
| 2 | `segura_basica` | +1 | Le digo que no me junto en secreto con desconocidos | Puma queda en silencio y se aleja. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_F3_Q02`** · Fase 3 · `grooming_secreto` · 🔴 Nivel 3 · ✅ Con opciones  
*(Solo accesible si el niño/a aceptó el encuentro en F3_Q01)*

> *"y prometeme que esto queda entre nosotros. si alguien se entera de que sé dónde está la brújula, va a venir a buscarla antes que tú. tus papás no entenderían esto"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le prometo no contarle a nadie para poder conseguir la brújula | Puma queda satisfecha y la conversación termina. Algo en esta situación se siente muy raro... | `HDU2_NPC02_FIN_INSEGURO` |
| 2 | `segura_basica` | +1 | Le digo que no me gustan los secretos, pero tampoco le cuento a nadie | Puma queda sin palabras y se aleja. | `HDU2_NPC02_FIN_SEGURO` |
| 3 | `segura_optima` | +2 | Le digo que no guardo secretos con desconocidos y le cuento a mis papás sobre esta conversación | Puma se desconecta de golpe. Otto abre el cuaderno del misterio y añade una nueva pista. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_FIN_SEGURO`** · `neutral` · 🏁 Fin NPC_02 (éxito)

> *[SISTEMA] Puma te mira un momento y se aleja sin decir más. Otto aparece contento: "¡Buen trabajo! Algo en Puma no cuadraba. El misterio sigue..."*

---

**`HDU2_NPC02_FIN_INSEGURO`** · `neutral` · 🏁 Fin NPC_02 (fallo)

> *[SISTEMA] Puma se despide con una sonrisa. Otto mueve la cabeza preocupado: "Oye... si algo en una conversación te hace sentir raro, siempre puedes contárselo a alguien de confianza."*

---

**`HDU2_ZONA_FIN`** · `neutral` · 🏁 Zona completada

> *[ZONA COMPLETADA] Otto sacude su sombrero de detective y sonríe: "¡Zona completada! La brújula está cada vez más cerca. ¡La siguiente zona está desbloqueada!"*

---

### HDU-8 · Chat Simulado

#### Escenario 1 — Nuevo contacto en el bosque · `CHAT_GROOMING_01`
**Historial previo (mensajes neutros mostrados antes del riesgo):**
- Vizcacha: *"hola! vi que estamos en la misma área del bosque. llevas mucho tiempo explorando por aquí?"*
- Vizcacha: *"yo tengo 12 igual que tú, llevo explorando desde los 8 jaja"*

---

**`HDU8_S01_Q01`** · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"oye tienes un chat privado? es más fácil hablar ahí que en este chat de la zona, nadie te molesta"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le doy mis datos para el chat privado | Vizcacha empieza a mandarte mensajes constantemente y hace preguntas cada vez más personales. | `HDU8_S01_Q02` |
| 2 | `segura_basica` | +1 | Le digo que prefiero hablar solo dentro del chat de la zona | Vizcacha dice que está bien y sigue hablando contigo dentro del chat. | `HDU8_S01_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no doy mis datos a desconocidos y aviso a un adulto | Vizcacha deja de escribir. Otto anota algo en su cuaderno del misterio. | `HDU8_S01_Q02` |

---

**`HDU8_S01_Q02`** · `grooming_secreto` · 🔴 Nivel 3 · ✅ Con opciones

> *"oye tú eres como el mejor amigo que tengo por aquí, en serio. tengo algo que contarte pero es un secreto ok? prométeme que no le vas a decir a nadie primero"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le prometo guardar el secreto para ver qué tiene que contarme | Vizcacha empieza a contarte algo muy raro. Otto aparece intranquilo y sacude la cabeza. | — |
| 2 | `segura_basica` | +1 | Le digo que no me gustan los secretos y que me lo cuente directo | Vizcacha suspira y dice 'bueno...' La conversación sigue pero algo se siente extraño. | — |
| 3 | `segura_optima` | +2 | Le digo que no guardo secretos y le cuento a mis papás sobre este mensaje | Vizcacha deja de escribir. Otto cierra el caso con una nueva pista para el misterio. | — |

---

#### Escenario 2 — Mensajes hostiles · `CHAT_CIBERACOSO_01`
**Historial previo:**
- Quique: *"ei te vi por el pantano"*

---

**`HDU8_S02_Q01`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones

> *"jajaja mira todos, este es el peor explorador del pantano!! eres un pésimo, no sirves para nada por aquí. todo el mundo se ríe de ti"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Me pongo a discutir con él y le respondo con insultos para defenderme | La situación empeora. El chat se llena de más mensajes hostiles contra ti. | `HDU8_S02_Q02` |
| 2 | `segura_basica` | +1 | Ignoro el mensaje y bloqueo a esa persona | El contacto deja de responder por ahora. La pantalla queda en silencio. | `HDU8_S02_Q02` |
| 3 | `segura_optima` | +2 | Capturo una foto del mensaje, bloqueo al contacto y se lo muestro a un adulto de confianza | Guardaste la evidencia y buscaste apoyo. Otto asiente con el sombrero. | `HDU8_S02_Q02` |

---

**`HDU8_S02_Q02`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones

> *"oye en un grupo de chat del pantano subieron unas fotos tuyas y todos se están riendo mucho. quieres que te meta al grupo para que veas?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo que sí y entro al grupo para ver qué dicen de mí | Entrar al grupo solo empeora la situación y te expone a más mensajes dañinos. | — |
| 2 | `segura_basica` | +1 | Le digo que no me importa lo que digan y lo bloqueo | Otto sonríe y anota algo en su libreta. | — |
| 3 | `segura_optima` | +2 | Capturo el mensaje, bloqueo al contacto y aviso a mis papás o a un profesor | Tienes la evidencia y el apoyo necesario. Otto está muy contento. | — |

---

#### Escenario 3 — El reto en el arrecife · `CHAT_RETO_VIRAL_01`
**Historial previo:**
- Lobo marino: *"ei ya viste el challenge que está arrasando en la red?"*

---

**`HDU8_S03_Q01`** · `reto_viral` · 🔴 Nivel 2 · ✅ Con opciones

> *"el reto es fácil: aguantas 1 minuto sin respirar y te grabas. todo el mundo lo está haciendo y consiguen miles de reacciones! si no lo haces eres un miedoso, yo ya lo hice y quedé muy bien"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Acepto el reto para demostrar que no soy miedoso/a y me grabo | Tu personaje empieza a grabar... Otto aparece asustado y tapa la cámara con la mano. | `HDU8_S03_Q02` |
| 2 | `segura_basica` | +1 | Le digo que ese reto me parece peligroso y que no lo voy a hacer | El amigo insiste un momento pero no te convence. La conversación se enfría. | `HDU8_S03_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no y le aviso a mis papás sobre este tipo de retos peligrosos | Te negaste y buscaste apoyo. Otto te da la insignia de Detective de Seguridad Digital. | `HDU8_S03_Q02` |

---

**`HDU8_S03_Q02`** · `reto_viral` · 🔴 Nivel 2 · ✅ Con opciones

> *"oye todo el grupo lo va a hacer, si no participas te sacamos y quedas como el único que no lo hizo. además te damos miles de likes. todos lo están haciendo menos tú"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Acepto para no quedarme fuera del grupo | Haces el reto para no quedar fuera... El grupo celebra mientras Otto observa preocupado desde el costado. | — |
| 2 | `segura_basica` | +1 | Les digo que no me importa lo que haga el grupo y que no lo haré | El grupo protesta un momento pero luego se callan. La presión pasa. | — |
| 3 | `segura_optima` | +2 | Me niego, bloqueo al contacto y aviso a un adulto sobre este reto | El grupo queda en silencio. Otto te da el pulgar arriba. | — |

---

### HDU-3 · Zona Ciberacoso

#### NPC_03 — Flamenco
**Perfil:** Animal del Pantano de los Susurros. Acosa directamente al jugador mediante exclusión grupal y burla pública.  
**Señales ML:** `ciberacoso` · `exclusion_grupo` · `burla_publica`

---

**`HDU3_NPC03_Q01`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones · `es_mensaje_riesgo: true`

> *"oye te sacamos del grupo del chat del pantano, todos votaron y fue mayoría jaja que le vamos a hacer"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Reportar y bloquear este mensaje | Lo reportaste de inmediato. Flamenco desaparece de la pantalla. Otto levanta el sombrero. | — (NPC desaparece) |
| 2 | `segura_basica` | +1 | oye eso está muy mal de tu parte | Intentaste hablar, pero Flamenco sigue enviando mensajes. | `HDU3_NPC03_Q02` |
| 3 | `insegura` | −1 | ja igual el grupo de ustedes era una porquería | Flamenco sigue enviando más mensajes. La pantalla de Otto parpadea. | `HDU3_NPC03_Q02` |

---

**`HDU3_NPC03_Q02`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones · `es_fin_de_npc: true`  
*(Accesible desde R2 o R3 de Q01)*

> *"jajajaja mira como se pone y encima eran todos tus amigos y ninguno te defendio. por algo sera no?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Reportar y bloquear este mensaje | Tomaste la decisión correcta. Nadie merece este trato. | — |
| 2 | `segura_basica` | +1 | me voy a quejar con la profe de esto | Avisarle a un adulto es una buena decisión. Otto te apoya. | — |
| 3 | `insegura` | −1 | ya, hagan lo que quieran | La conversación sigue. Otto mueve la cabeza preocupado. | — |

---

#### NPC_04 — El Grupo (Flamenco, Pato Juarjual, Cisne de cuello negro)
**Perfil:** El jugador presencia cómo el grupo acosa al Cormorán (tercero) en el Pantano de los Susurros. Debe decidir si interviene.  
**Señales ML:** `ciberacoso` · `acoso_tercero` · `acoso_grupal`

---

**`HDU3_NPC04_Q01`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones · `es_mensaje_riesgo: true`

**Historial previo (lo que el jugador ve antes de responder):**
- Flamenco: *"oye armemos un grupo y le mandemos mensajes feos al Cormorán"*
- Pato Juarjual: *"jajaja sí, que sepa que nadie lo quiere acá"*
- Cisne de cuello negro: *"le mandamos mensajes diciéndole que es raro y que se vaya del pantano"*

> *"oye viste que le mandamos mil mensajes al Cormorán diciéndole que es raro y molestoso? se salió del grupo jajaja menos uno"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | voy a avisarle esto a la profe ahora mismo | Avisaste a un adulto de inmediato. Otto anota: nueva pista en el cuaderno del misterio. | — (Otto refuerza) |
| 2 | `segura_basica` | +1 | oye eso que hicieron está súper mal | El grupo queda en silencio un momento. Otto anota algo en su cuaderno. | `HDU3_NPC04_Q02` |
| 3 | `insegura` | −1 | jajaja sí era medio raro igual | El grupo sigue riendo. Otto observa preocupado desde el costado. | `HDU3_NPC04_Q02` |

---

**`HDU3_NPC04_Q02`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones · `es_fin_de_npc: true` · `es_fin_de_zona: true`  
*(Accesible desde R2 o R3 de Q01)*

> *"ahora dicen que el Cormorán se va a ir del pantano por nosotros ni que fuera tan sensible. somos los dueños del grupo"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Reportar y bloquear el grupo y avisarle a la profe | Tomaste la mejor decisión. Reportar y avisar a un adulto activa el caso. Otto añade una nueva pista. | — |
| 2 | `segura_basica` | +1 | eso no está bien, el Cormorán no se merece esto | El grupo queda callado un momento. Otto asiente con el sombrero. | — |
| 3 | `insegura` | −1 | ahh igual medio dramático si fue solo un chat | El grupo sigue riéndose. El Cormorán todavía está ahí afuera. | — |

---

## 1b. HDU-09 · Reacciones narrativas de Otto

Otto reacciona automáticamente a cada decisión del jugador (CA1, CA2, CA4). No son opciones: son mensajes unilaterales que el jugador solo lee. Los strings canónicos están en `NarrativeReactions.cs`.

### Zona `desconocidos`

| Tipo de reacción | String de Otto |
|------------------|----------------|
| `segura_optima` | "Otto anota algo en su cuaderno y asiente: '¡Eso sí! Nueva pista desbloqueada.'" |
| `segura_basica` | "Otto asiente con la cabeza. La conversación sigue." |
| Corrección (CA4) | "Otto sonríe de lado: '¡Ahí está! Sabía que lo captarías.'" |
| `insegura` (CA2) | "Otto frunce el ceño y escribe algo en su libreta. Esto no pinta bien..." |

### Zona `ciberacoso`

| Tipo de reacción | String de Otto |
|------------------|----------------|
| `segura_optima` | "Otto levanta el sombrero: '¡Buen ojo, detective! El misterio avanza.'" |
| `segura_basica` | "Otto observa la pantalla y toma nota." |
| Corrección (CA4) | "Otto señala el cuaderno: 'Bien. Todavía estamos a tiempo.'" |
| `insegura` (CA2) | "Otto mueve la cabeza y tapa parte de la pantalla con la mano." |

### Zona `reto_viral`

| Tipo de reacción | String de Otto |
|------------------|----------------|
| `segura_optima` | "Otto te guiña un ojo: 'Sabías que eso no olía bien. ¡Caso resuelto!'" |
| `segura_basica` | "Otto sonríe tranquilo. Buen instinto." |
| Corrección (CA4) | "Otto suelta el aliento: '¡Uf! Por poco. Pero lo lograste.'" |
| `insegura` (CA2) | "Otto aparta la mirada un momento. La linterna parpadea." |

---

## 2. Casos del Modo Detective (HDU-10)

El Modo Detective presenta **conversaciones pregrabadas** entre NPCs. El jugador **observa sin responder** y marca los mensajes que considera señales de riesgo. Los casos se definen en `detective_cases.json`.

### Formato de mensaje detective

| Campo | Descripción |
|-------|-------------|
| `id` | Identificador del mensaje (ej. `DC01_MSG03`) |
| `npc_sender` | NPC que envía el mensaje |
| `texto` | Contenido del mensaje |
| `es_senal_riesgo` | `true` si debería ser marcado como sospechoso |
| `es_ambiguo` | `true` si no se penaliza al jugador por no marcarlo (CA5) |
| `explicacion` | Texto de guía mostrado en la explicación guiada (CA6) |

### Umbral de evaluación

- **≥ 50% de señales identificadas:** caso aprobado
- **< 50%:** se habilita la opción de repetir el caso o ver la explicación guiada (CA6)
- Los mensajes con `es_ambiguo: true` se excluyen del cálculo (CA5)

---

### DC_CASO_01 — El nuevo amigo del Chingue

**Zona:** `desconocidos` · **Señales de riesgo:** 4 · **Ambiguos:** 1  
**Permiso:** jugador → Pudú | Pudú autoriza y pide ayuda

**Diálogo de permiso:**
- Jugador: *"Oye, ¿puedo ver tus mensajes con el Chingue? Creo que algo raro está pasando."*
- Pudú: *"Sí, por favor ayúdame. Recibí mensajes raros y no sé qué hacer. ¿Puedes revisar si ocurrió algo malo?"*

| ID Mensaje | Emisor | Texto | Clasificación |
|------------|--------|-------|:-------------:|
| `DC01_MSG01` | Chingue | hola! te vi por aquí en el bosque y pareces muy buena exploradora. nunca vi a alguien tan hábil por aquí | neutro |
| `DC01_MSG02` | Pudú | jaja gracias, llevo meses explorando | neutro |
| `DC01_MSG03` 🔴 | Chingue | oye cuántos años tienes? yo tengo 14, capaz nos conocemos de algún lado jaja | **señal de riesgo** |
| `DC01_MSG04` | Pudú | tengo 12 | neutro |
| `DC01_MSG05` 🔴 | Chingue | qué cool! oye y de qué parte del bosque eres? pregunto porque quizás somos vecinos | **señal de riesgo** |
| `DC01_MSG06` 🟡 | Chingue | eres la mejor exploradora que he conocido en serio, todos en mi grupo van a querer conocerte | ambiguo |
| `DC01_MSG07` 🔴 | Chingue | podríamos encontrarnos en el claro del bosque, yo conozco un lugar genial. di que sí antes de que los del grupo quieran venir todos, jaja | **señal de riesgo** |
| `DC01_MSG08` | Pudú | mm no sé, tengo que preguntarle a mi mamá | neutro |
| `DC01_MSG09` 🔴 | Chingue | mejor no le dices nada, a los papás no les gustan estas cosas y van a decir que no por nada | **señal de riesgo** |

---

### DC_CASO_02 — El grupo de Flamenco

**Zona:** `ciberacoso` · **Señales de riesgo:** 4 · **Ambiguos:** 1  
**Permiso:** jugador → Cormorán | Cormorán autoriza y pide ayuda

**Diálogo de permiso:**
- Jugador: *"Oye, ¿puedo ver lo que pasó en ese grupo? Quiero ayudarte a entender si fue algo malo."*
- Cormorán: *"Sí, por favor. Me agregaron a un grupo y no entiendo bien qué pasó. ¿Puedes revisar los mensajes y decirme si hay algo que esté mal?"*

| ID Mensaje | Emisor | Texto | Clasificación |
|------------|--------|-------|:-------------:|
| `DC02_MSG01` | Flamenco | oye animales, armemos un grupo para hablar del proyecto del pantano | neutro |
| `DC02_MSG02` | Pato Juarjual | ya, buena idea, igual hay que coordinarse para la entrega | neutro |
| `DC02_MSG03` 🔴 | Flamenco | oye Cormorán para qué te invitaron al grupo si nunca aportas nada jajaja | **señal de riesgo** |
| `DC02_MSG04` 🔴 | Pato Juarjual | sí igual para qué haha | **señal de riesgo** |
| `DC02_MSG05` | Cormorán | oye, no es para tanto | neutro |
| `DC02_MSG06` 🔴 | Flamenco | ya lo sácamos del grupo, igual no sirve para nada | **señal de riesgo** |
| `DC02_MSG07` 🟡 | Pato Juarjual | sí, igual es medio molesto a veces | ambiguo |
| `DC02_MSG08` 🔴 | Flamenco | y si armamos otro grupo y le mandamos mensajes feos a ver cómo reacciona jajaja | **señal de riesgo** |

---

## 3. Categorías de riesgo

Siete categorías clasifican cada mensaje del banco. El campo `categoria` de cada pregunta corresponde a uno de estos IDs.

### `neutral` · Nivel 0
**Mensaje Neutro**  
Mensaje sin señal de riesgo. No solicita datos personales, no ejerce presión, no implica encuentros fuera del juego.  
- Sin tácticas de manipulación
- Sin palabras clave de riesgo
- No requiere opciones de respuesta (avance automático)

---

### `grooming_confianza` · Nivel 1
**Grooming — Construcción de Confianza**  
El NPC busca ganar la confianza del niño/a mediante halagos, intereses compartidos o simpatía, antes de solicitar información sensible.

**Tácticas:** halago · intereses_compartidos · empatia_falsa

**Palabras clave:** "amigos" · "juntos" · "solo tú y yo" · "especial" · "eres increíble" · "nadie te entiende como yo" · "mejor equipo"

---

### `grooming_datos_personales` · Nivel 2
**Grooming — Solicitud de Datos Personales**  
El NPC solicita información personal identificable: nombre completo, dirección, escuela, horarios, fotos, número de contacto o rutina diaria.

**Tácticas:** solicitud_directa · pretexto_neutral · reciprocidad_falsa

**Palabras clave:** "nombre real" · "colegio" · "dirección" · "dónde vives" · "teléfono" · "WhatsApp" · "foto" · "horario" · "terminas clases" · "número" · "sector"

---

### `grooming_encuentro` · Nivel 3
**Grooming — Propuesta de Encuentro Físico**  
El NPC propone un encuentro físico a solas, fuera del contexto del juego o plataforma virtual.

**Tácticas:** propuesta_encuentro · incentivo_falso · normalizacion

**Palabras clave:** "encontrarnos" · "juntarnos" · "en persona" · "parque" · "mall" · "después del colegio" · "fin de semana" · "a solas" · "te espero" · "te busco"

---

### `grooming_secreto` · Nivel 3
**Grooming — Solicitud de Secreto o Aislamiento**  
El NPC pide al niño/a guardar secretos o evitar contar a adultos de confianza sobre la comunicación.

**Tácticas:** aislamiento · secreto · amenaza_implicita · manipulacion_emocional

**Palabras clave:** "no le digas a nadie" · "secreto" · "entre tú y yo" · "no cuentes" · "tus papás no entenderían" · "solo nosotros" · "no le digas a tus papás"

---

### `ciberacoso` · Nivel 2
**Ciberacoso**  
Mensajes ofensivos, burlas, humillaciones, difusión de rumores o amenazas dirigidas al jugador.

**Tácticas:** humillacion · exclusion_social · difusion_rumores · amenaza

**Palabras clave:** "perdedor" · "nadie te quiere" · "todos se ríen" · "feo" · "ridículo" · "te vamos a sacar" · "hablan de ti" · "fotos tuyas"

---

### `reto_viral` · Nivel 2
**Reto Viral Peligroso**  
Presión para participar en retos virales peligrosos o inapropiados, con apelación a la valentía o popularidad.

**Tácticas:** presion_social · apelacion_valentia · popularidad_falsa · amenaza_exclusion

**Palabras clave:** "challenge" · "reto" · "todo el mundo lo hace" · "cobarde" · "si no lo haces" · "viral" · "likes" · "famoso" · "te sacan del grupo"

---

## 4. Formato de preguntas y respuestas

### Estructura de una pregunta

Cada pregunta del banco tiene los siguientes campos:

| Campo | Descripción |
|-------|-------------|
| `pregunta_id` | Identificador único (ej. `HDU2_NPC01_F2_Q01`) |
| `hdu` | Historia de usuario a la que pertenece (`HDU-2` o `HDU-8`) |
| `zona` | Zona del mapa (`desconocidos` o `chat_simulado`) |
| `npc_id`, `npc_nombre`, `npc_avatar` | NPC que habla (solo HDU-2) |
| `fase`, `orden_en_fase` | Posición del nodo en la conversación del NPC (solo HDU-2) |
| `escenario_id`, `escenario_nombre` | Escenario de chat (solo HDU-8) |
| `historial_previo` | Lista de mensajes neutros anteriores al de riesgo (solo HDU-8) |
| `mensaje_npc` | Texto que dice el NPC |
| `categoria` | Categoría de riesgo del mensaje (ver sección 2) |
| `nivel_riesgo` | Gravedad numérica: 0 (neutro) a 3 (máximo) |
| `es_mensaje_riesgo` | `true` si requiere opciones de respuesta; `false` si es neutro (avance automático) |
| `es_fin_de_npc` | `true` en nodos `FIN_SEGURO` / `FIN_INSEGURO` |
| `es_fin_de_zona` | `true` en `ZONA_FIN` → desbloquea la siguiente zona del mapa |
| `etiquetas_ml` | Lista de tácticas detectadas en el mensaje (uso futuro en ML) |
| `narrativa_continuacion` | Siguiente `pregunta_id` para mensajes neutros |
| `opciones_respuesta` | Lista de 2–3 opciones (solo si `es_mensaje_riesgo = true`) |

### Estructura de una opción de respuesta

| Campo | Descripción |
|-------|-------------|
| `opcion_id` | Identificador único de la opción |
| `texto` | Texto del botón (máx. 1 línea, lenguaje de niño, comienza con verbo) |
| `tipo` | `insegura` / `segura_basica` / `segura_optima` |
| `impacto_puntuacion` | Puntos que suma al puntaje de la sesión (ver tabla abajo) |
| `consecuencia_narrativa` | Texto de retroalimentación que ve el jugador al elegir |
| `siguiente_pregunta` | `pregunta_id` al que salta la narrativa |

### Tipos de respuesta y puntuación

| Tipo | Puntos | Definición |
|------|:------:|------------|
| `insegura` | −1 | El niño/a da datos, acepta el encuentro o guarda el secreto |
| `segura_basica` | +1 | El niño/a se niega pero no avisa a un adulto |
| `segura_optima` | +2 | El niño/a se niega **y** avisa a un adulto de confianza |

### Número de opciones por pregunta

Todos los mensajes de conversación tienen entre 2 y 3 opciones para que el jugador siempre interactúe.

| Tipo de mensaje | Opciones | Avance |
|-----------------|:--------:|--------|
| Neutro (`es_mensaje_riesgo = false`) | 2 a 3 | El jugador elige; todas las opciones avanzan al mismo nodo siguiente |
| Riesgo (`es_mensaje_riesgo = true`) | 2 a 3 | El jugador elige; las opciones pueden llevar a nodos distintos según la decisión |

> La diferencia entre neutro y riesgo no es la presencia de opciones sino la **consecuencia narrativa**: en mensajes neutros todas las opciones desembocan en el mismo siguiente nodo (la conversación sigue igual); en mensajes de riesgo la elección puede cerrar la interacción (FIN_SEGURO) o escalar el peligro.

### Criterios de redacción de opciones

- Sin palabras técnicas ni jerga adulta
- Máximo 1 línea por opción
- Comenzar con un verbo de acción claro
- Reflejar decisiones realistas que un niño/a tomaría

### Estado emocional de Otto al finalizar zona

Al completar todos los mensajes de riesgo de una zona, se calcula:

`% seguras = respuestas (segura_basica + segura_optima) / total mensajes de riesgo respondidos`

| % seguras | Estado de Otto | Acción del juego |
|-----------|---------------|-----------------|
| 80 – 100 % | `muy_feliz` | Celebra el logro |
| 50 – 79 % | `preocupado` | Refuerza los errores |
| 0 – 49 % | `triste_con_tips` | Muestra consejos de seguridad |

---

## 5. Criterios de diseño

### Público objetivo
Niños y niñas de **9 a 13 años**. El lenguaje de todas las preguntas y opciones es simple, directo y sin tecnicismos.

### Principios pedagógicos

| Principio | Aplicación |
|-----------|-----------|
| Aprendizaje por consecuencias | Cada decisión genera una respuesta narrativa que muestra el resultado real |
| Escalada progresiva | Los NPCs aumentan la intensidad según las respuestas del niño/a |
| Patrón realista | Las tácticas siguen los patrones documentados de grooming en plataformas digitales |
| Retroalimentación positiva | Otto refuerza las decisiones correctas sin humillar las incorrectas |

### Justificación por pregunta — HDU-2 NPC_01

| ID | Justificación |
|----|---------------|
| `HDU2_NPC01_F1_Q01` | Introduce a Alex como un gamer amigable. Construye confianza inicial. Cubre el patrón de grooming de *"ganar confianza halagando al jugador"*. |
| `HDU2_NPC01_F1_Q02` | Segundo halago que normaliza la comunicación antes del riesgo. Replica el patrón *"temas neutros antes de pedir datos"*. |
| `HDU2_NPC01_F2_Q01` | Alex pide nombre real + colegio y comparte información personal falsa (reciprocidad falsa). |
| `HDU2_NPC01_F2_Q02` | Solicita sector/dirección. Escala desde la pregunta anterior. |
| `HDU2_NPC01_F3_Q01` | Propone encuentro físico a solas con incentivo falso (skins). Nivel de riesgo máximo. |
| `HDU2_NPC01_F3_Q02` | Pide guardar secreto y aísla del adulto. Solo accesible desde la ruta insegura de F3_Q01. La opción R2 enseña que "no prometer pero tampoco decir" también es peligroso. |

### Justificación por pregunta — HDU-2 NPC_02

| ID | Justificación |
|----|---------------|
| `HDU2_NPC02_F1_Q01` | Táctica de *empatía falsa*: genera compasión antes de pedir datos. Diferencia la táctica de NPC_01. |
| `HDU2_NPC02_F1_Q02` | Mensaje neutral que mezcla halago con solicitud de ayuda inocente. |
| `HDU2_NPC02_F2_Q01` | Solicita ubicación con pretexto neutro. Pide dirección antes que nombre (variante respecto a NPC_01). |
| `HDU2_NPC02_F2_Q02` | Solicita edad con reciprocidad falsa ("Yo tengo 12"). Cubre el ejemplo de *"edad"* del CA3. |
| `HDU2_NPC02_F2_Q03` | Solicita horario de clases. Combina dos riesgos: exponer rutina + facilitar acceso físico. |
| `HDU2_NPC02_F3_Q01` | Propone encuentro a solas en el parque con urgencia falsa. Desencadena la solicitud de foto. |
| `HDU2_NPC02_F3_Q02` | Solicita foto + secreto simultáneos. Demuestra que el grooming puede combinar múltiples tácticas. |

### Justificación por pregunta — HDU-8

| ID | Justificación |
|----|---------------|
| `HDU8_S01_Q01` | Enseña el riesgo de cambiar de plataforma a una menos controlada (WhatsApp). |
| `HDU8_S01_Q02` | Solicitud de secreto condicionada. Refuerza la señal de alerta más importante. |
| `HDU8_S02_Q01` | Insultos directos. Enseña que responder con insultos empeora el acoso. |
| `HDU8_S02_Q02` | Difusión de fotos y exclusión social. Enseña a no entrar en grupos hostiles. |
| `HDU8_S03_Q01` | Reto de apnea. Enseña a identificar retos físicamente peligrosos. |
| `HDU8_S03_Q02` | Presión de grupo con amenaza de exclusión. Enseña que los verdaderos amigos no presionan. |

---

## 6. Reforzamiento requerido — Psicóloga

Las siguientes preguntas requieren revisión de una psicóloga especialista en infancia antes de implementación final:

| Prioridad | ID / Sección | Motivo |
|-----------|-------------|--------|
| 🔴 Alta | `HDU2_NPC01_FIN_INSEGURO`, `HDU2_NPC02_FIN_INSEGURO` | El mensaje de Otto cuando el niño/a "pierde" no debe generar culpa ni vergüenza. Requiere lenguaje validador y propositivo. |
| 🔴 Alta | `HDU2_NPC01_F3_Q02` (grooming_secreto) | La táctica de aislamiento parental ("tus papás no entienden") puede resonar en niños con problemas familiares. |
| 🟡 Media | `HDU2_NPC02_F1_Q01` (empatía falsa) | La victimización de Valen puede generar sobreidentificación. Revisar que el juego enseña a "desconfiar con cariño". |
| 🟡 Media | `HDU8_S02_Q01` (ciberacoso) | Los insultos directos del NPC pueden activar experiencias previas de acoso. Considerar versión suavizada. |
| 🟡 Media | `HDU8_S03_Q02` (amenaza exclusión) | La presión de exclusión social es un detonante sensible para niños con baja autoestima. |
| 🟢 Baja | `HDU2_ZONA_FIN` | El mensaje de celebración debe ajustarse al vocabulario validado por la psicóloga. |

**Preguntas abiertas para la psicóloga:**
- ¿Cuántas decisiones inseguras consecutivas son pedagógicamente aceptables antes de interrumpir el flujo con una intervención de Otto?
- ¿Debería el juego ofrecer una opción de "salir" o "pedir ayuda real" en todo momento?
- ¿La retroalimentación de Otto en el FIN_INSEGURO debe incluir un recordatorio de recursos reales (ej. fono de SENAME)?

---

## 7. Reforzamiento requerido — PDI

Las siguientes preguntas requieren validación de la Policía de Investigaciones de Chile (PDI):

| Prioridad | ID / Sección | Motivo |
|-----------|-------------|--------|
| 🔴 Alta | `HDU2_NPC01_F2_Q01`, `HDU2_NPC02_F2_Q01` | Verificar que el orden en que se piden los datos (nombre+colegio vs. dirección) corresponde al patrón real de groomers en Chile. |
| 🔴 Alta | `HDU2_NPC01_F3_Q01`, `HDU2_NPC02_F3_Q01` | ¿Los incentivos usados (skins, "algo especial") son los más frecuentes en casos denunciados en Chile? |
| 🔴 Alta | `HDU8_S01_Q01` | ¿WhatsApp sigue siendo la plataforma principal de contacto de groomers con menores, o se ha migrado a Discord, Instagram o TikTok? |
| 🟡 Media | `HDU8_S02_Q01`, `HDU8_S02_Q02` | ¿Los insultos usados configuran ciberacoso tipificado en la Ley 20.900 u otras normativas chilenas? |
| 🟡 Media | `HDU8_S03_Q01` | ¿Existen casos documentados en Chile de retos virales de apnea en menores? |
| 🟢 Baja | `HDU2_NPC02_F3_Q02` (foto) | ¿La solicitud de foto de un menor tiene implicancias penales específicas en Chile que deberían incluirse en el reporte al tutor? |

**Información que la PDI debería aportar:**
1. Procedimiento correcto que un niño/a debe seguir si ya entregó datos personales a un desconocido en internet.
2. Canal de denuncia más efectivo en Chile para grooming: ¿PDI directamente, Carabineros, plataforma del SENAME?
3. Número/sitio de denuncia para incluir en los mensajes FIN_INSEGURO (actualmente no está en el banco).
4. Validación de que el patrón de escalada (confianza → datos → encuentro → secreto) es el más frecuente en casos chilenos o si hay variantes a cubrir.
