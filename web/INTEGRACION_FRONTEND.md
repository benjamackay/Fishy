# Contrato para integrar el panel

El frontend está preparado y probado con una fuente de datos de demostración.
Este documento no define rutas de API obligatorias ni realiza cambios de backend.

## Punto de conexión

Completar las operaciones de `panelReal` en `src/api/panelReal.ts`, respetando
`FuentePanel` de `src/types/panel.ts`. Las pantallas lo obtienen mediante
`usePanel`. Se mantienen el inicio de sesión y el listado real de perfiles
preexistentes. Las demás operaciones devuelven un error de función no disponible
hasta que se conecten. No hay fallback de datos reales a mock.

| Operación | Resultado esperado |
|---|---|
| listarNinos | NinoResumen[] de los hijos del padre/madre autenticado; no permitido a profesores |
| obtenerReporteNino(id) | ReporteNino del hijo autorizado; no permitido a profesores |
| listarGrupos | Grupo[] administrados por el tutor |
| crearGrupo(datos) | Grupo con identificador único asignado por el servidor |
| obtenerGrupo(id) | GrupoDetalle, con correos para gestionar membresías |
| agregarUsuario(id, email) | GrupoDetalle confirmado por el servidor |
| eliminarUsuario(id, miembroId) | Finaliza solo tras confirmar la eliminación |
| eliminarGrupo(id) | Elimina el grupo; conserva cuentas y progreso |
| obtenerReporteGrupo(id) | ReporteGrupo agregado, sin filas de integrantes |

Las lecturas reciben un AbortSignal opcional. El cliente existente
`api.get(ruta, { signal })` lo acepta; usarlo para abortar al abandonar una vista.
Las mutaciones no deben resolver antes de que el cambio haya sido aceptado.
Mapear errores del servicio a ApiError (401, 403, 404, 409, 400) o ErrorUsuario
con un mensaje apto para el tutor. Un duplicado debe informar “El usuario ya
forma parte del grupo”, incluso si se produce entre solicitudes concurrentes.

## Datos por temática

Los identificadores de dominio de este frontend son `desconocidos`,
`ciberacoso` y `retos_virales`. Si el juego utiliza nombres de zona diferentes,
hacer el mapeo en el adaptador acordado, sin adivinar correspondencias.

```json
{
  "tematica": "desconocidos",
  "metricas": {
    "decisiones_seguras": 8,
    "decisiones_evaluadas": 10,
    "completada": true
  }
}
```

```json
{
  "tematica": "retos_virales",
  "metricas": null,
  "motivo": "sin_resultados"
}
```

Los contadores deben ser enteros no negativos; seguras <= evaluadas.
Sin observaciones, usar null. La UI también completa temáticas omitidas como
ausentes. No convertir riesgo_acumulado, progreso o puntajes normalizados
en un porcentaje de decisiones seguras: representan cosas diferentes.

El reporte individual incluye el niño y su adulto_id para que la interfaz
descarte datos de otra cuenta. Esto es una defensa de presentación: el servidor
debe validar pertenencia antes de entregar cualquier información.

## Reglas que debe garantizar el servidor

- Exponer `is_admin` como booleano en el perfil autenticado: `false` identifica
  al tutor padre/madre y `true` al tutor administrador/profesor. Si se usa otro
  nombre para el rol, mapearlo en el adaptador de autenticación. Mientras el
  campo no esté disponible, la interfaz no habilita administración.
- Reservar todas las operaciones de grupos, reportes grupales y exportación a
  profesores autorizados. Devolver 403 a padres/madres; además, comprobar que
  el profesor administra el grupo solicitado. Ocultar el menú y proteger las
  rutas del frontend no reemplaza estos controles del servidor.
- Los profesores no pueden tener niños asociados ni consultar reportes
  individuales. Reservar la vinculación de hijos y las lecturas individuales
  a padres/madres, y rechazar esas operaciones para profesores, incluso si
  existen asociaciones antiguas. Su migración real corresponde al backend;
  este cambio solo corrige la demo local y los permisos del frontend.
- Autenticar por la sesión existente y derivar del token el tutor responsable.
  Nunca confiar en un adulto_id enviado por el cliente.
- Filtrar niños, grupos, detalle, reportes y mutaciones por autorización.
- Definir qué usuario registrado identifica el correo y qué perfiles aportan
  resultados al grupo. En la demo se utiliza una cuenta ficticia con un perfil.
  No se crean credenciales de menores ni se envían invitaciones por correo.
- Normalizar correo, resolver identidad y evitar duplicados con unicidad
  transaccional. Validar grupo, miembro y autorizaciones también al eliminar.
- Asignar identificadores únicos persistentes. La demo utiliza UUID locales.
- Persistir los eventos del juego al completar nivel/temática y generar el
  reporte con los resultados recientes. Acordar si se incluyen todos los
  intentos, el último o una ventana temporal; el frontend no inventa esa regla.
- Entregar actualizado_en como ISO 8601 correspondiente a los resultados.
- Definir un umbral de resultados suficientes. El frontend usa como mínimo
  provisional tres participantes por temática. Con un umbral mayor, reflejarlo
  en minimo_participantes y aplicar la supresión desde el servidor.
- Suprimir cada temática con pocos participantes usando metricas:null y
  motivo:muestra_insuficiente. No basta con un umbral global del grupo.
- Entregar solo agregados en ReporteGrupo, sin nombres, correos, IDs o
  resultados por integrante. Calcular el agregado en servidor; el navegador
  real no debe descargar resultados de otras familias para sumar localmente.
- No devolver transcripciones o decisiones textuales a estos reportes.
- Evitar cachés que sirvan resultados vencidos. Las lecturas del cliente
  solicitan no-store, pero eso no corrige cachés internas del servidor.

## Actualización sin intervención del tutor

El frontend reconsulta cada 15 segundos mientras la pestaña está visible y al
volver a una vista/foco/conexión. Si se integra SSE o WebSocket, tras una
notificación de progreso se puede emitir:

```ts
window.dispatchEvent(new Event('fishy:datos-actualizados'))
```

No hay suscripción real al juego en este cambio. El registro de progreso en
base de datos y la entrega de nuevos resultados son tareas de integración.

## Comprobación conjunta pendiente

Validar con un padre/madre real con hijos vinculados y un profesor real sin
niños asociados, con grupos a su cargo. Comprobar que el padre no puede listar, crear,
modificar ni obtener reportes de grupos, incluso usando directamente la API.
Comprobar que el profesor puede gestionar solo los grupos autorizados y no puede
vincular niños, listarlos ni consultar sus reportes individuales, tampoco desde
enlaces antiguos o con relaciones previas. Validar el caso de perfil sin rol
y los cambios de sesión entre ambos roles.
Para los reportes individuales, validar acceso directo a
IDs ajenos, cambio de sesión, completar un nivel en Unity, volver al reporte,
observar la actualización, duplicados concurrentes, eliminación, agregados sin
información individual, grupos sin muestra suficiente y PDF con datos actuales.
Revisar también la interfaz en navegador a 320, 390, 768 y 1440 px, zoom 200%,
navegación con teclado y foco/restauración de las ventanas nativas.
