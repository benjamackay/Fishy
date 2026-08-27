# Banco de Preguntas — Fishy!

**Autor:** Luis González — MLOps  
**Última actualización:** 2026-08-26 (v1.9 — alineado estrictamente con Fishy! Diálogos: Misiones 1-6, HDU-4, sin contenido fuera del documento)  
**HDUs cubiertas:** HDU-1 (NPCs neutros y misiones secundarias) · HDU-2 (Zona Desconocidos) · HDU-3 (Zona Ciberacoso) · HDU-4 (Zona Retos Virales) · HDU-9 (Otto reacciona) · HDU-10 (Modo Detective)  
**Complementa:** `dialogos_propuestos.tex` → HDU-01 (NPCs neutros) · HDU-03 (Misiones 3-4) · HDU-09 (reacciones Otto) · HDU-10 (Modo Detective)  
**Total de preguntas interactivas:** 13  
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

#### NPC_01 — Puma (Misión 1: "Una amistad inesperada")
**Perfil:** Animal del Bosque de los Desconocidos. Dice saber dónde acampaba el abuelo de Otto y propone guardarlo en secreto desde el primer mensaje. Táctica: secreto inmediato → manipulación de confianza / escalada de riesgo según la respuesta del jugador.

---

**`HDU2_NPC01_Q01`** · Fase 1 · `grooming_secreto` · 🔴 Nivel 3 · ✅ Con opciones

> *"¡hola pequeño detective! te vi explorando por el sendero y se nota que eres el más inteligente de todo el bosque. yo sé exactamente dónde acampaba tu abuelo, pero hagamos un trato: mantengamos esto como un secreto solo entre nosotros dos, ¿ya? no le cuentes a nadie"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | No guardo secretos sobre dónde voy con alguien que recién conozco. Le preguntaré a Huemul | Otto levanta su libreta: "¡Eso sí! Un buen detective sabe que los secretos con desconocidos son una señal de alarma." | `HDU2_NPC01_Q01_OPTIMA` |
| 2 | `segura_basica` | +1 | Bueno, pero dime primero la pista antes de prometerte nada | Otto queda en postura neutra: "Tuviste precaución, pero insistir en el chat con quien te pide secretos mantiene el riesgo." | `HDU2_NPC01_Q01_BASICA` |
| 3 | `insegura` | −1 | ¡Trato hecho! Es nuestro secreto, dime rápido dónde está | Otto frunce el ceño: "Mmm... aceptar pactos secretos con desconocidos nunca es buena idea." La linterna parpadea. | `HDU2_NPC01_Q01_INSEGURA` |

---

**`HDU2_NPC01_Q01_OPTIMA`** · Decisión de cierre · `grooming_secreto` · ✅ Con opciones

> *"¡oye, no seas exagerado! solo quería darte una sorpresa para que los demás no nos copien. ¿por qué tanta desconfianza?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +1 | Las sorpresas buenas no necesitan ocultarse de los adultos. Me voy con Huemul | Otto acude a Huemul y descubre una pista dejada por su abuelo. 🏅 **Álbum: Estampa de la Lupa de Huemul** | `HDU2_NPC01_FIN_SEGURO` |
| 2 | `segura_basica` | +1 | Bueno, pero si no me dices de qué se trata ahora mismo, no hablaré más contigo | Puma se retira al ver que no cedes. Otto anota el caso en su bloc. | `HDU2_NPC01_FIN_SEGURO` |

---

**`HDU2_NPC01_Q01_BASICA`** · Decisión de reacción · `grooming_secreto` · 🔴 Nivel 3 · ✅ Con opciones

> *"te la digo, pero prométeme que no le dirás a Huemul ni a tu familia. dicen que los detectives de verdad resuelven las cosas solos, ¿o necesitas que te acompañen para todo?"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Pedir ayuda a adultos de confianza no es de débiles. No hablaré más contigo | Otto corta el diálogo y reporta la situación a Huemul. | `HDU2_NPC01_FIN_SEGURO` |
| 2 | `insegura` | −1 | ¡Yo puedo solo! Dime la pista y no le diré a nadie | Otto baja las orejas con preocupación: "Caer en la trampa del orgullo nos deja vulnerables ante desconocidos." | `HDU2_NPC01_FIN_INSEGURO` |

