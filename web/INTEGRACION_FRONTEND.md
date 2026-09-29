# Integración del panel de Fishy

**Estado al 13 de septiembre de 2026:** el frontend consume grupos, invitaciones,
reportes reales. El backend los reincorporó con la migración
`0014_grupos_invitaciones`, sobre el esquema de `dev`. Invitar y reenviar
responden 503 mientras no se configure el proveedor de correo; lo demás funciona.
La demostración continúa separada: un fallo del servidor jamás activa datos
ficticios.

El contrato de dominio está en `src/types/panel.ts` y el adaptador en
`src/api/panelReal.ts`. La implementación y la configuración del servidor están
en [Backend/INVITACIONES.md](../Backend/INVITACIONES.md).

## Agregar niños por correo (HDU17)

**Frontend conectado al contrato de `dev` (27 de septiembre de 2026).**
No se modificaron el backend ni las migraciones para esta integración.

Es el flujo que pide el criterio de aceptación de HDU17: el profesor presiona
"Agregar", escribe el correo del apoderado, ve los perfiles de niño de esa cuenta,
marca uno o varios y presiona "Agregar". Quedan en el curso **de inmediato**, sin
correo ni aceptación de la familia. Reemplaza a "Invitar familia" en el portal:
las invitaciones siguen en el backend (sección siguiente), pero **se ocultan**.
El aviso a la familia y que ella pueda sacar al niño quedan para el Sprint 3.

**Un niño está en un solo curso a la vez.** Lo garantiza la base de datos.

### Endpoints (profesor dueño del grupo)

| Método y ruta | Body | Respuesta |
|---|---|---|
| `POST /api/grupos/<id>/buscar-familia/` | `{"email": "..."}` | 200 `{"perfiles": [{"jugador_id", "nombre", "estado"}]}` |
| `POST /api/grupos/<id>/miembros/` | `{"email": "...", "jugador_ids": [1, 2]}` | 201 con el detalle del grupo (igual que `GET /api/grupos/<id>/`); 200 si todos ya estaban |

Es POST también para buscar, para que el correo no quede en URLs ni logs.

`estado` de cada perfil:

- `disponible`: se puede marcar.
- `en_este_curso`: ya está; mostrarlo marcado o deshabilitado.
- `en_otro_curso`: **deshabilitado**, con el texto "Ya está en otro curso". No se
  dice cuál ni de qué profesor, a propósito.

Solo se devuelve el nombre del perfil: nada de edad ni avance antes de agregarlo.

### Errores (todos traen `detail` listo para mostrar)

| Código | Cuándo |
|---|---|
| 400 | Correo inválido, `jugador_ids` vacío o con más de 20 |
| 403 | La cuenta no es profesor (padre y admin incluidos) |
| 404 `No encontramos perfiles de niño para ese correo.` | El correo no tiene cuenta, es de un profesor, no tiene niños, o algún `jugador_id` no es de ese correo. **Es el mismo mensaje a propósito**, para no revelar quién está registrado |
| 404 (otro texto) | El grupo no existe o es de otro profesor |
| 409 `Martina ya está en otro curso. ...` | Algún elegido está en otro curso. **No se agrega ninguno** (todo o nada) |
| 429 | Más de 120 búsquedas o agregados por hora del mismo profesor |

Repetir un agregado no duplica: los que ya estaban se saltan sin error.

### Comportamiento implementado en web

- `AgregarNinos` separa la búsqueda y la selección. Cambiar de correo descarta
  perfiles y selección anteriores; el agregado usa el correo de la búsqueda.
- Los perfiles `en_este_curso` aparecen marcados y deshabilitados; los de otro
  curso aparecen deshabilitados. Solo se envían nuevos perfiles seleccionados,
  con un máximo de 20 por operación. Antes del agregado se muestran solo nombres
  y disponibilidad, sin edad ni progreso.
- Se muestran `detail` para 403, 404, 409 y 429, y errores 400 por campo. Un 409
  descarta la selección y repite la búsqueda; si falla, exige buscar otra vez.
- Al guardar, el detalle del grupo se reconsulta automáticamente. Las
  respuestas 200 y 201 se consideran éxito; no se promete envío de correo.
- `INVITACIONES_VISIBLES = false` mantiene ocultos botón, modal y listado de
  invitaciones. El código, los métodos y la ruta de aceptación se conservan.
- La demo replica selección de hermanos, unicidad entre cursos, reintentos sin
  duplicar y operaciones todo-o-nada. No usa la red.
- Pruebas: `npm test`, `npm run build`, `npm run lint`. Las solicitudes del
  adaptador se verifican con respuestas simuladas, sin modificar Supabase.


## Vinculación por niño (invitaciones, oculto en el portal)

