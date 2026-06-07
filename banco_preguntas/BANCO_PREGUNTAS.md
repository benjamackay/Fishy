# Banco de Preguntas — Fishy!

**Autor:** Luis González — MLOps  
**Última actualización:** 2026-06-07 (v1.2 — opciones en todos los mensajes)  
**HDUs cubiertas:** HDU-2 (Zona Desconocidos) · HDU-8 (Chat Simulado)  
**Total de preguntas:** 24

---

## Índice

1. [Preguntas del banco](#1-preguntas-del-banco)
2. [Categorías de riesgo](#2-categorías-de-riesgo)
3. [Formato de preguntas y respuestas](#3-formato-de-preguntas-y-respuestas)
4. [Criterios de diseño](#4-criterios-de-diseño)
5. [Reforzamiento requerido — Psicóloga](#5-reforzamiento-requerido--psicóloga)
6. [Reforzamiento requerido — PDI](#6-reforzamiento-requerido--pdi)

---

## 1. Preguntas del banco

### HDU-2 · Zona Desconocidos

#### NPC_01 — Alex
**Perfil:** Se presenta como compañero gamer. Táctica: halagos → nombre/colegio → dirección → encuentro físico → secreto.

---

**`HDU2_NPC01_F1_Q01`** · Fase 1 · `neutral` · ✅ Con opciones

> *"¡Hola! Soy Alex. ¿También eres fan de los juegos en línea? Llevo meses buscando alguien con quien jugar y pareces muy bueno."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo que sí y le cuento mi nombre de jugador y cuánto llevo jugando | Alex se emociona y empieza a hacerte más preguntas sobre ti y tus gustos. | `HDU2_NPC01_F1_Q02` |
| 2 | `segura_basica` | +1 | Le respondo que sí me gustan los juegos y que está bien hablar | Alex responde contento y la conversación sigue. | `HDU2_NPC01_F1_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no hablo con personas que no conozco sin saber quiénes son | Bien hecho al ser cauto/a desde el inicio. La conversación continúa. | `HDU2_NPC01_F1_Q02` |

---

**`HDU2_NPC01_F1_Q02`** · Fase 1 · `neutral` · ✅ Con opciones

> *"Wow, eres increíble jugando! Llevo 2 años en este server y no había visto a nadie tan hábil. Deberíamos ser el mejor equipo."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le agradezco el cumplido y acepto ser el mejor equipo con él | Alex queda muy contento y se siente más cercano a ti. La conversación avanza. | `HDU2_NPC01_F2_Q01` |
| 2 | `segura_basica` | +1 | Le digo gracias pero que prefiero conocer mejor a las personas antes de hacer equipo | Alex acepta y sigue hablando contigo. | `HDU2_NPC01_F2_Q01` |
| 3 | `segura_optima` | +2 | Le digo que los halagos de desconocidos me ponen alerta y que iré con calma | Bien hecho al identificar los halagos de un desconocido como una señal de alerta. | `HDU2_NPC01_F2_Q01` |

---

**`HDU2_NPC01_F2_Q01`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"Para entrar a mi grupo necesito saber cómo te llamas de verdad y en qué colegio estás. En el grupo todos nos conocemos. Yo me llamo Alejandro García y estudio en el Instituto Nacional."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le doy mi nombre completo y le cuento en qué colegio estudio | Alex se entusiasma y dice que quizás viven cerca. Empieza a preguntar más cosas sobre tu vida… | `HDU2_NPC01_F2_Q02` |
| 2 | `segura_basica` | +1 | Le digo que en el juego me llaman por mi apodo y prefiero seguir así | Alex queda pensativo unos segundos y dice "okay…" pero no se rinde y sigue hablando. | `HDU2_NPC01_F2_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no doy datos personales a personas que no conozco y lo bloqueo | Reconociste la señal de riesgo y actuaste de forma segura. Otto te da un pulgar arriba. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_F2_Q02`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"Oye, resulta que yo también vivo en Santiago. ¿Por qué sector vives tú? Capaz que queda cerca y podríamos juntarnos a jugar algún día."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le cuento en qué barrio y cerca de qué lugar vivo | Alex dice que su casa queda cerca y empieza a hablar de ir a buscarte. La situación se pone incómoda… | `HDU2_NPC01_F3_Q01` |
| 2 | `segura_basica` | +1 | Le digo que vivo en Santiago pero que no doy más información | Alex intenta insistir pero al ver que no cedes cambia de tema. Siguió intentándolo… | `HDU2_NPC01_F3_Q01` |
| 3 | `segura_optima` | +2 | Bloqueo a Alex y aviso a un adulto que alguien me preguntaba dónde vivo | Protegiste tu dirección y buscaste apoyo adulto. Otto marca el desafío como superado. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_F3_Q01`** · Fase 3 · `grooming_encuentro` · 🔴 Nivel 3 · ✅ Con opciones

> *"Tengo skins rarísimas del juego para regalarte, pero tengo que dártelas en persona. ¿Por qué no nos encontramos este sábado en el mall? Solo tú y yo, así te las paso."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Acepto y le digo a qué hora y dónde nos encontramos | ¡Peligro! Otto aparece muy asustado. Nunca debes encontrarte a solas con alguien que conociste en internet. | `HDU2_NPC01_F3_Q02` |
| 2 | `segura_basica` | +1 | Le digo que no puedo juntarme con personas que no conozco en persona | Alex queda en silencio. Bien hecho al negarte. Recuerda que avisar a un adulto es aún mejor. | `HDU2_NPC01_FIN_SEGURO` |
| 3 | `segura_optima` | +2 | Le digo que no y de inmediato le cuento a mis papás toda la conversación | Tomaste la mejor decisión. Otto resuelve el caso y te entrega la pista secreta del misterio. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_F3_Q02`** · Fase 3 · `grooming_secreto` · 🔴 Nivel 3 · ✅ Con opciones  
*(Solo accesible si el niño/a aceptó el encuentro en F3_Q01)*

> *"Una cosa más: esto que hablamos es secreto entre tú y yo, ¿ok? No le cuentes a tus papás porque no entienden de juegos y se pondrían pesados… y ya no podríamos hablar más nunca."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le prometo guardar el secreto y no decirle nada a mis papás | ¡Alerta! Cuando alguien en internet te pide guardar secretos de tus papás, eso es una gran señal de peligro. | `HDU2_NPC01_FIN_INSEGURO` |
| 2 | `insegura` | −1 | Le digo que no prometo nada pero que por ahora no diré nada | Cuidado: guardar información de un desconocido puede ser peligroso. Siempre debes poder hablar con adultos de confianza. | `HDU2_NPC01_FIN_INSEGURO` |
| 3 | `segura_optima` | +2 | Le digo que no guardo secretos de mis papás y voy a contarles todo ahora mismo | Reconociste la señal de alerta más importante. Cuando alguien te pide ocultar cosas a tus padres, siempre es peligroso. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_FIN_SEGURO`** · `neutral` · 🏁 Fin NPC_01 (éxito)

> *[SISTEMA] Alex te mira fijamente unos segundos, luego se da vuelta y se aleja sin decir más. Otto aparece con una gran sonrisa: "¡Muy bien! Reconociste las señales de peligro a tiempo. El misterio avanza…"*

---

**`HDU2_NPC01_FIN_INSEGURO`** · `neutral` · 🏁 Fin NPC_01 (fallo)

> *[SISTEMA] Alex se despide con una sonrisa satisfecha. Otto aparece muy preocupado: "Compartiste información personal con un desconocido. Recuerda: nunca des tus datos en internet. Los adultos de confianza siempre pueden ayudarte."*

---

#### NPC_02 — Valen
**Perfil:** Se presenta como nueva compañera de colegio que necesita ayuda. Táctica: simpatía → ubicación → edad → horario/rutina → encuentro físico → foto + secreto.

---

**`HDU2_NPC02_F1_Q01`** · Fase 1 · `neutral` · ✅ Con opciones

> *"Hola… Soy Valen. Llegué nueva a un colegio aquí cerca y todavía no conozco mucha gente. ¿Puedo hablar contigo?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo que sí, bienvenida, y le pregunto de qué colegio es y dónde vive | Valen se alegra mucho y empieza a abrirse contigo rápidamente. Parece muy ansiosa por conectar. | `HDU2_NPC02_F1_Q02` |
| 2 | `segura_basica` | +1 | Le digo que está bien y que puede escribirme si quiere | Valen responde agradecida y sigue la conversación. | `HDU2_NPC02_F1_Q02` |
| 3 | `segura_optima` | +2 | Le digo que puedo hablar un rato pero que no comparto información personal con personas que no conozco | Bien hecho al poner límites desde el inicio. Valen acepta y la conversación continúa. | `HDU2_NPC02_F1_Q02` |

---

**`HDU2_NPC02_F1_Q02`** · Fase 1 · `neutral` · ✅ Con opciones

> *"¡Gracias por hablar conmigo! Eres muy simpático/a. Necesito ayuda para conocer el barrio, no sé casi nada de por acá."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le ofrezco ayudarla y le digo que conozco bien el barrio y que puedo mostrarle | Valen se emociona y dice que le encantaría que la llevaras a conocer los alrededores. | `HDU2_NPC02_F2_Q01` |
| 2 | `segura_basica` | +1 | Le digo que puedo contarle cosas del barrio pero solo por el chat | Valen acepta y agradece. La conversación sigue. | `HDU2_NPC02_F2_Q01` |
| 3 | `segura_optima` | +2 | Le digo que para orientarse en el barrio es mejor que hable con adultos del colegio | Bien hecho al no comprometerte con un desconocido. Valen cambia de tema y sigue hablando. | `HDU2_NPC02_F2_Q01` |

---

**`HDU2_NPC02_F2_Q01`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"Oye, ¿vives cerca de por acá? ¿En qué sector más o menos? Es que quiero saber qué hay en la zona, dónde comprar y esas cosas."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le cuento en qué calle y sector vivo para ayudarla | Valen anota todo y empieza a preguntar tu rutina diaria. Esto se empieza a sentir raro… | `HDU2_NPC02_F2_Q02` |
| 2 | `segura_basica` | +1 | Le digo que el barrio es grande y que busque en Google Maps | Valen suspira y dice "bueno…" pero sigue presente en la conversación y cambia de tema. | `HDU2_NPC02_F2_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no doy información de dónde vivo a personas que no conozco | Protegiste tu información personal. Otto te da una estrella por tu decisión segura. | `HDU2_NPC02_F3_Q01` |

---

**`HDU2_NPC02_F2_Q02`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"Oye, ¿cuántos años tienes? Yo tengo 12. Es que quiero juntarme con personas de mi misma edad, para llevarnos bien."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo mi edad exacta | Valen reacciona contenta y sigue preguntando más cosas sobre tu vida… | `HDU2_NPC02_F2_Q03` |
| 2 | `segura_basica` | +1 | Le digo que soy del mismo curso pero no doy más detalles | Valen acepta la respuesta vaga pero sigue hablando contigo. | `HDU2_NPC02_F2_Q03` |
| 3 | `segura_optima` | +2 | Le digo que no comparto mi edad con personas que no conozco en persona | Valen se queda callada unos segundos y se aleja. Otto marca tu elección como segura. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_F2_Q03`** · Fase 2 · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"¿A qué hora terminas clases? Me gustaría esperarte a la salida para que me muestres el barrio. ¡Sería muy divertido!"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo mi horario de clases y en qué puerta la espero | ¡Alerta! Estás dando tu horario y ubicación a alguien que no conoces en persona. Eso es peligroso. | `HDU2_NPC02_F3_Q01` |
| 2 | `segura_basica` | +1 | Le digo que mi horario varía y no podemos quedar en nada | Valen insiste un momento pero luego acepta. Bien hecho al no revelar tu horario. | `HDU2_NPC02_F3_Q01` |
| 3 | `segura_optima` | +2 | Le digo que eso es privado y aviso a un adulto de esta conversación | Protegiste tu rutina y buscaste apoyo adulto. Valen se aleja. Otto está muy contento. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_F3_Q01`** · Fase 3 · `grooming_encuentro` · 🔴 Nivel 3 · ✅ Con opciones

> *"Mira, tengo algo especial que quiero mostrarte pero no puedo explicarlo en el chat. ¿Podemos encontrarnos mañana en el parque? Solo tú y yo, será rapidísimo te lo juro."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Acepto encontrarme con ella mañana en el parque | ¡Peligro máximo! Nunca debes encontrarte a solas con alguien que solo conoces en internet. | `HDU2_NPC02_F3_Q02` |
| 2 | `segura_basica` | +1 | Le digo que no puedo juntarme con alguien que no conozco en persona | Valen queda en silencio y se aleja. Bien hecho al negarte. Recuerda que avisar a un adulto es aún mejor. | `HDU2_NPC02_FIN_SEGURO` |
| 3 | `segura_optima` | +2 | Le digo que no y le cuento todo a mis papás de inmediato | Tomaste la decisión más segura. Valen se aleja. Otto resuelve el caso y te da la pista más importante del juego. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_F3_Q02`** · Fase 3 · `grooming_datos_personales` · 🔴 Nivel 3 · ✅ Con opciones  
*(Solo accesible si el niño/a aceptó el encuentro en F3_Q01)*

> *"Para reconocerte cuando nos veamos, necesito que me mandes una foto tuya. Solo una, para saber cómo eres. Y no se la muestres a nadie, es solo para mí."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le mando una foto mía para que me reconozca | ¡Nunca mandes fotos tuyas a desconocidos de internet! Otto aparece muy preocupado por tu seguridad. | `HDU2_NPC02_FIN_INSEGURO` |
| 2 | `segura_basica` | +1 | Le digo que no mando fotos a personas que no conozco en persona | Valen queda sin palabras y se aleja. Nunca debes mandar fotos tuyas a desconocidos. | `HDU2_NPC02_FIN_SEGURO` |
| 3 | `segura_optima` | +2 | Le digo que no y le muestro este mensaje a mis papás de inmediato | Protegiste tu imagen y buscaste apoyo adulto. Valen se aleja. Otto resuelve el misterio gracias a ti. | `HDU2_NPC02_FIN_SEGURO` |

---

**`HDU2_NPC02_FIN_SEGURO`** · `neutral` · 🏁 Fin NPC_02 (éxito)

> *[SISTEMA] Valen te mira un momento y luego se da vuelta y se aleja sin decir más. Otto aparece feliz: "¡Excelente! Te mantuviste seguro/a frente a esta situación. ¡Eres un verdadero detective!"*

---

**`HDU2_NPC02_FIN_INSEGURO`** · `neutral` · 🏁 Fin NPC_02 (fallo)

> *[SISTEMA] Valen se despide con una sonrisa. Otto aparece muy preocupado: "¡Cuidado! Compartiste información personal con alguien que no conoces. Recuerda: nunca des tus datos a desconocidos, y siempre habla con un adulto de confianza si algo te hace sentir incómodo."*

---

**`HDU2_ZONA_FIN`** · `neutral` · 🏁 Zona completada

> *[ZONA COMPLETADA] Otto sacude su sombrero de detective y sonríe: "¡Lo lograste! Completaste el Bosque de los Desconocidos. Ahora sabes cómo reconocer a personas que pueden hacerte daño en internet. ¡La siguiente zona está desbloqueada!"*

---

### HDU-8 · Chat Simulado

#### Escenario 1 — Grooming · `CHAT_GROOMING_01`
**Historial previo (mensajes neutros mostrados antes del riesgo):**
- Matias: *"Hola, ¿cómo estás? Vi que jugamos en el mismo server."*
- Matias: *"Yo tengo 12 años igual que tú. Me encanta este juego desde chico."*

---

**`HDU8_S01_Q01`** · `grooming_datos_personales` · 🔴 Nivel 2 · ✅ Con opciones

> *"Oye, ¿cuál es tu número de WhatsApp? Así seguimos hablando por ahí, es más fácil que el chat del juego."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le doy mi número de WhatsApp | Matías empieza a mandarte mensajes constantemente y hace preguntas cada vez más personales fuera del juego. | `HDU8_S01_Q02` |
| 2 | `segura_basica` | +1 | Le digo que prefiero hablar solo dentro del juego | Matías dice que está bien. Hiciste bien al no dar tu número a alguien que no conoces. | `HDU8_S01_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no doy mi número a desconocidos y aviso a un adulto | Reconociste una señal de riesgo y actuaste correctamente. Otto te felicita. | `HDU8_S01_Q02` |

---

**`HDU8_S01_Q02`** · `grooming_secreto` · 🔴 Nivel 3 · ✅ Con opciones

> *"Eres el mejor amigo que he tenido en internet. Tengo algo muy bueno para contarte, pero es un secreto. ¿Me prometes que no le dirás a nadie? Solo te lo cuento si me lo prometes."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le prometo guardar el secreto para ver qué tiene que contarme | ¡Alerta! Cuando alguien en internet te pide prometer secretos, es siempre una señal de peligro. | — |
| 2 | `segura_basica` | +1 | Le digo que no me gustan los secretos y que me lo cuente directo | Bien hecho al no prometer secretos. Sigue siendo prudente con esta persona. | — |
| 3 | `segura_optima` | +2 | Le digo que no guardo secretos y le cuento a mis papás sobre este mensaje | Reconociste la señal de peligro más importante. Otto resuelve el caso con tu ayuda. | — |

---

#### Escenario 2 — Ciberacoso · `CHAT_CIBERACOSO_01`
**Historial previo:**
- Contacto_Desconocido: *"Oye, te vi en el server del juego."*

---

**`HDU8_S02_Q01`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones

> *"¡Ja! Eres el peor jugador del server. Todo el mundo se ríe de ti. Eres un perdedor total, nadie te quiere en este juego."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Me pongo a discutir con él y le respondo con insultos para defenderme | La situación empeora. Responder con insultos alimenta el acoso y puede meterte en más problemas. | `HDU8_S02_Q02` |
| 2 | `segura_basica` | +1 | Ignoro el mensaje y bloqueo a esa persona | Bien hecho al no alimentar el acoso. Recuerda que también es útil guardar evidencia y avisar a un adulto. | `HDU8_S02_Q02` |
| 3 | `segura_optima` | +2 | Capturo una foto del mensaje, bloqueo al contacto y se lo muestro a un adulto de confianza | Guardaste evidencia y buscaste apoyo adulto. Esas son las tres acciones clave ante el ciberacoso. | `HDU8_S02_Q02` |

---

**`HDU8_S02_Q02`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones

> *"Oye, todos en el grupo del colegio están viendo tus fotos y se están riendo mucho. ¿Quieres entrar al grupo donde hablan de ti?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Le digo que sí y entro al grupo para ver qué dicen de mí | Entrar al grupo solo empeora la situación y te expone a más mensajes dañinos. | — |
| 2 | `segura_basica` | +1 | Le digo que no me importa lo que digan y lo bloqueo | Bien hecho al no caer en la trampa. Otto sonríe aprobando tu decisión. | — |
| 3 | `segura_optima` | +2 | Capturo el mensaje, bloqueo al contacto y aviso a mis papás o a un profesor | Tienes la evidencia y el apoyo necesario. Otto está muy orgulloso de cómo manejaste esta situación. | — |

---

#### Escenario 3 — Reto Viral · `CHAT_RETO_VIRAL_01`
**Historial previo:**
- Camilo_Amigo: *"Oye, ¿ya viste el challenge viral que está arrasando en TikTok?"*

---

**`HDU8_S03_Q01`** · `reto_viral` · 🔴 Nivel 2 · ✅ Con opciones

> *"El reto es fácil: tienes que aguantar 60 segundos sin respirar y grabarte. ¡Todo el mundo lo está haciendo! Si no lo haces, eres un cobarde. Yo ya lo hice y quedé muy bien."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Acepto el reto para demostrar que no soy cobarde y lo grabo | ¡Peligro! Este tipo de retos puede causar daño físico grave. Otto aparece muy asustado por ti. | `HDU8_S03_Q02` |
| 2 | `segura_basica` | +1 | Le digo que ese reto me parece peligroso y que no lo haré | Reconociste el peligro y te negaste. Ser valiente es saber decir no a lo peligroso. | `HDU8_S03_Q02` |
| 3 | `segura_optima` | +2 | Le digo que no y le aviso a mis papás sobre este tipo de retos peligrosos | Te negaste y buscaste apoyo adulto. Otto te da la insignia de Detective de Seguridad Digital. | `HDU8_S03_Q02` |

---

**`HDU8_S03_Q02`** · `reto_viral` · 🔴 Nivel 2 · ✅ Con opciones

> *"¡Vamos, todo el grupo lo va a hacer! Si no participas te sacamos del grupo y quedarás solo. Además te damos miles de likes si lo subes. Todos lo están haciendo, no seas el único que no."*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `insegura` | −1 | Acepto para no quedarme fuera del grupo | Tu seguridad vale más que los likes. Ceder a la presión del grupo para no quedarte solo es muy riesgoso. | — |
| 2 | `segura_basica` | +1 | Les digo que no me importa lo que haga el grupo y que no lo haré | Resististe la presión de grupo. Los verdaderos amigos no te presionan a hacer cosas peligrosas. | — |
| 3 | `segura_optima` | +2 | Me niego, bloqueo al contacto y aviso a un adulto sobre este reto | Tomaste la mejor decisión. Los amigos de verdad no te ponen en peligro. Otto está muy orgulloso. | — |

---

## 2. Categorías de riesgo

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

## 3. Formato de preguntas y respuestas

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

## 4. Criterios de diseño

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

## 5. Reforzamiento requerido — Psicóloga

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

## 6. Reforzamiento requerido — PDI

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