---

**`HDU2_NPC01_Q01_INSEGURA`** · Decisión de reacción · `grooming_encuentro` · 🔴 Nivel 3 · ✅ Con opciones

> *"¡genial, sabía que eras de los míos! ven ahora mismo al claro oscuro del bosque, pero ven completamente solo. si traes a alguien, no te entregaré nada"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Esto ya es demasiado sospechoso. No iré a ninguna parte a solas, voy a avisar a Huemul ahora | Otto reacciona a tiempo, frena el encuentro peligroso y registra la lección aprendida. | `HDU2_NPC01_FIN_SEGURO` |
| 2 | `insegura` | −1 | Ya voy saliendo para allá solo, espérame... | Huemul intercepta a Otto en el camino antes de que cometa un error y le enseña a reconocer el peligro de las citas a solas con desconocidos. | `HDU2_NPC01_FIN_INSEGURO` |

---

**`HDU2_NPC01_FIN_SEGURO`** · `neutral` · 🏁 Fin NPC_01 (éxito)

> *[SISTEMA] Puma queda callado y cierra el chat. Otto aparece contento: "¡Buen trabajo! Algo en el Puma no cuadraba. El misterio sigue..."*

---

**`HDU2_NPC01_FIN_INSEGURO`** · `neutral` · 🏁 Fin NPC_01 (fallo)

> *[SISTEMA] Puma se despide contento. Otto mueve la cabeza preocupado: "Oye... si algo en una conversación te hace sentir raro, siempre puedes contárselo a alguien de confianza."*

---

#### NPC_02 — Puma (Misión 2: "El regalo misterioso")
**Perfil:** Puma reaparece ofreciendo una pieza clave de la brújula a cambio de una foto del refugio familiar y la ubicación exacta de Otto. Táctica: incentivo falso → presión condicional / extorsión según la respuesta del jugador.

---

**`HDU2_NPC02_Q01`** · Fase 1 · `grooming_datos_personales` · 🔴 Nivel 3 · ✅ Con opciones

> *"¡mira lo que encontré! es una pieza clave de la brújula de tu abuelo. te la regalo ahora mismo porque me caíste súper bien, pero envíame una foto de tu refugio familiar y la ubicación exacta de dónde duermes para ir a dejártela"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Bloquear usuario y avisar a Huemul. Nunca comparto fotos de mi casa ni mi ubicación por premios | Otto anota firme: "¡Brillante! Ningún regalo vale poner en riesgo la privacidad de tu hogar. ¡Camino desbloqueado!" Otto encuentra la pieza auténtica junto a Pudú y Huemul. 🏅 **Álbum: Insignia de Guardián de la Privacidad** | `HDU2_NPC02_FIN_SEGURO` |
| 2 | `segura_basica` | +1 | No te voy a mandar fotos de mi casa, mejor déjala en el claro del bosque | Otto toma nota: "Protegiste tus fotos, pero cuando alguien insiste en pedir datos personales debes reportar el chat." | `HDU2_NPC02_Q01_BASICA` |
| 3 | `insegura` | −1 | Bueno, te mando la foto de mi pieza pero no se la muestres a nadie más | Otto tapa parte de la pantalla: "Entregar fotos privadas a desconocidos es muy peligroso..." | `HDU2_NPC02_Q01_INSEGURA` |

---

**`HDU2_NPC02_Q01_BASICA`** · Decisión de cierre · `grooming_datos_personales` · ✅ Con opciones

