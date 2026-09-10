# Criterios de aceptación del frontend

Todos los flujos siguientes están implementados para la demostración. La
aceptación de extremo a extremo requiere conectar los contratos y probarlos
con el servicio real. No se declara completo el registro en base de datos.

Los criterios de grupos (8–18) corresponden exclusivamente al tutor administrador
(profesor). El tutor padre/madre solo consulta los reportes de sus hijos.
El profesor solo ve grupos: no tiene niños asociados, menú de reportes
individuales ni acceso a esas lecturas. Las rutas individuales y sus enlaces
antiguos lo redirigen a grupos sin cargar información de niños.
La interfaz oculta la navegación de grupos para padres, bloquea sus accesos
directos a `/admin` y rechaza todas las operaciones grupales antes de invocar
el servicio. También se prueban perfiles sin rol, cambio entre ambos tipos de
tutor, sesión real de profesor, bloqueo de lecturas individuales y corrección
de asociaciones antiguas en la demo sin perder grupos. La API debe repetir
estas validaciones y corregir las asociaciones reales antiguas si existen.

| CA | Comportamiento implementado | Validación frontend / dependencia real |
|---|---|---|
| 1 | Reportes solo de niños vinculados; acceso directo ajeno rechazado | Pruebas de cuenta y ruta. El servidor debe autorizar y filtrar. |
| 2 | Reporte individual organizado por temática | Pruebas de pantalla con las tres temáticas. |
| 3 | Recepción de cambios y reconsulta automática; simulación de nivel | Pruebas de eventos y datos. Registrar progreso real corresponde al juego/backend. |
| 4 | Reconsulta al volver a la vista, foco y visibilidad; sondeo de 15 s | Pruebas de regreso, reloj, reconexión y respuestas tardías. |
| 5 | Sin conversaciones ni decisiones textuales | Nueva presentación agregada; vista antigua redirigida y sin consultas textuales. |
| 6 | Porcentaje y contadores de decisiones seguras por las tres temáticas | Pruebas de valores válidos, cero seguro y métricas de grupo. |
| 7 | Sin datos: sin porcentaje ni barra | Pruebas por temática y niño sin actividad. |
| 8 | Crear grupo abre formulario con nombre y descripción | Prueba del flujo de creación y validación. |
| 9 | Confirmación de creación e identificador único | Prueba de UUID y persistencia de demo. UUID definitivo lo asigna el servidor. |
| 10 | Detalle con gestión habilitada tras crear | Prueba del flujo completo. |
| 11 | Agregar usuario abre formulario de correo | Prueba de formulario. |
| 12 | Alta por correo y confirmación de éxito | Pruebas de correo registrado, normalización y error de usuario desconocido. |
| 13 | Eliminación pide confirmación y comunica éxito | Prueba de cancelar, confirmar y volver a agregar. |
| 14 | Ver reporte presenta agregado por temática | Pruebas de agregación ponderada y pantalla. Agregado real corresponde al servidor. |
| 15 | Sin muestra suficiente: aviso sin porcentajes, PDF deshabilitado | Pruebas de grupo vacío y grupos pequeños. Umbral provisional: 3 por temática. |
| 16 | Reporte grupal sin datos personales ni resultados individuales | Pruebas de contenido de DTO, pantalla y PDF. El servidor debe omitir esos datos. |
| 17 | Descarga de un archivo PDF con el agregado más reciente | Prueba de generación PDF y de la acción de descarga con reconsulta. |
| 18 | Impide duplicados e informa que ya pertenece | Pruebas de mayúsculas/espacios y altas concurrentes en el mock. Unicidad real en servidor. |

Las pruebas de interfaz usan React Testing Library y jsdom. La exportación se
comprueba como documento PDF y por sus campos permitidos. La acción de descarga
se prueba con la función de guardado simulada. La revisión visual en dispositivos
y el guardado final del archivo por distintos navegadores quedan para QA.
