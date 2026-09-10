# Fishy! · Panel de tutores

Frontend en React + TypeScript + Vite. El alcance de este trabajo es la interfaz,
los flujos, los estados, la actualización de lecturas y la exportación a PDF.
No se modificó Django, Unity, la base de datos ni se crearon endpoints.

## Ejecutar y probar

Se requiere Node compatible con el proyecto (22.19 o superior).

```sh
npm install
npm run dev
```

En Windows, si PowerShell bloquea npm.ps1, usar `npm.cmd` en los comandos.
Abrir la URL que muestra Vite y seleccionar **Probar como padre** o
**Probar como profesor**. No requiere
backend ni contraseña y siempre muestra una etiqueta de datos ficticios.

- Camila, tutora madre: reportes de Martina (resultados parciales) y Tomás
  (sin actividad), sin acceso a grupos.
- Diego, tutor administrador/profesor: únicamente creación, gestión y reportes
  de grupos. No tiene niños asociados ni acceso a reportes individuales.
- La demo de profesor abre Mis grupos, con un grupo con resultados y otro sin datos.
- En cada formulario de agregar usuario, “Correos de la demostración” lista
  las cuentas ficticias registradas. Un correo desconocido produce un error.
- En un reporte individual, “Probar la actualización automática” simula un
  nivel completado. El cambio también afecta al agregado del grupo cuando
  ese perfil participa en él.
- Los cambios de la demo se guardan en este navegador, separados por cuenta.
  Con almacenamiento bloqueado permanecen en memoria durante esa sesión.
  Las pestañas del mismo origen se notifican sus cambios.
- Si se usó la demo anterior del profesor, se elimina su vínculo individual
  antiguo conservando grupos, integrantes y resultados agregados.

```sh
npm test
npm run build
npm run lint
```

Las pruebas verifican flujos de interfaz en un entorno DOM simulado, aislamiento
de cuentas, reglas de datos, actualización automática y generación del PDF.
No sustituyen una validación visual en navegadores ni pruebas de integración
con el juego y los endpoints reales.

## Rutas

| Ruta | Vista |
|---|---|
| /login | Inicio de sesión existente y entrada separada a demo |
| / | Reportes de los hijos para padres; redirección a grupos para profesores |
| /reportes/:id | Resumen individual por temática, exclusivo de padres |
| /admin/grupos | Lista y creación de grupos |
| /admin/grupos/:id | Información del grupo y gestión de integrantes |
| /admin/grupos/:id/reporte | Agregado anónimo y descarga PDF |
| /partidas/:id | Enlace antiguo; valida pertenencia y lleva al reporte individual |

Existen dos tipos de tutor. Los padres/madres ven únicamente los reportes de sus
hijos y no pueden acceder a grupos. Los administradores (profesores) pueden
crear y gestionar sus grupos y descargar sus reportes agregados.
Los profesores no tienen niños asociados y no pueden abrir ni consultar reportes
individuales. Su navegación contiene únicamente Mis grupos. Al abrir `/`,
`/reportes`, `/reportes/:id` o `/partidas/:id`, vuelven a `/admin/grupos` sin
consultar datos individuales.

La navegación, todo el árbol de rutas `/admin` y las operaciones de `usePanel`
requieren `perfil.is_admin === true`. Si el campo está ausente o es falso, la
administración permanece bloqueada. `VITE_FORZAR_ADMIN` no otorga permisos.
El rol real proviene del perfil autenticado, sin selector de rol en el login.
El servicio real debe validar también rol y pertenencia en cada operación.
`RequierePadre` protege las rutas individuales y `aplicarPermisosPanel` rechaza
las lecturas individuales de profesores antes de invocar la fuente de datos,
incluso si el servicio antiguo todavía les atribuye perfiles infantiles.

## Integración pendiente del otro integrante

Ver [INTEGRACION_FRONTEND.md](INTEGRACION_FRONTEND.md).
El contrato es `src/types/panel.ts` (`FuentePanel`); el punto de conexión es
`src/api/panelReal.ts`. No hay que reescribir las pantallas.