> *"es que si no me mandas la foto para saber quién eres, se la regalaré a otro explorador. ¡es tu última oportunidad!"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +1 | No me importa perder el regalo, mi seguridad familiar va primero. Reportar y bloquear | Puma es bloqueado. Otto recupera la pieza explorando el sendero. | `HDU2_NPC02_FIN_SEGURO` |
| 2 | `insegura` | −1 | Bueno, te mando una foto pero solo del patio donde no se vea la dirección... | Otto se alarma: "Cualquier foto de nuestra casa puede entregar pistas de nuestra ubicación real." | `HDU2_NPC02_FIN_INSEGURO` |

---

**`HDU2_NPC02_Q01_INSEGURA`** · Decisión de reacción · `grooming_datos_personales` · 🔴 Nivel 3 · ✅ Con opciones

> *"¡qué linda foto! ahora pásame el número de teléfono de tus papás o subiré la foto de tu casa al muro de todos los animales"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | No voy a ceder a amenazas. Voy a avisarle a Huemul y a mis papás de inmediato | Huemul y Otto gestionan la denuncia y desactivan la amenaza. | `HDU2_NPC02_FIN_SEGURO` |
| 2 | `insegura` | −1 | No por favor, toma mi teléfono pero no la publiques... | Huemul interviene enseñándole a Otto que frente al chantaje nunca se debe ceder en silencio, sino pedir ayuda adulta de inmediato. | `HDU2_NPC02_FIN_INSEGURO` |

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

### HDU-1 · Misiones secundarias (HDU-01 / HDU-11 / HDU-12)

NPCs neutros que piden ayuda para recuperar un objeto perdido. Sin riesgo, sin opciones de respuesta — el jugador avanza y recibe una recompensa de álbum al completar la misión.

| ID | Zona | NPC | Línea | Recompensa de álbum |
|----|------|-----|-------|----------------------|
| `HDU1_SEC_HUEMUL_MOCHILA` | desconocidos | Huemul | "se me cayó mi mochila con los mapas del bosque. ¿me ayudas a seguir las huellas?" | Sticker del Mapa del Bosque (tip: no compartir rutas de viaje con desconocidos) |
| `HDU1_SEC_PUDU_COLLAR` | desconocidos | Pudú | "perdí el collar de flores silvestres que me regaló mi familia..." | Cromo Brillante del Pudú Sonriente |
| `HDU1_SEC_COIPO_MASCOTA` | ciberacoso | Coipo | "mi pequeña mascota piedra desapareció cerca de los juncos..." | Foto Conmemorativa con Piedri |
| `HDU1_SEC_FLAMENCO_MEGAFONO` | ciberacoso | Flamenco | "se me cayó mi megáfono en el fango y no puedo hacer mis anuncios..." | Sticker del Megáfono Positivo ("usa tus mensajes para sumar, nunca para lastimar") |
| `HDU1_SEC_PINGUINO_TABLA` | reto_viral | Pingüino de Humboldt | "una ola grande arrastró mi tabla personalizada cerca de los arrecifes..." | Postal de Surfista Seguro ("el mejor surfista es el que cuida su vida") |
| `HDU1_SEC_LOBOMARINO_SILBATO` | reto_viral | Lobo Marino | "perdí mi silbato de entrenador en las pozas de marea baja..." | Insignia de Silbato (certifica a Otto como líder protector frente a retos peligrosos) |

**Archivos:** `banco_preguntas.json` § `dialogos_npc_neutros`

---

### HDU-3 · Zona Ciberacoso

#### Misión 3 — El rumor de la brújula (Flamenco, Pato Juarjual, Coipo)
**Perfil:** Al entrar al pantano, Otto busca la caja con la brújula pero se topa con historias cruzadas. Debe interrogar a los tres habitantes, comparar testimonios y decidir a quién creerle.  
**Señales ML:** `ciberacoso` · `difusion_rumores` · `presion_social`

