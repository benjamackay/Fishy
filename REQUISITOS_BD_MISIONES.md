# Requisitos de datos — Sistema de misiones

Qué tiene que poder guardar la base para que el sistema de misiones del juego
funcione completo.

**Esto dice QUÉ hay que guardar, no CÓMO.** Las tablas, los tipos, las relaciones
y la forma de exponerlo son decisiones de quien implemente. Los nombres que
aparecen aquí son los que ya existen hoy, sólo para poder señalar de qué se está
hablando.

---

## Dos cosas distintas que no se pueden mezclar

1. **Catálogo** — el contenido del juego: qué misiones hay y qué pide cada una.
   Es igual para todos los niños y sólo cambia cuando el equipo escribe contenido.
2. **Progreso** — lo que un niño concreto lleva hecho en una partida concreta.

La separación ya existe y hay que mantenerla, por una razón concreta: el catálogo
se **borra y se recrea entero** cada vez que se corre `cargar_banco`
(`Mision.objects.all().delete()`). Si el progreso colgara del catálogo, recargar
el banco borraría lo que los niños llevan hecho.

---

## A. Catálogo: datos de cada misión

| Dato | ¿Obligatorio? | Para qué se usa | Estado hoy |
|---|---|---|---|
| **Identificador de misión** | Sí | Es la llave con la que el juego y la base hablan de la misma misión | Ya existe |
| **Título** | Sí | Lo que lee el niño/a en el cartel de misión activa | Ya existe |
| **Zona objetivo** | No | Hacia dónde apunta el indicador que guía al niño/a. Identificador **espacial** (`zona_1`…). Vacío = esta misión no señala ninguna zona | **Falta** |
| **Orden en la historia** | Sí | Decide cuál es la misión activa y cuál entra cuando se completa la anterior | **Falta** |
| **Tipo** | Sí | principal / secundaria / exploración | Ya existe |
| **Zona a la que pertenece** | Sí | Agrupar y filtrar por temática de contenido | Ya existe |

Sobre el **identificador**: tiene que ser único en todo el juego y estable en el
tiempo. Es lo que se guarda en el progreso, así que renombrarlo desconecta a los
niños de lo que ya llevaban hecho.

Sobre la **descripción**: queda **fuera de la base a propósito**. El campo sigue
existiendo del lado del juego —es la pista que se muestra cuando una misión no
tiene objetivos que listar— pero no se guarda aquí.

### Las dos zonas no son lo mismo, y manda la espacial

Conviven dos vocabularios y hay que no confundirlos:

- **Zona espacial** (`zona_1`, `zona_2`, `zona_3`) — un sitio del mapa. Es la que
  vale para **señalar un destino** y para **detectar que el niño/a llegó**.
- **Zona temática** (`desconocidos`, `ciberacoso`, `reto_viral`) — de qué trata el
  contenido. Sirve para agrupar y filtrar, no para ubicar a nadie.

Cuando un dato responde a "¿dónde?", se guarda la espacial. Cuando responde a "¿de
qué trata?", la temática. El campo de zona que ya existe en el catálogo es del
segundo tipo.

Sobre el **orden**: hace falta un criterio de secuencia explícito. Sin él no se
puede contestar "¿cuál es la siguiente misión?", que es lo que el juego necesita
al completar una. Un valor numérico donde menor va antes sirve; conviene poder
dejar huecos para intercalar misiones nuevas sin renumerar las demás.

---

## B. Catálogo: objetivos de cada misión

Una misión tiene **cero o más objetivos** y se da por completada cuando todos
están cumplidos. Cero objetivos significa que la misión es informativa y la
completa otro sistema, no sus objetivos.

De cada objetivo hay que poder guardar:

- **A qué misión pertenece.** Un objetivo es de una sola misión.
- **Su posición dentro de la misión.** El orden importa: el cartel del juego
  muestra sólo los primeros y resume el resto.
- **Su categoría**, que es una de las cuatro de abajo. Un objetivo tiene
  exactamente una.
- **Los datos propios de esa categoría.**

Como cada categoría necesita datos distintos, los datos de las otras tres tienen
que poder quedar vacíos sin que eso sea un error.

