# Alertas en el detalle del grupo

Rama: `Feature/AlertaGrupo`, creada desde `dev` (`8ee7d53`).

En `/admin/grupos/:id` se agrega una sección de acompañamiento antes de los
integrantes. La vista muestra niños y áreas con señales cualitativas:
**Requiere apoyo** y **Conviene reforzar**. No presenta porcentajes,
puntajes, respuestas ni comparaciones individuales.

Incluye búsqueda de nombres sin distinguir tildes, filtros por temática y
tipo de alerta, resumen por niños distintos y orientaciones desplegables.
Los niños con señales de apoyo aparecen primero. Un niño puede figurar en
ambos tipos si necesita acompañamiento en distintas áreas.

## Probar la interfaz

Ejecutar el portal en desarrollo con `VITE_PERIODO_DEMO=true`, entrar como
profesor y abrir un grupo. El botón **Ver ejemplo de alertas** permite recorrer
las tarjetas ficticias; **Cerrar ejemplo** restaura la vista inicial.
También se puede abrir el ejemplo desde un grupo real, siempre con el aviso
de que no corresponde a los resultados de sus integrantes.

`AlertasGrupo` admite una lista de señales de presentación (`AlertaGrupo[]`).
`null` indica que las alertas no están disponibles; `[]` representa una lista
disponible sin alertas. No se deducen resultados desde los agregados grupales
ni se hacen peticiones HTTP adicionales. El ejemplo no se guarda ni modifica
datos de los niños.

El alcance es exclusivamente frontend. No se agregaron endpoints, permisos,
umbrales de evaluación ni cambios de base de datos. La rama no habilita alertas
reales: esa conexión requerirá una tarea posterior.

Verificado con las 126 pruebas del frontend, compilación de producción y
revisión en navegador. El análisis estático conserva los dos avisos previos
de `AgregarNinos.tsx`.