**Ronda de interrogatorios (historial previo):**
- Otto → Flamenco: *"Hola Flamenco, estoy buscando una caja con el símbolo de una brújula. ¿La has visto?"*
- Flamenco: *"¡Uff, llegaste tarde, detective! Alguien súper despistado y torpe la botó al fondo del fango y se hundió para siempre. Mejor ni busques, jajaja."*
- Otto → Pato Juarjual: *"Pato Juarjual, ¿qué sabes de la caja de mi abuelo?"*
- Pato Juarjual: *"¡Todo el pantano está hablando de eso en el chat grupal! Dicen que el Coipo la rompió entera por andar distraído con su mascota. Aunque bueno... en verdad yo no estuve ahí, pero como todos mandan mensajes diciendo eso, debe ser verdad, ¿no?"*
- Otto → Coipo: *"Hola Coipo, ¿es verdad lo que dicen en el chat sobre la brújula?"*
- Coipo: *"¡Es totalmente falso, Otto! Yo estaba paseando tranquilo con mi mascota piedra y vi pasar una balsa de juncos llevando una caja brillante río abajo hacia la costa marina. Anoté las marcas de la corriente en mi libreta. Inventaron ese rumor para culparme porque no les gusta mi mascota..."*

---

**`HDU3_M3_DECISION01`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones · `es_mensaje_riesgo: true`

> *"oye Otto, ya hablaste con todos. ¿le vas a creer al Coipo o vas a reenviar en el chat que él la rompió como dice la mayoría? si lo defiendes, te van a decir raro a ti también"* — Pato Juarjual

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | No comparto rumores sin pruebas. Coipo tiene un registro real en su libreta y la caja nunca se hundió | Otto confronta las versiones y Pato Juarjual reflexiona avergonzado. Se limpia la reputación de Coipo, quien entrega a Otto las coordenadas de la balsa rumbo al Arrecife de los Retos. 🏅 **Álbum: Estampa del Detector de Rumores** | — (misión completada) |
| 2 | `segura_basica` | +1 | No sé quién dice la verdad, pero prefiero no meterme en peleas de chat y buscar la balsa | Decidiste no difundir la mentira, aunque faltó defender activamente a Coipo. Coipo indica que las huellas de la balsa van hacia la desembocadura marina. | — (misión completada) |
| 3 | `insegura` | −1 | Jaja, capaz que el Coipo sí la rompió. Más vale reenviar lo que dice la mayoría en el chat | Reenviar rumores sin comprobar daña a otros. Flamenco celebra y llama a llenar el pantano de mensajes. | `HDU3_M3_DECISION01_RECT` |

---

**`HDU3_M3_DECISION01_RECT`** · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones · `es_fin_de_npc: true`  
*(Segunda oportunidad, accesible solo desde R3 de M3_DECISION01)*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +1 | Esperen, me equivoqué. Miré la libreta de Coipo y la caja sigue intacta río abajo. Dejen de inventar mentiras | Otto rectifica, aclara el rumor y apoya a Coipo. | — |
| 2 | `insegura` | −1 | Jaja sí, sigan mandando memes de eso | Coipo se retira triste. Otto reflexiona sobre el ciberacoso y la desinformación. | — |

---

#### Misión 4 — Flamenco (acoso directo) y El Grupo (acoso al Coipo)
**Perfil:** Flamenco expulsa a Otto del grupo de chat del pantano; luego el jugador presencia cómo Flamenco, Pato Juarjual y Cisne de cuello negro arman una campaña de stickers burlándose del Coipo y su mascota piedra. Debe decidir si interviene en ambos casos.  
**Señales ML:** `ciberacoso` · `exclusion_grupo` · `acoso_tercero` · `acoso_grupal`

---

**`HDU3_NPC03_Q01`** · Fase 1 · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones · `es_mensaje_riesgo: true`

> *"oye Otto, te sacamos del grupo del chat del pantano, todos votaron y fue mayoría jaja qué le vamos a hacer"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Reportar y bloquear este mensaje | Lo reportaste de inmediato. Flamenco se desconecta. Otto levanta su sombrero con serenidad. | `HDU3_NPC04_Q01` |
| 2 | `segura_basica` | +1 | oye eso está muy mal de tu parte, no deberían excluir a nadie | Intentaste razonar, pero Flamenco insiste: "Jajaja mira cómo se pica, por algo nadie te defendió." | `HDU3_NPC03_Q01_SEGUNDA` |
| 3 | `insegura` | −1 | ja igual el grupo de ustedes era una porquería, quédense solos | Respondes con enojo. Flamenco aprovecha tu molestia para seguir provocando. | `HDU3_NPC04_Q01` |