Una invitación de un niño que ya está en otro curso se rechaza al aceptarla (409).

El profesor envía `{ email, nombre_nino }` desde el detalle de su propio grupo.
Se crea una invitación con UUID, vencimiento y secreto de un solo uso. El envío no
agrega al padre ni incorpora automáticamente sus hijos.

El enlace abre `/invitacion#<token>`. El secreto no aparece en parámetros enviados
al servidor web, no se guarda en localStorage y no se incluye en listados.
La página ofrece el mismo acceso animado: iniciar sesión o registrarse con el
correo de la invitación. El registro crea la cuenta; después del login se exige
confirmar el perfil y aceptar expresamente el vínculo.

Con una cuenta existente, la interfaz ofrece perfiles del padre cuyo nombre
coincide con el de la invitación. El servidor verifica correo, propiedad del ID
seleccionado y nombre antes de vincularlo. No se asigna un niño automáticamente
por nombre. Si existe con otro nombre, el profesor debe cancelar y corregir la
invitación para conservar su progreso. Si no existe, se crea únicamente ese
perfil dentro de la misma transacción que lo incorpora al curso.

La membresía persiste `grupo + jugador_id`. Desde la `0018`, la base exige que
cada `jugador_id` esté en un solo grupo.
Dos hermanos requieren dos invitaciones. Quitar a un integrante elimina solo
su pertenencia al curso, conservando la cuenta, el perfil y el juego.

## FuentePanel

| Operación | Resultado |
|---|---|
| listarNinos | Hijos del padre autenticado |
| obtenerReporteNino | Resumen del hijo autorizado |
| listarGrupos / crearGrupo / obtenerGrupo | Grupos propios del profesor |
| buscarFamilia | Perfiles del correo con disponibilidad para el curso, mediante POST |
| agregarNinos | Incorpora únicamente los IDs seleccionados y el correo consultado |
| invitarFamilia | Invitación para un correo y un niño (flujo oculto) |
| reenviarInvitacion | Nuevo enlace; invalida el anterior |
| cancelarInvitacion | Invalida el enlace sin borrar perfiles |
| eliminarUsuario | Quita una membresía infantil concreta |
| eliminarGrupo | Elimina grupo y enlaces; conserva perfiles |
| obtenerReporteGrupo | Agregado de los niños vinculados, sin filas individuales |

Los errores previstos del nuevo contrato se muestran con mensajes aptos para las
familias y profesores. Un fallo de correo no muestra éxito: la fila conserva
`estado_envio: fallido` y puede reenviarse. Sin proveedor configurado se informa
que el envío no está disponible. La demo solo simula creación, reenvío y cancelación;
no envía correos ni genera enlaces reales de aceptación.

## Roles y privacidad