### Las 4 categorías

**1. Recoger un objeto**

- Qué objeto hay que juntar → un **identificador de objeto**, no un nombre. Los
  objetos del juego ya tienen uno propio (`itemId`) y el juego puede resolverlo
  sin depender de en qué parte del mapa esté.
- Cuántas unidades hacen falta → un entero, mínimo 1.

**2. Hablar con un NPC** — cara a cara, en el mapa

- Con qué NPC hay que conversar → el **id de diálogo** del bloque
  `dialogos_npc_neutros` del banco (p. ej. `HDU1_SEC_COIPO_MASCOTA`). Es el único
  identificador que los NPCs del mapa llevan encima hoy, y además es el que la base
  **ya** usa para relacionar diálogo → misión.

**3. Chatear por el celular**

- Qué conversación hay que atender → uno o varios **`escenario_id`** del bloque
  `preguntas` del banco (`M1_CHAT01`, `M4_FASE01`…). Varios cuando la historia va
  por fases, y en ese caso **el orden importa**.

> **El id de NPC de chat no hace falta, y es mejor no guardarlo.** Se evaluó como
> segundo criterio —"cualquier conversación de este personaje"— y se descartó: dentro
> del propio bloque `preguntas` un mismo `npc_id` se repite entre conversaciones
> distintas (`NPC_01` y `NPC_02` son los dos "Puma"), así que elegía la conversación
> equivocada. El `escenario_id` identifica la conversación en sí y es el único criterio
> que usa el juego.

> **Por qué 2 y 3 no son la misma categoría, aunque las dos sean "conversar".**
> No comparten identificador ni vocabulario: salen de **dos bloques distintos del
> banco**, con espacios de nombres distintos para el mismo animal —Flamenco es
> `NPC_FLAMENCO_SEC` en `dialogos_npc_neutros` y `NPC_03` en `preguntas`—. En el
> bloque de chats no existen los `dialogo_id` en absoluto. Y para rematar, tres ids
> (`NPC_GUIA`, `NPC_GUIA_2`, `NPC_GUIA_3`) **aparecen en los dos bloques** señalando
> conversaciones diferentes, así que guardar "un id de NPC" sin decir de qué bloque
> es sería ambiguo.
>
> Además el juego las cumple con hechos distintos: la primera, al cerrar el diálogo
> de un NPC del mapa; la segunda, al cerrarse el chat del celular.

**4. Llegar a una zona**

- A qué zona hay que llegar → el **identificador espacial** de la zona: `zona_1`,
  `zona_2`, `zona_3`.
- **Manda el vocabulario espacial, no el temático.** Existe también el del banco
  (`desconocidos`, `ciberacoso`, `reto_viral`), que nombra la temática de contenido
  y no un sitio del mapa. Para "llegar a un sitio" el que vale es el espacial;
  el temático se queda como etiqueta de contenido, que es para lo que sirve.

### Lo que NO hace falta guardar

El texto que ve el niño/a ("Juntar Concha (1/3)", "Ir a El Bosque") lo compone el
juego a partir de la categoría y de sus datos. Guardarlo además sería una segunda
copia que se puede contradecir con la primera.

---

## C. Progreso de misión por partida

Esto **ya existe y funciona**. Los requisitos que cumple hoy y hay que conservar:

- Qué partida y qué misión.
- Cuándo quedó disponible.
- Cuándo se completó — vacío significa que sigue disponible. Que el estado se
  derive de ahí evita que puedan contradecirse.
- Una sola anotación por (partida, misión).
- **Completar es un camino de ida**: una misión completada no puede volver a
  disponible. Los avisos del juego no llegan en orden garantizado, y el registro
  que ve el adulto responsable no puede retroceder.
- Una misión que no esté en el catálogo se guarda igual, avisando. El progreso del
  niño/a no puede depender de que el contenido esté al día.

---

## D. Progreso de objetivo por partida — esto es lo que falta

Hoy el avance **dentro** de una misión se pierde al cerrar el juego, y no en
todos los casos igual. Medido sobre el comportamiento actual:

| Categoría | ¿Sobrevive a cerrar el juego? | Por qué |
|---|---|---|
| Recoger objeto | **Sí** | Se recalcula del inventario, que sí se guarda |
| Hablar con NPC | **No** | Se cumple por un aviso puntual y no queda registro de haber hablado |
| Chatear por celular | **No** | Igual que el anterior |
| Llegar a zona | **No** | Se recalcula de dónde está el personaje *ahora*: si ya salió de la zona, se pierde |

En la práctica: un niño/a que cumple 2 de 3 objetivos y cierra el juego puede
volver con 0 de 3, y no entender por qué.

Para poder arreglarlo, hace falta poder guardar, **por partida**:

- Qué objetivo concreto, de qué misión.
- Si está cumplido y desde cuándo.
- Para los que se cuentan (recoger objetos), cuánto lleva acumulado — **o** dejar
  escrito que ese caso se recalcula del inventario y a propósito no se guarda.
  Las dos respuestas valen; lo que no vale es que quede sin decidir.

Y el mismo requisito que el progreso de misión: **recargar el banco no puede
llevárselo por delante**.

---

## E. Cómo tiene que poder llegar al juego

Requisitos de acceso, sin decir de qué forma se resuelven:

1. El juego tiene que poder pedir el **catálogo completo** de misiones con sus
   objetivos **sin haber jugado antes y sin partida abierta**. Hoy esto no se
   puede: lo único que se puede pedir son las misiones de una partida, es decir,
   las que el juego ya había subido. Sin esto el juego no puede descubrir una
   misión que no tenga ya configurada por dentro.
2. Por separado, el progreso de una partida (misiones y objetivos).

---

## F. Tres cosas que hay que decidir antes de empezar

1. **Los identificadores no calzan.** El juego usa hoy `MISION_NPC_01` y
   `MISION_NPC_02`; la base tiene los 9 del banco (`MISION_EXPLORACION_01`,
   `MISION_SEC_MOCHILA_HUEMUL`, …). No se solapa ninguno. Van a mandar los de la
   base, pero conviene decirlo explícitamente porque implica rehacer las fichas
   del lado del juego.

2. **El banco no trae objetivos.** Los 9 diálogos traen `mision_desbloquea` y
   `nombre_mision`, y el objetivo está contado en prosa dentro de las líneas del
   NPC — la misión de Coipo es literalmente *"mi pequeña mascota piedra
   desapareció cerca de los juncos"*. Los objetivos de las 9 misiones **hay que
   escribirlos como contenido nuevo**; no se pueden cargar de lo que ya existe.

3. **El ícono.** La ficha del juego admite uno, pero es un recurso del proyecto,
   no una imagen que tenga sentido guardar en la base. Si se quiere desde la base,
   lo que se guarda es el nombre del recurso. También es razonable dejarlo fuera.

---

## Anexo — las 9 misiones que ya están en el catálogo

| zona | mision_id | nombre |
|---|---|---|
| desconocidos | `MISION_EXPLORACION_01` | *(sin nombre en el banco)* |
| desconocidos | `MISION_SEC_MOCHILA_HUEMUL` | La mochila de Huemul |
| desconocidos | `MISION_SEC_COLLAR_PUDU` | El collar de flores de Pudú |
| ciberacoso | `MISION_EXPLORACION_02` | El rumor del pantano |
| ciberacoso | `MISION_SEC_MASCOTA_COIPO` | ¿Dónde está la mascota? |
| ciberacoso | `MISION_SEC_MEGAFONO_FLAMENCO` | El megáfono de Flamenco |
| reto_viral | `MISION_EXPLORACION_03` | Las llaves del cofre |
| reto_viral | `MISION_SEC_TABLA_PINGUINO` | La tabla de surf del Pingüino |
| reto_viral | `MISION_SEC_SILBATO_LOBOMARINO` | El silbato marino del Lobo Marino |

Verificado sobre `Backend/backend/local_db.sqlite3`. Falta confirmar que Supabase
tiene lo mismo: la `DB_PASSWORD` del `.env` está vacía, así que desde aquí no se
pudo comprobar.