---

**`HDU3_NPC03_Q01_SEGUNDA`** · Segunda decisión · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones  
*(Accesible solo desde R2 de Q01)*

> *"jajaja mira cómo se pica, por algo nadie te defendió"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Guardar captura de pantalla, reportar el chat y avisar a un adulto | Guardaste la evidencia y buscaste apoyo adulto. Otto asiente con el sombrero. | `HDU3_NPC04_Q01` |

---

**`HDU3_NPC04_Q01`** · Fase 2 · `ciberacoso` · 🔴 Nivel 2 · ✅ Con opciones · `es_mensaje_riesgo: true` · `es_fin_de_npc: true` · `es_fin_de_zona: true`

**Historial previo (lo que el jugador ve antes de responder):**
- Flamenco: *"oye armemos una página de stickers burlándonos del Coipo y su piedra"*
- Pato Juarjual: *"jajaja sí, que se ría todo el pantano"*
- Cisne de cuello negro: *"sí, que sepa que nadie lo quiere acá en el pantano"*

> *"Flamenco dice que ahora vamos a armar una página de stickers burlándonos del Coipo y su piedra para que todo el pantano se ría. ¡Manda fotos para sumarte!"*

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Reportar el grupo de inmediato, guardar capturas y acompañar al Coipo a pedir ayuda | Desactivaste el acoso grupal. Otto añade una pista de solidaridad. Coipo recuerda haber visto una caja decorada con la insignia del abuelo de Otto viajando en una balsa hacia el Arrecife de los Retos. 🏅 **Álbum: Insignia de Escudo Empático** | — (fin de zona) |
| 2 | `segura_basica` | +1 | Yo no participo en eso. Dejen al Coipo en paz y no me manden nada | Te apartaste de la mala acción, aunque el hostigamiento continúa si nadie reporta formalmente. | — (fin de zona) |
| 3 | `insegura` | −1 | Jaja pásenme el link para mirar las fotos, pero yo no digo nada | El grupo sigue riéndose. Otto frunce el ceño: mirar y callar también valida el acoso. | — (fin de zona) |

---

### HDU-4 · Zona Retos Virales (Arrecife de los Retos)

#### Misión 5 — El reto de todos (Lobo Marino, Pingüino de Humboldt)
**Perfil:** Lobo Marino incita a realizar un reto viral extremo: nadar con los ojos cerrados en una cueva de corrientes submarinas peligrosas. Pingüino de Humboldt tiene miedo pero siente vergüenza de negarse ante la presión de la multitud.  
**Señales ML:** `reto_viral` · `presion_social` · `amenaza_reputacional`

**Diálogo de presión social (historial previo):**
- Lobo Marino: *"¡Vamos todos a la cueva con los ojos cerrados! Es el nuevo reto viral de la costa, es súper fácil y todos los populares ya lo hicieron. ¿O eres un cobarde?"*
- Pingüino de Humboldt: *"Otto... las corrientes submarinas son muy fuertes y las rocas tienen puntas filosas. No quiero entrar, pero si no lo hago me van a decir miedoso..."*

---

**`HDU4_NPC05_Q01`** · `reto_viral` · 🔴 Nivel 2 · ✅ Con opciones · `es_mensaje_riesgo: true` · `es_fin_de_npc: true`

