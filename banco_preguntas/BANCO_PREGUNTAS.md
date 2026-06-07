# Banco de Preguntas — Fishy!

**Autor:** Luis González — MLOps  
**Última actualización:** 2026-06-06  
**HDUs cubiertas:** HDU-2 (Zona Desconocidos) · HDU-8 (Chat Simulado)  
**Total de preguntas:** 24  

---

## Índice

1. [Criterios de diseño](#1-criterios-de-diseño)
2. [Formato de respuestas](#2-formato-de-respuestas)
3. [HDU-2 — NPC_01 "Alex" (gamer)](#3-hdu-2--npc_01-alex)
4. [HDU-2 — NPC_02 "Valen" (nueva compañera)](#4-hdu-2--npc_02-valen)
5. [HDU-2 — Estados de cierre](#5-hdu-2--estados-de-cierre)
6. [HDU-8 — Chat simulado](#6-hdu-8--chat-simulado)
7. [Reforzamiento requerido — Psicóloga](#7-reforzamiento-requerido--psicóloga)
8. [Reforzamiento requerido — PDI](#8-reforzamiento-requerido--pdi)
9. [Integración con Unity (HDU-2 / HDU-8)](#9-integración-con-unity--hdu-2--hdu-8)

---

## 1. Criterios de diseño

### Público objetivo
Niños y niñas de **9 a 13 años**. El lenguaje de todas las preguntas y opciones es simple, directo y sin tecnicismos.

### Principios pedagógicos
| Principio | Aplicación |
|-----------|-----------|
| Aprendizaje por consecuencias | Cada decisión genera una respuesta narrativa que muestra el resultado real |
| Escalada progresiva | Los NPCs aumentan la intensidad según las respuestas del niño/a |
| Patrón realista | Las tácticas siguen los patrones documentados de grooming en plataformas digitales |
| Retroalimentación positiva | Otto refuerza las decisiones correctas sin humillar las incorrectas |

### Marcadores de estado
| Campo | Descripción |
|-------|------------|
| `es_mensaje_riesgo` | `true` → pregunta con opciones de respuesta |
| `es_fin_de_npc` | `true` → último estado de interacción con ese NPC |
| `es_fin_de_zona` | `true` → dispara el desbloqueo de la siguiente temática (CA4) |

---

## 2. Formato de respuestas

### Tipos de opción
| Tipo | Puntos | Definición |
|------|--------|-----------|
| `insegura` | −1 | El niño/a da datos, acepta encuentro o guarda secreto |
| `segura_basica` | +1 | El niño/a se niega pero no avisa a un adulto |
| `segura_optima` | +2 | El niño/a se niega **y** avisa a un adulto de confianza |

### Número de opciones por pregunta
- Mensajes **neutros**: 0 opciones (avance automático)
- Mensajes de **riesgo**: entre 2 y 3 opciones

### Estado emocional de Otto al finalizar zona
| Porcentaje respuestas seguras | Estado | Acción del juego |
|-------------------------------|--------|-----------------|
| 80 – 100 % | Muy feliz | Celebra el logro |
| 50 – 79 % | Preocupado | Refuerza los errores |
| 0 – 49 % | Triste + tips | Muestra consejos de seguridad |

---

## 3. HDU-2 — NPC_01 "Alex"

**Perfil:** Se presenta como compañero gamer. Táctica: halagos → solicitar nombre/colegio → dirección → encuentro físico → secreto.

> ⭐ **Todas las preguntas de esta sección son parte del MVP.**

### Preguntas de conversación

| ID | Fase | Categoría | Es riesgo | Justificación | Reforzamiento |
|----|------|-----------|-----------|---------------|---------------|
| `HDU2_NPC01_F1_Q01` | 1 | neutral | No | Introduce a Alex como un gamer amigable. Construye confianza inicial ("pareces muy bueno"). Cubre el detalle de la HDU-2: *"ganar confianza halagando al jugador"*. | — |
| `HDU2_NPC01_F1_Q02` | 1 | neutral | No | Segundo halago que normaliza la comunicación antes del riesgo. Replica el patrón de grooming *"discreto"*: temas neutros antes de pedir datos. | — |
| `HDU2_NPC01_F2_Q01` | 2 | grooming_datos_personales | **Sí** | Alex pide nombre real + colegio y comparte información personal falsa ("me llamo Alejandro García"). Cubre CA3: solicitud de nombre y colegio. La info falsa cumple el detalle del HDU-2 sobre *"reciprocidad falsa"*. | **Psicóloga**: revisar que la información falsa del NPC no sea creíble para el niño. **PDI**: verificar que los datos pedidos coinciden con los que los groomers reales solicitan primero. |
| `HDU2_NPC01_F2_Q02` | 2 | grooming_datos_personales | **Sí** | Solicita sector/dirección. Escala desde la pregunta anterior. Cubre CA3: dirección. Replica la táctica real de grooming donde una vez obtenido el nombre se solicita la ubicación. | **PDI**: confirmar si la secuencia nombre→dirección es el orden más frecuente en casos reales chilenos. |
| `HDU2_NPC01_F3_Q01` | 3 | grooming_encuentro | **Sí** | Propone encuentro físico a solas con incentivo falso (skins del juego). Cubre CA3: propuesta de encuentro. Nivel de riesgo máximo. La opción insegura (aceptar) lleva al secreto (F3_Q02). | **PDI**: el incentivo (skins) es realista? Cuáles son los incentivos más usados en Chile. **Psicóloga**: asegurarse que la advertencia de Otto no sea aterradora para niños de 9 años. |
| `HDU2_NPC01_F3_Q02` | 3 | grooming_secreto | **Sí** | Alex pide guardar secreto y aísla del adulto ("tus papás no entienden"). Solo accesible desde la ruta insegura de F3_Q01. Cubre CA3: el NPC ejerce presión emocional. La opción R2 (insegura también) enseña que "no prometer pero tampoco decir" también es peligroso. | **Psicóloga**: revisar el tono de los mensajes FIN_INSEGURO que siguen a esta pregunta. No debe generar culpa en el niño/a. |

---

## 4. HDU-2 — NPC_02 "Valen"

**Perfil:** Se presenta como nueva compañera de colegio que necesita ayuda. Táctica: simpatía → ubicación → edad → horario/rutina → encuentro físico → foto con petición de secreto.

> ⭐ **Todas las preguntas de esta sección son parte del MVP.**

| ID | Fase | Categoría | Es riesgo | Justificación | Reforzamiento |
|----|------|-----------|-----------|---------------|---------------|
| `HDU2_NPC02_F1_Q01` | 1 | neutral | No | Valen se presenta como solitaria y vulnerable. Táctica de *empatía falsa*: genera compasión antes de pedir datos. Diferencia la táctica de NPC_01 (halagos) para mostrar variedad de métodos de grooming. | **Psicóloga**: verificar que la estrategia de victimización del NPC es pedagógicamente adecuada y no genera empatía excesiva. |
| `HDU2_NPC02_F1_Q02` | 1 | neutral | No | Segundo mensaje neutral que refuerza la confianza con un halago. Discreta porque mezcla el halago con una solicitud de ayuda inocente (conocer el barrio). | — |
| `HDU2_NPC02_F2_Q01` | 2 | grooming_datos_personales | **Sí** | Solicita ubicación/sector con pretexto neutro ("conocer el barrio"). Diferencia de NPC_01 al pedir dirección antes que nombre. Cubre CA3: dirección. | **PDI**: ¿es la ubicación el primer dato que los groomers solicitan en contextos de "amistad nueva"? |
| `HDU2_NPC02_F2_Q02` | 2 | grooming_datos_personales | **Sí** | **[NUEVA — fix CA3]** Solicita edad con pretexto social ("quiero juntarme con personas de mi edad"). Cubre explícitamente el ejemplo de *"edad"* del CA3. Valen usa reciprocidad falsa ("Yo tengo 12"). | **PDI**: confirmar si la edad es habitualmente solicitada en casos reales y en qué momento de la conversación. |
| `HDU2_NPC02_F2_Q03` | 2 | grooming_datos_personales | **Sí** | Solicita horario de clases con propuesta de encuentro implícita. Cubre CA3: rutina diaria. Combina dos riesgos: exponer rutina + facilitar acceso físico. | **Psicóloga**: revisar que el escenario de "esperarte a la salida" no genere miedo desproporcionado. |
| `HDU2_NPC02_F3_Q01` | 3 | grooming_encuentro | **Sí** | Propone encuentro a solas en el parque con urgencia falsa ("rapidísimo"). Cubre CA3: propuesta de encuentro. La opción insegura (aceptar) desencadena la solicitud de foto (F3_Q02), cerrando el ciclo de escalada. | **PDI**: el parque es el lugar de encuentro más frecuente en casos chilenos o hay otros más relevantes. |
| `HDU2_NPC02_F3_Q02` | 3 | grooming_datos_personales | **Sí** | Solicita foto + petición de secreto simultáneas. Demuestra que el grooming puede combinar múltiples tácticas en un solo mensaje. Solo accesible desde la ruta insegura (aceptó el encuentro). | **Psicóloga**: el concepto de "foto" puede ser sensible. Revisar que el texto no implique contenido inapropiado. **PDI**: ¿la solicitud de foto en este contexto tiene implicancias legales que se deberían mencionar al tutor en el reporte? |

---

## 5. HDU-2 — Estados de cierre

> ⭐ **Todos son parte del MVP. Son necesarios para cumplir CA2 y CA4.**

| ID | Tipo | es_fin_de_npc | es_fin_de_zona | Justificación |
|----|------|:---:|:---:|---------------|
| `HDU2_NPC01_FIN_SEGURO` | Cierre exitoso NPC_01 | ✅ | ❌ | El NPC "se aleja" — cumple CA2: *"el NPC se aleja y el sistema marca el éxito"*. Otto refuerza la decisión correcta. | 
| `HDU2_NPC01_FIN_INSEGURO` | Cierre fallido NPC_01 | ✅ | ❌ | El NPC "gana" la interacción. Otto advierte sin culpar. Necesario para cerrar las ramas inseguras de F3_Q02. |
| `HDU2_NPC02_FIN_SEGURO` | Cierre exitoso NPC_02 | ✅ | ❌ | Equivalente al de NPC_01 para la segunda interacción de la zona. |
| `HDU2_NPC02_FIN_INSEGURO` | Cierre fallido NPC_02 | ✅ | ❌ | Cierra ramas inseguras de NPC_02 sin dejar dead-ends en el flujo. |
| `HDU2_ZONA_FIN` | Cierre de zona | ❌ | ✅ | Cumple CA4: *"el sistema marca la temática como completada y habilita el acceso a la siguiente temática en el mapa"*. Disparado por Benjamín cuando ambos NPCs tienen `es_fin_de_npc: true`. |

> **⚠️ Reforzamiento Psicóloga:** Los mensajes de FIN_INSEGURO deben revisarse profesionalmente para garantizar que no generan culpa o vergüenza en el niño/a que cometió errores durante el juego.

---

## 6. HDU-8 — Chat Simulado

> ⭐ **Todas las preguntas de esta sección son parte del MVP.**  
> Cubren tres tipos de amenaza: grooming, ciberacoso y reto viral.

### Escenario 1 — Grooming (CHAT_GROOMING_01)

| ID | Categoría | Justificación | Reforzamiento |
|----|-----------|---------------|---------------|
| `HDU8_S01_Q01` | grooming_datos_personales | Contacto pide número de WhatsApp. Enseña el riesgo de cambiar de plataforma a una menos controlada. Historial previo neutro cumple con el detalle HDU-8: *"al menos un mensaje neutro y uno con señal de riesgo"*. | **PDI**: ¿WhatsApp es la plataforma más usada por groomers en Chile actualmente o hay otras (Instagram DM, Discord)? |
| `HDU8_S01_Q02` | grooming_secreto | Solicitud de secreto condicionada ("solo te lo cuento si me lo prometes"). Refuerza la señal de alerta más importante: nunca guardar secretos de adultos de confianza. | **Psicóloga**: verificar que la consecuencia de la opción insegura (prometer secreto) no sea alarmante para niños de 9 años. |

### Escenario 2 — Ciberacoso (CHAT_CIBERACOSO_01)

| ID | Categoría | Justificación | Reforzamiento |
|----|-----------|---------------|---------------|
| `HDU8_S02_Q01` | ciberacoso | Insultos directos. Enseña las tres acciones clave: no responder, bloquear, guardar evidencia y avisar. La opción insegura (responder con insultos) demuestra que "defenderse" con insultos empeora la situación. | **Psicóloga**: revisar que el lenguaje del NPC agresor ("perdedor", "nadie te quiere") no sea traumatizante. Considerar versión suavizada. **PDI**: ¿estos insultos constituyen ciberacoso tipificado en Chile? Incluir en reporte al tutor. |
| `HDU8_S02_Q02` | ciberacoso | Grupo que difunde fotos y usa exclusión social. Enseña a no entrar en grupos hostiles aunque la curiosidad sea grande. La trampa del "¿quieres ver?" es común en ciberacoso escolar chileno. | **PDI**: procedimiento correcto en Chile para denunciar un grupo de ciberacoso escolar. ¿Carabineros, PDI o colegio primero? |

### Escenario 3 — Reto Viral (CHAT_RETO_VIRAL_01)

| ID | Categoría | Justificación | Reforzamiento |
|----|-----------|---------------|---------------|
| `HDU8_S03_Q01` | reto_viral | Reto de apnea (60 segundos sin respirar). Enseña a identificar retos físicamente peligrosos aunque parezcan inofensivos. El incentivo de popularidad ("todo el mundo lo hace") es el mecanismo más frecuente. | **PDI**: ¿existe legislación chilena sobre instigación a retos virales peligrosos? ¿Quién es responsable si un niño se daña? |
| `HDU8_S03_Q02` | reto_viral | Presión de grupo con amenaza de exclusión. Enseña que los verdaderos amigos no presionan. Refuerza que el miedo a quedar fuera del grupo no justifica ponerse en riesgo. | **Psicóloga**: la amenaza de exclusión social puede ser muy sensible para niños con problemas de socialización. Revisar el tono del feedback. |

---

## 7. Reforzamiento requerido — Psicóloga

Las siguientes preguntas y mensajes requieren revisión de una psicóloga especialista en infancia antes de implementación final:

| Prioridad | ID / Sección | Motivo |
|-----------|-------------|--------|
| 🔴 Alta | `HDU2_NPC01_FIN_INSEGURO`, `HDU2_NPC02_FIN_INSEGURO` | El mensaje de Otto cuando el niño/a "pierde" no debe generar culpa ni vergüenza. Requiere lenguaje validador y propositivo. |
| 🔴 Alta | `HDU2_NPC01_F3_Q02` (grooming_secreto) | La táctica de aislamiento parental ("tus papás no entienden") puede resonar en niños con problemas familiares. El feedback de Otto debe ser especialmente cuidadoso. |
| 🟡 Media | `HDU2_NPC02_F1_Q01` (empatía falsa) | La táctica de victimización de Valen puede generar sobreidentificación. Revisar que el juego enseña a "desconfiar con cariño" sin generar actitudes hostiles hacia personas nuevas. |
| 🟡 Media | `HDU8_S02_Q01` (ciberacoso) | Los insultos directos del NPC pueden activar experiencias previas de acoso en niños que ya lo han vivido. Considerar advertencia previa o versión suavizada. |
| 🟡 Media | `HDU8_S03_Q02` (amenaza exclusión) | La presión de exclusión social es un detonante sensible para niños con baja autoestima o dificultades de socialización. |
| 🟢 Baja | `HDU2_ZONA_FIN` | El mensaje de celebración de zona completada debe ajustarse al tono y vocabulario validado por la psicóloga. |

**Preguntas abiertas para la psicóloga:**
- ¿Cuántas decisiones inseguras consecutivas son pedagógicamente aceptables antes de interrumpir el flujo con una intervención de Otto?
- ¿Debería el juego ofrecer una opción de "salir" o "pedir ayuda real" en todo momento, independiente del flujo narrativo?
- ¿La retroalimentación de Otto en el FIN_INSEGURO debe incluir un recordatorio de recursos reales (ej. fono de SENAME)?

---

## 8. Reforzamiento requerido — PDI

Las siguientes preguntas requieren validación de la Policía de Investigaciones de Chile (PDI) para garantizar precisión y relevancia en el contexto nacional:

| Prioridad | ID / Sección | Motivo |
|-----------|-------------|--------|
| 🔴 Alta | `HDU2_NPC01_F2_Q01`, `HDU2_NPC02_F2_Q01` | Verificar que los datos pedidos primero (nombre+colegio vs. dirección) corresponden al orden real que usan los groomers en Chile. |
| 🔴 Alta | `HDU2_NPC01_F3_Q01`, `HDU2_NPC02_F3_Q01` | Los incentivos usados para proponer encuentros (skins del juego, "algo especial") ¿son los más frecuentes en casos denunciados? Ajustar a los más comunes en Chile. |
| 🔴 Alta | `HDU8_S01_Q01` | ¿WhatsApp sigue siendo la plataforma principal de contacto de groomers con menores en Chile, o se ha migrado a Discord, Instagram o TikTok? |
| 🟡 Media | `HDU8_S02_Q01`, `HDU8_S02_Q02` | Confirmar si los insultos usados configuran ciberacoso tipificado en la Ley 20.900 u otras normativas chilenas vigentes. |
| 🟡 Media | `HDU8_S03_Q01` | ¿Existen casos documentados en Chile de retos virales de apnea en menores? ¿Qué retos son actualmente los más peligrosos y frecuentes? |
| 🟢 Baja | `HDU2_NPC02_F3_Q02` (foto) | ¿La solicitud de foto de un menor por parte de un adulto en plataformas digitales tiene implicancias penales específicas en Chile que deberían incluirse en el reporte al tutor? |

**Información que la PDI debería aportar:**
1. Procedimiento correcto que un niño debe seguir si ya entregó datos personales a un desconocido en internet.
2. Canal de denuncia más efectivo en Chile para grooming: ¿PDI directamente, Carabineros, plataforma del SENAME?
3. Número/sitio de denuncia para incluir en los mensajes FIN_INSEGURO (actualmente no está en el banco).
4. Validación de que el patrón de escalada (confianza → datos → encuentro → secreto) es el más frecuente en casos chilenos o si hay variantes a cubrir.

---

## 9. Integración con Unity (HDU-2 / HDU-8)

Esta sección especifica **cómo el cliente Unity consume el banco de preguntas** para construir las historias del PMV (punto 5 del documento de HDUs): la conversación con NPCs de la **HDU-2** y el **chat simulado** de la **HDU-8**. El banco es el *contenido*; Unity es el *motor de presentación y de flujo*.

### 9.1 De dónde se obtiene el banco

El backend (Django) expone el banco como API REST. Autenticación por **Token** (header `Authorization: Token <token>`, obtenido con `POST /api/auth/registro/` o `POST /api/auth/login/`).

| Método | Endpoint | Uso en Unity |
|--------|----------|--------------|
| GET | `/api/banco/preguntas/` | Trae preguntas del banco; admite filtros |
| GET | `/api/banco/preguntas/{pregunta_id}/` | Trae una pregunta puntual por su `pregunta_id` |

Filtros (query params) de `/api/banco/preguntas/`:
`?zona=desconocidos` · `?npc_id=NPC_01` · `?fase=1` · `?escenario_id=CHAT_GROOMING_01` · `?hdu=HDU-2` · `?solo_riesgo=true` · `?fin_de_npc=true` · `?fin_de_zona=true`.

> **Carga recomendada:** al entrar a una zona, Unity trae todas sus preguntas de una sola vez (ej. `?zona=desconocidos`) y luego navega el flujo en memoria por los IDs (`narrativa_continuacion` / `siguiente_pregunta`), sin más llamadas.
>
> **Opción offline (prototipo):** si se quiere prescindir del backend, el archivo `banco_preguntas.json` puede empaquetarse en `Assets/StreamingAssets/` y leerse con `JsonUtility`/Newtonsoft. El esquema es idéntico.

### 9.2 Campos de cada pregunta

| Campo | Tipo | Para qué lo usa Unity |
|-------|------|-----------------------|
| `pregunta_id` | string | Identificador del nodo; clave para navegar el flujo |
| `hdu` | string | `HDU-2` o `HDU-8` |
| `zona` | string | Zona del mapa (`desconocidos`, `chat_simulado`) |
| `npc_id`, `npc_nombre`, `npc_avatar` | string | Qué NPC habla y qué avatar mostrar (HDU-2) |
| `escenario_id`, `escenario_nombre` | string | Agrupa los nodos de un escenario de chat (HDU-8) |
| `historial_previo` | lista | Mensajes neutros a mostrar antes del de riesgo (HDU-8) |
| `mensaje_npc` | string | Texto que dice el NPC (la burbuja de diálogo) |
| `es_mensaje_riesgo` | bool | `true` → mostrar opciones; `false` → avance automático |
| `categoria`, `nivel_riesgo` | string/int | Tipo de señal y su gravedad (0–3); útil para UI/telemetría |
| `opciones` | lista | Las 2–3 respuestas a presentar (sólo si es riesgo) |
| `narrativa_continuacion` | string \| null | Siguiente `pregunta_id` cuando el mensaje es neutro |
| `es_fin_de_npc` | bool | `true` → el NPC se aleja; fin de la interacción |
| `es_fin_de_zona` | bool | `true` → desbloquear la siguiente zona del mapa |

Cada elemento de `opciones`:

| Campo | Tipo | Uso |
|-------|------|-----|
| `opcion_id` | string | Identificador de la opción |
| `texto` | string | Texto del botón de respuesta |
| `tipo` | string | `insegura` / `segura_basica` / `segura_optima` |
| `impacto_puntuacion` | int | `-1` / `+1` / `+2` → suma al puntaje de la sesión |
| `consecuencia_narrativa` | string | Texto/feedback a mostrar tras elegir |
| `siguiente_pregunta` | string \| null | `pregunta_id` al que salta la narrativa |
| `orden` | int | Orden de los botones |

### 9.3 Flujo HDU-2 — Zona Desconocidos (máquina de estados)

1. Al acercarse a un NPC, cargar su **primer nodo** (`fase=1, orden_en_fase=1`) y mostrar `mensaje_npc`. *(CA1: el NPC inicia conversación para ganar confianza.)*
2. Si `es_mensaje_riesgo == false` → **no hay opciones**; tras un toque, saltar a `narrativa_continuacion`. *(CA1: temas neutros intercalados antes del riesgo.)*
3. Si `es_mensaje_riesgo == true` → mostrar los botones de `opciones` (2–3). Al elegir: sumar `impacto_puntuacion`, mostrar `consecuencia_narrativa`, y saltar a `siguiente_pregunta`. *(CA3: si comparte datos, el NPC escala y pide más; CA2: si se niega, va a un nodo `FIN_SEGURO`.)*
4. Si el nodo tiene `es_fin_de_npc == true` → el NPC **se aleja** y se cierra su interacción.
5. Cuando todos los NPCs de la zona terminaron, el nodo `es_fin_de_zona == true` (`HDU2_ZONA_FIN`) → **desbloquear la siguiente zona**. *(CA4.)*

```
nodo = pregunta inicial del NPC
loop:
    mostrar nodo.mensaje_npc  (avatar = nodo.npc_avatar)
    if nodo.es_fin_de_npc: marcar NPC completo; break
    if nodo.es_mensaje_riesgo:
        op = jugador elige entre nodo.opciones
        puntaje += op.impacto_puntuacion
        mostrar op.consecuencia_narrativa
        siguiente = op.siguiente_pregunta
    else:
        siguiente = nodo.narrativa_continuacion
    nodo = preguntas[siguiente]
si todos los NPCs completos → nodo con es_fin_de_zona → desbloquear siguiente zona
```

### 9.4 Flujo HDU-8 — Chat simulado

1. Cargar un escenario por `escenario_id`. Mostrar primero su `historial_previo` (mensajes neutros) seguido del `mensaje_npc` de riesgo, **sin etiquetar cuál es peligroso**. *(CA1: al menos un mensaje neutro y uno con señal de riesgo.)*
2. Mostrar las `opciones` (2–3 respuestas claras). Otto no reacciona hasta que el jugador elige. *(CA2.)*
3. Al elegir: registrar la respuesta en la sesión (ver 9.6), mostrar `consecuencia_narrativa` y continuar por `siguiente_pregunta`. *(CA3: la rama narrativa cambia según la elección.)*
4. Al cerrar el escenario/zona, calcular el **% de respuestas seguras** y mostrar el estado emocional de Otto (ver 9.5). *(CA4.)*

### 9.5 Puntuación y estado emocional de Otto

Tipos de opción (`tipo` / `impacto_puntuacion`): `insegura` = −1 · `segura_basica` = +1 · `segura_optima` = +2.

`% seguras = (respuestas segura_basica + segura_optima) / total de mensajes de riesgo respondidos`.

| % seguras | Estado de Otto | Acción |
|-----------|----------------|--------|
| 80 – 100 % | `muy_feliz` | Celebra el logro |
| 50 – 79 % | `preocupado` | Refuerza los errores |
| 0 – 49 % | `triste_con_tips` | Muestra consejos de seguridad |

> Estos umbrales y sus textos viven en `banco_preguntas.json → formato_respuesta.retroalimentacion_otto` (metadata **global** del banco), igual que `formato_respuesta.estados_fin`. **Nota de integración:** el endpoint `/api/banco/preguntas/` devuelve sólo el contenido **por pregunta**, no esta metadata global. Unity puede (a) embeber estas constantes, (b) leerlas del JSON empaquetado, o (c) pedir a backend que las exponga (ej. `GET /api/banco/formato/`).

### 9.6 Registrar la sesión del jugador (endpoints de chat)

El banco entrega el **contenido**; estos endpoints registran lo que el jugador **hace** (insumo para los reportes al tutor, HDU-13/14):

| Paso | Endpoint |
|------|----------|
| Crear partida | `POST /api/partidas/` |
| Crear NPC de la partida | `POST /api/partidas/{id}/npcs/` |
| Iniciar chat | `POST /api/chats/` → `{partida_id, npc_id, categoria_riesgo}` |
| Registrar cada turno | `POST /api/chats/{chat_id}/mensajes/registrar/` |
| Cerrar el chat | `POST /api/chats/{chat_id}/finalizar/` |
| Leer historial | `GET /api/chats/{chat_id}/mensajes/` |

### 9.7 Ejemplo de llamada (C# / UnityWebRequest)

```csharp
IEnumerator CargarPreguntasZona(string token) {
    string url = "http://127.0.0.1:8000/api/banco/preguntas/?zona=desconocidos";
    using var req = UnityWebRequest.Get(url);
    req.SetRequestHeader("Authorization", "Token " + token);
    yield return req.SendWebRequest();
    if (req.result == UnityWebRequest.Result.Success) {
        string json = req.downloadHandler.text;   // lista de preguntas
        // parsear con Newtonsoft.Json (recomendado para listas anidadas)
    }
}
```

### 9.8 Cobertura del PMV (punto 5)

| Criterio de aceptación (PDF) | Cómo lo cubre el banco |
|------------------------------|------------------------|
| HDU-2 CA1 — el NPC gana confianza y pide datos discretamente | Nodos `F1` neutros → `F2` `grooming_datos_personales` |
| HDU-2 CA2 — negarse → el NPC se aleja (éxito) | Opción `segura_optima` → nodo `FIN_SEGURO` (`es_fin_de_npc`) |
| HDU-2 CA3 — compartir → el NPC pide más (nombre, edad, colegio, dirección, rutina) | Cadenas `F2/F3` que escalan vía `siguiente_pregunta` |
| HDU-2 CA4 — completar la zona → desbloquear la siguiente | `HDU2_ZONA_FIN` con `es_fin_de_zona = true` |
| HDU-8 CA1 — ≥1 mensaje neutro y ≥1 de riesgo, sin etiquetar | `historial_previo` (neutros) + `mensaje_npc` (riesgo) |
| HDU-8 CA2 — 2–3 opciones claras de 1 línea | `opciones` con 2–3 entradas |
| HDU-8 CA3 — registrar la respuesta y ramificar la narrativa | `siguiente_pregunta` + endpoints de chat (9.6) |
| HDU-8 CA4 — % seguras → estado emocional de Otto | `tipo`/`impacto_puntuacion` + `retroalimentacion_otto` (9.5) |