El login y el listado de perfiles conservan las llamadas que ya existían.
Las operaciones nuevas de reportes y grupos están pendientes en ese adaptador:
una cuenta real muestra un estado de función no disponible hasta conectarlas.
**No se reemplaza una respuesta fallida del servicio real por datos ficticios.**

La demo está disponible por defecto solo en desarrollo. `VITE_DEMO=true` la
ofrece explícitamente en un build de demostración; `VITE_DEMO=false` la oculta.
`VITE_GRUPOS_MOCK` y `VITE_FORZAR_ADMIN` no activan las nuevas pantallas.

## Reportes y privacidad

- Tres temáticas fijas: Desconocidos, Ciberacoso y Retos Virales.
- Decisiones seguras / decisiones evaluadas × 100, redondeado al entero.
  Un puntaje de riesgo no se presenta como porcentaje de decisiones seguras.
- Sin evaluaciones: “Sin datos”, sin porcentajes ni barras. Cero decisiones
  seguras de cinco evaluadas sí corresponde a 0%.
- La finalización de una temática se presenta separada de su porcentaje.
- No se solicitan ni muestran conversaciones, alternativas elegidas ni
  transcripciones. Los enlaces antiguos tampoco cargan oportunidades textuales.
- El reporte grupal y el PDF solo reciben métricas agregadas. Los correos
  aparecen exclusivamente en gestión de integrantes.
- Umbral provisional: tres participantes con resultados por temática.
  El contrato debe confirmarlo con el equipo; las temáticas con muestra menor
  se suprimen. El agregado usa la suma de decisiones, no promedios de porcentajes.
- El PDF se genera directamente en el navegador, con jsPDF cargado al descargar,
  y reconsulta el agregado antes de exportar. No captura el DOM.

## Actualización

`useDatosVivos` consulta al entrar y cada 15 segundos con la pestaña visible;
también al recuperar foco, volver a estar visible, recuperar conexión, recibir
cambios de otra pestaña o el evento `fishy:datos-actualizados`.
Una notificación durante una consulta provoca una nueva lectura al terminar.
Al cambiar de cuenta o ruta se descartan respuestas anteriores.

Las lecturas HTTP existentes usan `cache: no-store`, cancelación y un límite
de espera de 20 segundos. Ante fallos temporales se avisa y se conserva el último
resultado visible; ante revocación de acceso se retira. El servidor sigue siendo
responsable de guardar el nuevo progreso y entregar un agregado reciente.

## Diseño

Café claro `#b78e70`, café pastel `#f0e4d9` y blanco. El café oscuro se utiliza
en texto y controles para legibilidad. Navegación lateral en escritorio y
horizontal en móvil, rejillas adaptables, formularios con etiquetas, estados
accesibles y ventanas de confirmación mediante `dialog` nativo.
Tipografías DM Sans / Manrope, con fuentes del sistema como respaldo.

El logo oficial de texto se conserva sin modificaciones en
`src/assets/fishy-text-logo.png` y se utiliza en el acceso, la navegación,
el pie de página, el icono del sitio y el PDF. Mantiene proporciones y
transparencia originales. El logo está incluido en la aplicación, sin
descargas externas adicionales al generar el documento.

La exportación adapta `reportedemo.pdf` a un reporte grupal: A4 blanco,
tipografía serif, logo superior derecho, barras azul/amarilla/ocre,
recuadros de fortalezas y mejora y pie de privacidad. Los datos del niño
y tutor de la referencia se sustituyen por información general del grupo.
El alcance es acumulado; no se inventa un periodo semanal ni un nivel reciente.
Las fortalezas y áreas para reforzar describen comparaciones de porcentajes,
incluyendo empates y resultados parciales. Los patrones de progreso se indican
como no disponibles: las métricas actuales no contienen un análisis de conducta
o evolución. El PDF nunca copia los ejemplos personales de la referencia.

Los criterios y su límite frontend/backend están en
[CRITERIOS_ACEPTACION.md](CRITERIOS_ACEPTACION.md).