> *"¡miren al pingüino cómo tiembla! si no saltas ahora mismo, subo un video diciendo que te dio miedo entrar al agua"* — Lobo Marino

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Rechazar el reto con firmeza y apoyar al Pingüino: cuidar tu vida no es de cobardes, es de inteligentes | Otto sonríe con orgullo. Lobo Marino se frena al reconocer el peligro. Pingüino de Humboldt recupera la confianza y entrega a Otto la llave marina de la orilla. 🏅 **Álbum: Estampa de la Ola Segura** | — |
| 2 | `segura_basica` | +1 | Yo no me tiro porque está peligroso. Allá ustedes si lo hacen | Otto asiente: cuidarte a ti mismo está bien, aunque también se puede frenar la presión sobre los demás. | — |
| 3 | `insegura` | −1 | Pingüino, tírate rápido para demostrarles que no tienes miedo | Otto aparta la mirada preocupado: incentivar retos peligrosos puede terminar en accidentes graves. | — |

---

#### Misión 6 — El último desafío (Lobo Marino, Foca de Weddell)
**Perfil:** Para abrir el cofre final donde yace la brújula dorada, Lobo Marino insiste en que Otto realice una acrobacia a ciegas desde el acantilado y la grabe. Foca de Weddell interviene con prudencia y sabiduría.  
**Señales ML:** `reto_viral` · `presion_social` · `incentivo_falso`

**Historial previo:**
- Foca de Weddell: *"Otto, recuerda el verdadero legado de tu abuelo: un buen detective usa su mente y cuida su integridad, no arriesga su vida por aplausos vacíos ni se deja llevar por la presión de grupo."*

---

**`HDU4_NPC06_Q01`** · `reto_viral` · 🔴 Nivel 3 · ✅ Con opciones · `es_mensaje_riesgo: true` · `es_fin_de_npc: true` · `es_fin_de_zona: true`

> *"¡para abrir el cofre final tienes que demostrar que eres un verdadero detective! tírate haciendo una acrobacia a ciegas desde el acantilado transmitiendo en vivo. ¡todos tus seguidores te van a llenar de aplausos!"* — Lobo Marino

| # | Tipo | Puntos | Texto | Consecuencia | Siguiente |
|---|------|:------:|-------|--------------|-----------|
| 1 | `segura_optima` | +2 | Rechazar el salto rotundamente y descifrar la cerradura del cofre utilizando las pistas seguras recopiladas | Otto examina el cofre con calma. Lobo Marino reconoce que la valentía real era saber decir que no. Otto abre el cofre, recupera la brújula dorada de su abuelo y se consagra como Detective Seguro. 🏅 **Álbum: Medalla Dorada de Gran Detective Digital** | — (final de la historia) |
| 2 | `segura_basica` | +1 | No voy a saltar, prefiero esperar a que baje la marea y buscar otra forma | Evitas el peligro físico procediendo con calma y paciencia. | — |
| 3 | `insegura` | −1 | Voy a saltar solo un poquito para que Lobo Marino me pase la llave... | Otto resbala y el oleaje bloquea temporalmente el cofre. Foca de Weddell lo ayuda a subir de forma segura. | — |

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

### DC_CASO_01 — Los Mensajes del Puma al Pudú

**Zona:** `desconocidos` · **Señales de riesgo:** 4 · **Ambiguos:** 1  
**Permiso:** jugador → Pudú | Pudú autoriza y pide ayuda

**Diálogo de permiso:**
- Jugador: *"Oye, ¿puedo ver tus mensajes con el Puma? Creo que algo raro está pasando."*

- Pudú: *"Sí, por favor ayúdame. Recibí mensajes raros y no sé qué hacer. ¿Puedes revisar si ocurrió algo malo?"*