`GET /auth/perfil/` expone `rol` (`padre`, `profesor` o `admin`) de solo lectura.
Ver la sección [Panel del admin](#panel-del-admin) para el tercero.
El registro no permite asignar el rol de profesor: lo asigna el equipo desde el
admin de Django. Los endpoints de perfiles de menores (`jugadores/`, su detalle
y sus partidas) y el reporte individual responden 403 a los profesores. Padres y madres no pueden
gestionar grupos; profesores consultan sus propios grupos, sus reportes agregados.
Los endpoints nuevos verifican estas reglas en servidor además del frontend.
La invitación no concede acceso a un perfil ajeno ni convierte a un profesor
en responsable de un niño.

`rol` y `is_admin` son independientes. `is_admin` solo da acceso al admin
interno de Django, que muestra los datos de todos los niños, y no se expone al
portal. Marcar una cuenta como profesor no le da ese acceso, y un administrador
técnico no se vuelve profesor. La columna `rol` tiene `padre` como valor por
defecto en la propia base (migración `0013_adulto_rol`), para que el backend
de otra rama que no la conoce pueda seguir creando cuentas.

## Panel del admin

**Backend listo desde el 25 de septiembre de 2026; el frontend falta.**

`rol = "admin"` es el equipo de Fishy!: ve a todos los profesores y sus grupos
desde el portal, en vez de hacerlo por consola. No es lo mismo que `is_admin`
(el admin técnico de Django), y no se asigna desde el portal: solo desde
`/admin/` de Django, o con `python manage.py crear_cuentas_prueba` para pruebas.

### Lo que el frontend tiene que ajustar

- `src/types/api.ts` declara `rol?: 'padre' | 'profesor'`: falta `'admin'`.
- Hoy todo lo que no es `profesor` se trata como padre (`esProfesor` en
  `ProveedorSesion.tsx`). Un admin vería el panel del padre y cada llamada le
  daría 403. Conviene tratar un rol desconocido como error, no como padre.
- Las rutas `/admin/grupos` y el texto "Tutor administrador" son del
  **profesor**. Se sugiere renombrarlos (`/profesor/grupos`, "Profesor") para
  que "admin" signifique una sola cosa.

### Endpoints (todos piden `rol = admin`; si no, 403)

| Método y ruta | Qué hace |
|---|---|
| `GET /api/admin/profesores/` | Todos los profesores: `id, nombre, apellido, email, rol, fecha_creacion, total_grupos` |
| `GET /api/admin/grupos/` | Todos los grupos, con la misma forma que `GET /api/grupos/` más `profesor: {id, nombre, apellido, email}` |
| `GET /api/admin/grupos/?profesor=<id>` | Lo mismo, solo los de un profesor. Un id no numérico da 400 |
| `GET /api/admin/cuentas/?buscar=<texto>` | Busca por correo **o** nombre de usuario **exactos** (sin mayúsculas). Devuelve una lista con `total_perfiles` y `total_grupos`. Sin `buscar`, 400 |
| `PATCH /api/admin/cuentas/<id>/rol/` | Body `{"rol": "profesor"}` o `{"rol": "padre"}`. Devuelve la cuenta |

Además, el admin usa **los mismos** endpoints de lectura del profesor para
cualquier grupo, sea de quien sea:

- `GET /api/grupos/<id>/` (detalle con miembros e invitaciones)
- `GET /api/grupos/<id>/reporte/` (agregado)

### Lo que el admin NO puede (403)

- `GET /api/grupos/` ("mis grupos") y toda escritura en grupos: crear, borrar,
  quitar integrantes, invitar. Siguen siendo del profesor dueño.
- `jugadores/`, su detalle, sus partidas y el reporte individual.

### Errores de `PATCH .../rol/`

| Código | Cuándo |
|---|---|
| 400 | `rol` no es `padre` ni `profesor` (incluye pedir `admin`) |
| 403 | Es la propia cuenta del admin, o la cuenta destino es admin |
| 404 | La cuenta no existe |
| 409 | Pasar a `profesor` una cuenta con perfiles de niños, o a `padre` un profesor que todavía tiene grupos |

Los 403, 404 y 409 traen `detail` con un mensaje listo para mostrar; los 400
traen el error por campo, como `{"rol": ["..."]}`. Repetir el
rol que ya tiene responde 200 sin cambiar nada.

## Reportes

El servidor toma exclusivamente `jugador_id` de las membresías aceptadas, nunca
todos los perfiles de los padres del grupo. Lee las decisiones guardadas que
tienen `opcion_banco_id` resoluble contra el banco y cuentan con tipo
`segura_basica`, `segura_optima` o `insegura`. El porcentaje usa decisiones
seguras / decisiones evaluadas; no convierte un puntaje de riesgo a porcentaje.

Se consideran todas las decisiones clasificadas guardadas de esos perfiles,
en las zonas `desconocidos`, `ciberacoso` y `retos_virales`. El banco, Unity y
`ZonaProgreso` guardan esta última como `reto_viral`; el backend la lee con ese
slug (y con `retos_virales` por compatibilidad) y la entrega siempre como
`retos_virales`, así que el frontend no cambia. Los mensajes
sin clasificación o de zonas desconocidas no generan métricas inventadas.
El backend debe recibir las opciones del juego para que aparezcan resultados.
Otros modos que no guardan opciones del banco no se mezclan en esta métrica.

Cada temática grupal requiere al menos tres niños con resultados. Si no alcanza
la muestra, devuelve `metricas: null` y `motivo: muestra_insuficiente`; si no hay
decisiones clasificadas, `motivo: sin_resultados`. La fecha se deriva de eventos
guardados. La finalización se deriva de `ZonaProgreso.fecha_completada`.

Los reportes grupales y PDF solo reciben métricas agregadas: no incluyen nombres,
correos, IDs infantiles ni conversaciones. Los reportes individuales validan
el propietario. El PDF mantiene el diseño y logo oficiales ya implementados.

## Actualización y validación

### Comprobación

Las pantallas reconsultan al entrar, cada 15 segundos con la pestaña visible
y al recuperar foco o conexión. Las respuestas usan `no-store`. La aceptación
del enlace solo ocurre al confirmar el formulario, nunca al abrirlo (incluidos
escáneres de correo).

Las pruebas cubren creación de cuenta y regreso a la invitación, selección de un
único hijo, conservación del progreso, permisos, duplicados, vencimiento,
cancelación, reenvío, fallo SMTP y agregados que excluyen hermanos. Se ejecutan
con DOM simulado y base SQLite aislada, sin correo externo ni acceso a Supabase.
Pendiente al activar el entorno: configurar SMTP, aplicar la migración, validar
entrega con el proveedor elegido y probar con los eventos reales de Unity.
