# Fishy! · Panel de tutores

Frontend en React + TypeScript + Vite, con grupos y agregado de niños por correo
integrados con Django. Las invitaciones se conservan, pero están ocultas en el panel. El envío de correo queda preparado para configurar el
proveedor elegido. Ver [Backend/INVITACIONES.md](../Backend/INVITACIONES.md).

## Ejecutar y probar

Se requiere Node compatible con el proyecto (22.19 o superior).

```sh
npm install
npm run dev
```

En Windows, si PowerShell bloquea npm.ps1, usar `npm.cmd` en los comandos.
Abrir la URL que muestra Vite e iniciar sesión con una cuenta de Django.
El portal usa únicamente la API configurada: no incluye accesos de demostración,
simuladores de progreso ni datos de prueba. Los errores del servicio no activan
datos alternativos. Las fixtures están en `tests/fixtures`, fuera del código publicado.

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
| /login | Inicio de sesión y registro |
| /invitacion#token | Registro/login y aceptación de una invitación para un niño |
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
requieren `perfil.rol === 'profesor'`. Si el campo está ausente o trae otro
valor, la administración permanece bloqueada. `is_admin` (acceso técnico al
admin de Django) no concede nada en el portal. `VITE_FORZAR_ADMIN` no otorga permisos.
El rol real proviene del perfil autenticado, sin selector de rol en el login.
El servicio real debe validar también rol y pertenencia en cada operación.
`RequierePadre` protege las rutas individuales y `aplicarPermisosPanel` rechaza
las lecturas individuales de profesores antes de invocar la fuente de datos,
incluso si el servicio antiguo todavía les atribuye perfiles infantiles.

## Integración del backend

Ver [INTEGRACION_FRONTEND.md](INTEGRACION_FRONTEND.md).
El contrato es `src/types/panel.ts` (`FuentePanel`); el punto de conexión es
`src/api/panelReal.ts`. No hay que reescribir las pantallas.

El login y el listado de perfiles conservan las llamadas que ya existían.
El adaptador conecta grupos, invitaciones, integrantes y reportes
con los endpoints de Django (migración `0014_grupos_invitaciones`). Invitar
necesita además la configuración SMTP del backend; sin ella responde 503 con un
mensaje claro. Las invitaciones exigen aceptación con el correo destinatario y
vinculan solo un perfil infantil.
**No se reemplaza una respuesta fallida del servicio real por datos ficticios.**

Las variables antiguas `VITE_DEMO`, `VITE_GRUPOS_MOCK` y `VITE_FORZAR_ADMIN` no habilitan accesos ni cambian permisos.

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

El acceso alterna entre **Iniciar sesión** y **Registrarse** con un indicador
deslizante, un trazo de luz y entrada escalonada de los campos. La altura se adapta
al contenido y las animaciones respetan `prefers-reduced-motion`. Las pestañas
admiten flechas, Inicio y Fin, y solo el formulario activo queda accesible.
El registro utiliza `/auth/registro/` del contrato existente, muestra errores y
confirma únicamente una respuesta válida. Tras crear la cuenta permite volver
al login con el usuario rellenado. Los permisos siguen viniendo del perfil real.

Café claro `#b78e70`, café pastel `#f0e4d9` y blanco. El café oscuro se utiliza
en texto y controles para legibilidad. Encabezado horizontal superior en escritorio y
navegación adaptada en filas para móvil, rejillas adaptables, formularios con etiquetas, estados
accesibles y ventanas de confirmación mediante `dialog` nativo.
Tipografías DM Sans / Manrope, con fuentes del sistema como respaldo.

El logo oficial de texto se conserva sin modificaciones en
`src/assets/fishy-text-logo.png` y se utiliza en el acceso, la navegación,
el icono del sitio y el PDF. Mantiene proporciones y
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