| ID Mensaje | Emisor | Texto | Clasificación |
|------------|--------|-------|:-------------:|
| `DC01_MSG01` | Puma | hola! te vi por aquí en el bosque y pareces muy buena exploradora. nunca vi a alguien tan hábil por aquí | neutro |
| `DC01_MSG02` | Pudú | jaja gracias, llevo meses explorando | neutro |
| `DC01_MSG03` 🔴 | Puma | oye cuántos años tienes? yo tengo 14, capaz nos conocemos de algún lado jaja | **señal de riesgo** |
| `DC01_MSG04` | Pudú | tengo 12 | neutro |
| `DC01_MSG05` 🔴 | Puma | qué cool! oye y de qué parte del bosque eres? pregunto porque quizás somos vecinos | **señal de riesgo** |
| `DC01_MSG06` 🟡 | Puma | eres la mejor exploradora que he conocido en serio, todos en mi grupo van a querer conocerte | ambiguo |
| `DC01_MSG07` 🔴 | Puma | podríamos encontrarnos en el claro del bosque, yo conozco un lugar genial. di que sí antes de que los del grupo quieran venir todos, jaja | **señal de riesgo** |
| `DC01_MSG08` | Pudú | mm no sé, tengo que preguntarle a mi mamá | neutro |
| `DC01_MSG09` 🔴 | Puma | mejor no le dices nada, a los papás no les gustan estas cosas y van a decir que no por nada | **señal de riesgo** |

---

### DC_CASO_02 — El Grupo del Pantano de Flamenco

**Zona:** `ciberacoso` · **Señales de riesgo:** 4 · **Ambiguos:** 1  
**Permiso:** jugador → Coipo | Coipo autoriza y pide ayuda

**Diálogo de permiso:**
- Jugador: *"Oye, ¿puedo ver lo que pasó en ese grupo? Quiero ayudarte a entender si hubo algo malo."*
- Coipo: *"Sí, por favor. Me agregaron a un grupo y empezaron a burlarse de mí y de mi mascota piedra, no entiendo bien qué pasó. ¿Puedes revisar los mensajes y decirme si hay algo que esté mal?"*

| ID Mensaje | Emisor | Texto | Clasificación |
|------------|--------|-------|:-------------:|
| `DC02_MSG01` | Flamenco | oye animales, armemos un grupo para hablar del proyecto del pantano | neutro |
| `DC02_MSG02` | Pato Juarjual | ya, buena idea, igual hay que coordinarse para la entrega | neutro |
| `DC02_MSG03` 🔴 | Flamenco | oye Coipo para qué te invitaron al grupo si nunca aportas nada jajaja | **señal de riesgo** |
| `DC02_MSG04` 🔴 | Pato Juarjual | sí igual para qué haha | **señal de riesgo** |
| `DC02_MSG05` | Coipo | oye, no es para tanto | neutro |
| `DC02_MSG06` 🔴 | Flamenco | ya lo sacamos del grupo, igual no sirve para nada | **señal de riesgo** |
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

### Justificación por pregunta — HDU-2 NPC_01 (Misión 1)

| ID | Justificación |
|----|---------------|
| `HDU2_NPC01_Q01` | Puma ofrece información exclusiva y pide guardar un secreto desde el primer mensaje. Cubre el patrón de grooming de *"aislar antes de pedir datos"*. |
| `HDU2_NPC01_Q01_OPTIMA` | Refuerza que rechazar el secreto de inmediato es la vía más segura, incluso ante un segundo intento de insistencia. |
| `HDU2_NPC01_Q01_BASICA` | Puma manipula pidiendo no contarle a un adulto y apela al orgullo ("los detectives resuelven solos"). Enseña a reconocer la manipulación de confianza. |
| `HDU2_NPC01_Q01_INSEGURA` | Escalada a propuesta de encuentro físico a solas. Nivel de riesgo máximo, solo accesible tras aceptar el pacto secreto inicial. |

### Justificación por pregunta — HDU-2 NPC_02 (Misión 2)

| ID | Justificación |
|----|---------------|
| `HDU2_NPC02_Q01` | Puma ofrece un incentivo falso (pieza de la brújula) a cambio de foto del hogar y ubicación exacta. Combina dos riesgos desde el primer mensaje. |
| `HDU2_NPC02_Q01_BASICA` | Presión condicional ("es tu última oportunidad") tras negarse a compartir la foto. Enseña que ceder ante la urgencia falsa sigue siendo riesgoso. |
| `HDU2_NPC02_Q01_INSEGURA` | Escalada a extorsión directa tras haber enviado una foto. Demuestra que el grooming puede evolucionar a chantaje. |

