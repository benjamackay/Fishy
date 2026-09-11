# Integración del panel de Fishy

El frontend consume ahora grupos, invitaciones y reportes reales. El contrato
de dominio permanece en `src/types/panel.ts` y el adaptador en
`src/api/panelReal.ts`. La demostración continúa separada: un fallo del servidor
jamás activa datos ficticios.

La implementación y configuración del servidor están documentadas en
[Backend/INVITACIONES.md](../Backend/INVITACIONES.md). La migración
`0012_grupos_invitaciones` debe aplicarse en el entorno de backend donde se active
esta versión. No se han aplicado cambios a la base compartida desde este trabajo.

## Vinculación por niño

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

La membresía persiste `grupo + jugador_id`, con unicidad en base de datos.
Dos hermanos requieren dos invitaciones. Quitar a un integrante elimina solo
su pertenencia al curso, conservando la cuenta, el perfil y el juego.

## FuentePanel

| Operación | Resultado |
|---|---|
| listarNinos | Hijos del padre autenticado |
| obtenerReporteNino | Resumen del hijo autorizado |
| listarGrupos / crearGrupo / obtenerGrupo | Grupos propios del profesor |
| invitarFamilia | Invitación para un correo y un niño |
| reenviarInvitacion | Nuevo enlace; invalida el anterior |
| cancelarInvitacion | Invalida el enlace sin borrar perfiles |
| eliminarUsuario | Quita una membresía infantil concreta |
| eliminarGrupo | Elimina grupo y enlaces; conserva perfiles |
| obtenerReporteGrupo | Agregado de los niños vinculados, sin filas individuales |
| obtenerSeguimientoGrupo | Seguimiento privado por alumno del propio curso, sin textos del juego |

Los errores previstos del nuevo contrato se muestran con mensajes aptos para las
familias y profesores. Un fallo de correo no muestra éxito: la fila conserva
`estado_envio: fallido` y puede reenviarse. Sin proveedor configurado se informa
que el envío no está disponible. La demo solo simula creación, reenvío y cancelación;
no envía correos ni genera enlaces reales de aceptación.

## Roles y privacidad

`GET /auth/perfil/` expone `is_admin` como booleano de solo lectura.
El registro no permite asignar el rol de profesor. Padres y madres no pueden
gestionar grupos; profesores consultan sus propios grupos, sus agregados y las
necesidades de apoyo de los alumnos vinculados a ese curso.
Los endpoints nuevos verifican estas reglas en servidor además del frontend.
La invitación no concede acceso a un perfil ajeno ni convierte a un profesor
en responsable de un niño.

Los endpoints y el admin histórico del juego conservan su arquitectura existente.
La separación entre administradores internos de Django y profesores del portal
sigue usando el campo histórico `is_admin`; revisar esa política antes de
habilitar el admin interno a cuentas de profesores.

## Reportes

El servidor toma exclusivamente `jugador_id` de las membresías aceptadas, nunca
todos los perfiles de los padres del grupo. Lee las decisiones guardadas que
tienen `opcion_banco_id` resoluble contra el banco y cuentan con tipo
`segura_basica`, `segura_optima` o `insegura`. El porcentaje usa decisiones
seguras / decisiones evaluadas; no convierte un puntaje de riesgo a porcentaje.

Se consideran todas las decisiones clasificadas guardadas de esos perfiles,
en las zonas `desconocidos`, `ciberacoso` y `retos_virales`. Los mensajes
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

### Seguimiento de apoyo en el grupo

`GET /grupos/:id/seguimiento/` devuelve `SeguimientoGrupo` (contrato en
`src/types/seguimiento.ts`). Es una consulta independiente del reporte grupal:
contiene el ID de la membresía, nombre invitado, correo familiar, estado,
contadores por temática, fecha de la última decisión y criterios utilizados.
Solo el profesor propietario puede consultarla. No habilita las rutas de
reportes personales para profesores ni incorpora hermanos o invitaciones pendientes.
El correo y la aceptación de invitaciones explican esta visibilidad a la familia.

Criterios iniciales configurables en `settings.FISHY_SEGUIMIENTO`:

- Se toman las últimas 20 decisiones clasificadas por niño y temática, ordenadas
  por fecha e ID. La ventana se limita en SQL y se recalcula en cada consulta.
- Con un mínimo de 5 decisiones: menos de 40% seguras → `prioritario`;
  de 40% a menos de 60% → `apoyo`; desde 60% → `sin_alertas`.
  Se comparan contadores sin redondear; se muestra como máximo un decimal.
- De 1 a 4 decisiones → `muestra_insuficiente`; ninguna → `sin_datos`.
  En ambos casos porcentaje y contador de decisiones seguras son `null`.
- El resumen general pondera por decisiones de las temáticas con muestra
  suficiente: necesita al menos 10 decisiones en 2 temáticas. Una temática baja
  mantiene la alerta incluso con un promedio alto; no mide modos del juego
  sin opciones clasificadas ni pretende ser un diagnóstico.
- `datos_antiguos` indica que la ventana incluye algún resultado de hace más de
  30 días. Una sola decisión nueva no esconde el resto de la muestra antigua.
  Los resultados futuros o no clasificables se excluyen. Un resultado antiguo
  sigue visible, rotulado para verificar el aprendizaje actual.

El panel reconsulta cada 15 segundos, al volver a él y con cambios del juego.
Una mejora que cruza el umbral retira la alerta automáticamente. La eliminación
de un vínculo también retira su seguimiento; los errores de permisos eliminan
los datos previos y los fallos de conexión identifican la última consulta conocida.
Los filtros distinguen alumnos que requieren apoyo, alumnos sin ninguna temática
con muestra suficiente y todos los alumnos. Las temáticas faltantes siguen
indicándose aunque otras sí tengan resultados.

“Preparar correo a la familia” abre un borrador `mailto:` editable con la
sugerencia de coordinar una conversación, sin envío automático. No depende
del proveedor SMTP de invitaciones. El PDF y la pantalla de reporte mantienen
solo métricas agregadas; el seguimiento también se oculta en la impresión web.

En la demo del profesor, abrir el grupo → “Probar casos de apoyo” → “Simular
casos de apoyo”. Se reemplazan únicamente resultados ficticios del grupo: prioridad,
apoyo, muestra insuficiente y ausencia de datos. “Simular mejora” cambia las
muestras ficticias al 90% y permite comprobar que las alertas desaparecen.

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