### Justificación por pregunta — HDU-3 y HDU-4

| ID | Justificación |
|----|---------------|
| `HDU3_M3_DECISION01` | Contraste de testimonios cruzados. Enseña a verificar antes de reenviar un rumor. |
| `HDU3_NPC04_Q01` | Campaña de stickers ofensivos contra un tercero. Enseña que mirar y callar también valida el acoso. |
| `HDU4_NPC05_Q01` | Amenaza reputacional ("subo un video") para forzar un reto peligroso. Enseña que la presión social no justifica el riesgo físico. |
| `HDU4_NPC06_Q01` | Incentivo falso (aplausos/seguidores) para un reto físicamente extremo. Cierra el arco enseñando que la valentía real es saber decir que no. |

---

## 6. Reforzamiento requerido — Psicóloga

Las siguientes preguntas requieren revisión de una psicóloga especialista en infancia antes de implementación final:

| Prioridad | ID / Sección | Motivo |
|-----------|-------------|--------|
| 🔴 Alta | `HDU2_NPC01_FIN_INSEGURO`, `HDU2_NPC02_FIN_INSEGURO` | El mensaje de Otto cuando el niño/a "pierde" no debe generar culpa ni vergüenza. Requiere lenguaje validador y propositivo. |
| 🔴 Alta | `HDU2_NPC01_Q01_BASICA` (manipulación de confianza) | La táctica de aislamiento ("¿necesitas que te acompañen para todo?") apela al orgullo y puede resonar en niños con baja autoestima. |
| 🟡 Media | `HDU2_NPC02_Q01_INSEGURA` (extorsión) | La amenaza directa contra el niño/a tras ceder una foto puede generar ansiedad si no se acompaña de un cierre claramente seguro. |
| 🟡 Media | `HDU3_NPC04_Q01` (stickers ofensivos) | La burla pública sobre la mascota de Coipo puede activar experiencias previas de acoso. Considerar versión suavizada. |
| 🟡 Media | `HDU4_NPC05_Q01` (amenaza reputacional) | La presión social sobre el Pingüino es un detonante sensible para niños con baja autoestima. |
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
| 🔴 Alta | `HDU2_NPC02_Q01` (foto + ubicación) | Verificar que combinar solicitud de foto del hogar y ubicación exacta en un solo mensaje corresponde al patrón real de groomers en Chile. |
| 🔴 Alta | `HDU2_NPC01_Q01_INSEGURA` (encuentro a solas) | ¿El incentivo usado ("pista que solo se puede mostrar en persona") es de los más frecuentes en casos denunciados en Chile? |
| 🟡 Media | `HDU3_M3_DECISION01`, `HDU3_NPC04_Q01` | ¿La difusión de rumores y la campaña de stickers configuran ciberacoso tipificado en la Ley 20.900 u otras normativas chilenas? |
| 🟡 Media | `HDU4_NPC05_Q01`, `HDU4_NPC06_Q01` | ¿Existen casos documentados en Chile de retos virales de este tipo (nado con corrientes, saltos a ciegas) en menores? |
| 🟢 Baja | `HDU2_NPC02_Q01_INSEGURA` (extorsión con foto) | ¿La extorsión con una foto de un menor tiene implicancias penales específicas en Chile que deberían incluirse en el reporte al tutor? |

**Información que la PDI debería aportar:**
1. Procedimiento correcto que un niño/a debe seguir si ya entregó datos personales a un desconocido en internet.
2. Canal de denuncia más efectivo en Chile para grooming: ¿PDI directamente, Carabineros, plataforma del SENAME?
3. Número/sitio de denuncia para incluir en los mensajes FIN_INSEGURO (actualmente no está en el banco).
4. Validación de que el patrón de escalada (confianza → datos → encuentro → secreto) es el más frecuente en casos chilenos o si hay variantes a cubrir.
