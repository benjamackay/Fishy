# Informe de integración backend: cuenta, acceso y reportes

Fecha: 19 de octubre de 2026. Proyecto: Fishy!.

## 1. Alcance y estado de entrega

Este documento entrega al desarrollador backend los flujos, contratos propuestos,
reglas de cálculo, permisos, pendientes de frontend y pruebas necesarias para
conectar el trabajo de tres ramas independientes. No se implementaron endpoints,
migraciones, envío de correo ni cambios en la base de datos durante estas tareas.

**Los endpoints nuevos y los ejemplos JSON de este informe son propuestas de
integración, no servicios ya implementados.** Los endpoints existentes se indican
expresamente. Acordar el contrato antes de conectar los formularios.

| Rama | Commit funcional de referencia | Entregado en frontend | Pendiente principal |
| --- | --- | --- | --- |
| `Feature/CambioDeDatos` | `28296f4` | Menú de usuario, configuración de perfil y contraseña, demo local | Guardar perfil, verificar contraseña actual y cambiarla |
| `Feature/PantallaPreLogin` | `e66673d` | Portada pública, login y registro separados, recuperación por correo y seis casillas numéricas | Solicitar y enviar código, verificarlo y completar el restablecimiento |
| `Feature/ReportePorPeriodo` | `287eb48` | Filtro compacto por fechas, comparación grupal con gráficos, datos ficticios y controles de rol | Agregados reales por período, autorización efectiva y exportación filtrada |

Las tres ramas parten de `dev`; **no contienen automáticamente el trabajo de las
otras dos ni se han integrado a `dev`**. No asumir que abrir la tercera rama
incluye la configuración de cuenta o la portada de las anteriores.

## 2. Contrato y código existentes verificados

Base habitual de la API: `/api`. Autenticación: `Authorization: Token <token>`.
El cliente envía JSON, usa `cache: no-store` y dispone de un timeout de 20 segundos.
Un `401` autenticado provoca el cierre de sesión del portal.

| Método y ruta | Estado actual |
| --- | --- |
| `POST /api/auth/login/` | Existe. Recibe `nombre` y `password`; devuelve `token` y `adulto_id` |
| `POST /api/auth/registro/` | Existe. Devuelve `201` con `token` y `adulto_id` |
| `GET /api/auth/perfil/` | Existe; solo lectura |
| `GET /api/jugadores/{jugador_id}/reporte/` | Existe; reporte acumulado de un menor propio |
| `GET /api/grupos/{grupo_id}/reporte/` | Existe; agregado acumulado del grupo visible |
| `GET /api/grupos/{grupo_id}/` | Existe; debe validar acceso al grupo |

Puntos de partida en el repositorio:

- `Backend/backend/api/urls.py`: rutas actuales.
- `Backend/backend/api/views.py`: `registro`, `auth_login`, `perfil_adulto`.
- `Backend/backend/api/serializers.py`: `RegistroSerializer`, `AdultoResponsableSerializer`.
- `Backend/backend/api/models.py`: `AdultoResponsable`, `Mensaje`, `OpcionBanco`, `ZonaProgreso`.
- `Backend/backend/api/autenticacion.py`: token compartido entre Unity y portal, con vencimiento.
- `Backend/backend/api/reportes_web.py`: cálculo y respuestas de reportes.
- `Backend/backend/api/invitaciones.py`: `grupo_propio` y `grupo_visible`.
- `web/src/api/auth.ts`, `web/src/api/panelReal.ts`, `web/src/types/reportes.ts`: adaptadores y tipos del cliente.

**Compatibilidad relevante:** el login usa nombre de usuario, no correo. El
registro frontend valida que la respuesta incluya un token y un identificador
válidos, pero no guarda ese token ni inicia sesión automáticamente. Cambiar esa
respuesta exige ajustar el frontend.

## 3. Configuración de cuenta — Feature/CambioDeDatos

### 3.1. Comportamiento entregado

Ruta `/configuracion`, protegida por sesión. Menú de usuario con Configuración y
Cerrar sesión. Dos secciones: Perfil y Contraseña y seguridad.

Perfil muestra `nombre`, `apellido`, `email`, `fecha_nacimiento`. Los campos se
editan y pueden descartarse. En el componente el borrador de nacimiento se llama
`fecha`; **al enviarlo debe mapearse a `fecha_nacimiento`**, no enviarse como `fecha`.
Un nacimiento vacío debe representarse con `null` si se quiere borrar el valor.

La contraseña requiere actual, nueva y confirmación. La nueva se valida con al
menos ocho caracteres, debe coincidir con su confirmación y ser distinta de la
actual. Hoy solo se comprueba el formato: no se consulta ni cambia la contraseña.
Los botones dicen “Revisar cambios” y “Revisar contraseña”; no hay guardado real.

### 3.2. Perfil: endpoint propuesto

Extender `PATCH /api/auth/perfil/` sobre el usuario autenticado. No aceptar un ID
de cuenta para elegir el destinatario de la edición.

```json
{
  "nombre": "camila_rojas",
  "apellido": "Rojas",
  "email": "camila@example.com",
  "fecha_nacimiento": "1990-05-17"
}
```

Responder `200` con el perfil completo compatible con `GET /auth/perfil/`:
`id`, `nombre`, `apellido`, `email`, `edad`, `fecha_nacimiento`, `fecha_creacion`, `rol`.

Reglas:

- `nombre`: obligatorio si se modifica, sin quedar en blanco; máximo 150; único.
- `apellido`: máximo 150; admite vacío.
- `email`: formato válido, máximo 254, trim y normalización coherentes con el
  registro existente; unicidad sin distinguir mayúsculas.
- `fecha_nacimiento`: fecha válida o `null`; no futura. Derivar edad de manera
  consistente; no tratar la edad calculada como editable desde este formulario.
- Rechazar escrituras de `id`, `rol`, `is_admin`, `fecha_creacion` y cualquier
  privilegio. Los roles no se escogen ni modifican desde la cuenta personal.
- Validar y guardar atómicamente: un error no debe dejar cambios parciales.
- No serializar hashes ni contraseñas.

Propuesta adicional a acordar: pedir reautenticación para cambiar nombre de
usuario o correo, y verificar el nuevo correo antes de sustituir el anterior.
**La interfaz actual no incluye todavía los campos/pasos para esa confirmación**;
si se adopta, ambos lados deben incorporar el flujo y sus estados pendientes.

### 3.3. Contraseña autenticada: endpoint propuesto

`POST /api/auth/cambiar-contrasena/`, requiere sesión:

```json
{
  "password_actual": "<contraseña actual>",
  "password_nueva": "<nueva contraseña>",
  "password_confirmacion": "<repetición>"
}
```

Comprobar con `check_password`, validar política en servidor y persistir con
`set_password`. Nunca confiar en que las validaciones del formulario ya ocurrieron.
Propuesta de éxito: `200 {"detail":"Contraseña actualizada. Inicia sesión nuevamente.","requiere_login":true}`.

Se recomienda revocar el token tras cambiar/restablecer la contraseña. El token
actual es uno por cuenta y lo comparten Unity y el portal; esta revocación cierra
el acceso de ambos. Comunicarlo al usuario y coordinar con el equipo del juego.
No rotar el token por una simple edición de apellido o fecha de nacimiento.

**Inconsistencia actual a resolver:** registro permite cuatro caracteres, y
configuración propone ocho. Definir una política única, aplicarla en registro,
cambio y recuperación, y actualizar los mensajes y tests de frontend. No asumir
que ya existe una política uniforme de ocho caracteres.

### 3.4. Conexión pendiente en frontend

Agregar métodos a `web/src/api/auth.ts`; sustituir validación de muestra por llamadas
reales; incorporar estado ocupado y errores por campo; actualizar `perfil` en el
proveedor de sesión tras guardar; refrescar nombre/avatar del encabezado; limpiar
contraseñas tras éxito; cerrar sesión si así lo indica el servidor. “Cerrar sesión”
hoy elimina el token del navegador, sin endpoint de revocación remota.

## 4. Portada, login, registro y recuperación — Feature/PantallaPreLogin

### 4.1. Rutas y contenido

- `/`: portada pública para visitantes; sesiones activas redirigen al panel.
- `/panel`: reportes de la familia autenticada.
- `/login`: formulario de ingreso.
- `/registro`: formulario de registro independiente.
- `/recuperar-contrasena`: correo y, después, seis casillas de código numérico.
- `/invitacion`: conserva el flujo de invitación existente y su correo restringido.

La portada describe el juego, las familias/colegios/instituciones, el uso de datos
y contacto. Usa `mailto:contacto@fishygame.cl`; **no hay formulario de contacto
con envío ni se requiere un endpoint para esa sección**. La portada tampoco
prueba que un proveedor de correo esté configurado para la recuperación.

Login y registro conservan los servicios existentes. No asignar rol profesor por
crear una cuenta ni aceptar roles enviados por el cliente. Preservar la ruta
de destino tras autenticarse y el enlace de invitación durante el registro.

### 4.2. Recuperación: estado real de la interfaz

1. Se valida sintácticamente un correo.
2. Se muestran seis casillas que aceptan números y permiten pegar el código.
3. Se valida únicamente que haya seis dígitos.
4. Se muestra un aviso de demostración.

No se comprueba la existencia de la cuenta ni se envían correos. Cualquier código
de seis dígitos pasa la comprobación de formato. **No existe aún una pantalla de
nueva contraseña posterior al código, ni un restablecimiento real.** Ese paso
debe añadirse para cerrar la integración.

### 4.3. Flujo de recuperación propuesto

**Paso 1 — solicitud:** `POST /api/auth/recuperacion/solicitar/`, público.

```json
{"email":"camila@example.com"}
```

Respuesta `202` genérica, tanto si existe como si no existe la cuenta:

```json
{
  "detail":"Si existe una cuenta asociada, recibirás instrucciones.",
  "solicitud_id":"<identificador opaco>",
  "expira_en_segundos":600,
  "reenvio_en_segundos":60
}
```

Los plazos anteriores son **valores iniciales propuestos y configurables**. La
respuesta y el comportamiento observable no deben revelar cuentas existentes;
generar un identificador opaco también para solicitudes sin destinatario válido.
Un correo con formato inválido sí puede recibir `400`.

**Paso 2 — verificación:** `POST /api/auth/recuperacion/verificar/`, público.

```json
{"solicitud_id":"<identificador opaco>","codigo":"012345"}
```

El código es un **string de seis dígitos**, para conservar ceros iniciales.
Responder `200` con una autorización temporal específica para restablecer:

```json
{"reset_token":"<secreto temporal de alta entropía>","expira_en_segundos":300}
```

Este secreto no es el token DRF ni habilita consultar el perfil o jugar.

**Paso 3 — nueva contraseña:** `POST /api/auth/recuperacion/restablecer/`.

```json
{
  "reset_token":"<secreto temporal>",
  "password_nueva":"<nueva contraseña>",
  "password_confirmacion":"<repetición>"
}
```

Responder `200` con confirmación y `requiere_login:true`. Consumir autorización,
guardar hash y revocar tokens atómicamente. Volver al login sin iniciar sesión
automática ni devolver un token DRF de acceso.

### 4.4. Persistencia, correo y controles de recuperación

Modelo sugerido: solicitud con ID opaco, cuenta opcional, huella protegida del
código, vencimiento, contador de intentos, último envío, consumo y huella del
secreto temporal de restablecimiento. No guardar el código ni el secreto en claro.
Como seis dígitos tienen poca entropía, usar una protección con clave del servidor
para el verificador del código; no depender solo de un SHA simple del número.

- Generación criptográfica, comparación segura y consumo de un solo uso.
- Propuesta inicial: cinco intentos por solicitud, código vigente diez minutos,
  espera de sesenta segundos para reenviar y autorización de reset de cinco minutos.
  Configurarlos y acordarlos; el frontend actual no implementa reenvío ni contador.
- Limitar solicitud, verificación y reenvío por origen y por destinatario normalizado.
  Responder `429` con `Retry-After` cuando corresponda.
- Al reenviar, invalidar el código previo y definir el nuevo plazo claramente.
- Proteger la verificación y el consumo ante dos peticiones simultáneas.
- No registrar códigos, contraseñas, tokens ni cuerpos sensibles en logs o errores.
- Configurar proveedor SMTP/transaccional, remitente autorizado, plantilla y cola
  de entrega. El correo debe mostrar código, vencimiento y qué hacer si no se pidió.
  `contacto@fishygame.cl` es el contacto dado por producto; no asumir autorización
  técnica para usarlo como remitente hasta configurar el proveedor.

Frontend debe retener `solicitud_id` y `reset_token` solo durante el flujo, borrar
los secretos al salir y soportar código inválido/vencido, reenvío, `429`, error de
red y confirmación final. No enviar contraseñas dentro de la URL.

## 5. Reportes por período — Feature/ReportePorPeriodo

### 5.1. Contrato propuesto sobre rutas existentes

```text
GET /api/jugadores/{jugador_id}/reporte/?inicio=2026-10-01&fin=2026-10-15
GET /api/grupos/{grupo_id}/reporte/?inicio=2026-10-01&fin=2026-10-15
```

Sin ambos parámetros: acumulado compatible con el actual. Si viene solo uno,
fecha inexistente/formato inválido o inicio posterior a fin: `400` sin calcular
ni devolver un reporte para ese rango. No convertir silenciosamente un rango
incorrecto en acumulado. Los rangos de un solo día son válidos.

Conservar los DTO existentes y agregar metadata del alcance aplicado:

```json
{
  "grupo_id":"<uuid del grupo>",
  "nombre_grupo":"5° Básico A",
  "total_integrantes":4,
  "participantes_con_resultados":4,
  "minimo_participantes":3,
  "actualizado_en":"2026-10-15T03:00:00Z",
  "periodo":{"inicio":"2026-10-15","fin":"2026-10-15","zona_horaria":"America/Santiago"},
  "tematicas":[
    {"tematica":"desconocidos","metricas":{"decisiones_seguras":4,"decisiones_evaluadas":4,"completada":false}},
    {"tematica":"ciberacoso","metricas":null,"motivo":"muestra_insuficiente"},
    {"tematica":"retos_virales","metricas":null,"motivo":"sin_resultados"}
  ]
}
```

Para acumulados, propuesta `periodo:null`. El reporte individual conserva `nino`
en lugar de los campos específicos del grupo. Siempre devolver las tres temáticas.

### 5.2. Fuente y reglas de cálculo verificadas

El código actual usa `Mensaje.timestamp`, vincula `opcion_banco_id` con
`OpcionBanco`, reconoce tipos `segura_basica`, `segura_optima`, `insegura` y cuenta
como seguras las primeras dos. Descarta mensajes sin opción clasificable. Esta
base ya permite filtrar decisiones fechadas: **no reconstruir períodos a partir
de totales acumulados ni usar `Partida.fecha_update` como fecha de decisión**.

Temáticas del portal: `desconocidos`, `ciberacoso`, `retos_virales`. Mantener el alias
existente `reto_viral` → `retos_virales` al leer el banco.

```text
evaluadas = número de decisiones clasificables dentro del rango
seguras = número de esas decisiones de tipo segura_basica o segura_optima
porcentaje = 100 × seguras / evaluadas, solo si evaluadas > 0 y la muestra es suficiente
```

No promediar porcentajes de menores: sumar numeradores y denominadores. No
introducir deduplicación nueva sin definirla: el comportamiento acumulado actual
cuenta los registros de mensajes elegibles. Un 0% con decisiones evaluadas es un
resultado real; ausencia de decisiones es `metricas:null`, nunca un 0% inventado.

### 5.3. Fechas y muestra

Interpretar las fechas como días calendario completos en `America/Santiago`.
Convertir a instantes conscientes de zona y filtrar por intervalo semiabierto:
desde medianoche local del inicio, inclusive, hasta medianoche local del día
siguiente al fin, exclusiva. Convertir los límites a UTC para consultar. Usar una
zona IANA; no fijar el offset en `-03:00` porque Chile cambia de horario.

La fecha `2026-10-05T23:59:59-03:00` pertenece al 5 de octubre local, aunque en UTC
ya sea 6 de octubre. Ese caso está probado en la demo.

Para grupos, el mínimo actual es `max(3, FISHY_REPORTE_MINIMO)`. Se cuentan
**participantes distintos con decisiones evaluadas por temática y por período**;
no el número de decisiones ni solo el total global de participantes del reporte.
Dos personas con cien decisiones cada una no cumplen un mínimo de tres personas.

- Cero decisiones clasificables: `metricas:null`, `motivo:"sin_resultados"`.
- Hay decisiones pero pocos participantes: `metricas:null`, `motivo:"muestra_insuficiente"`.
- No exponer conteos evaluados/seguros ni porcentajes de una muestra suprimida.
- `participantes_con_resultados` se calcula dentro del rango, no desde el acumulado.
- `actualizado_en` no debe incorporar actividad posterior al rango seleccionado.

`ZonaProgreso` también afecta el estado `completada` y la fecha de actualización
actual. **Definir antes de integrar su semántica temporal**: se recomienda separar
“decisiones durante el período” de “estado de completitud al final del período”.
No usar una finalización posterior al fin para marcar completado un reporte
histórico. La demo deja `completada:false`; no demuestra una solución real de
completitud histórica.

### 5.4. Permisos, integrantes e índices

- Familia: solo menores propios; el ID de la URL no otorga acceso.
- Profesor: solo grupos propios; comprobarlo antes de consultar decisiones.
- `grupo_visible` actual también permite al admin del portal leer cualquier grupo.
  El nuevo frontend de comparación está restringido a profesores mediante
  `RequiereAdmin` (el nombre del componente no significa rol `admin`). Para el
  endpoint de comparación se propone `grupo_propio`; confirmar explícitamente si
  el admin del portal debe tener acceso adicional y ajustar ambos lados juntos.
- Mantener agregados grupales sin nombres, correos, IDs de menores ni respuestas.
- Usar los integrantes vinculados al grupo, nunca todos los hijos de las familias.

El cálculo existente usa integrantes actuales. Para compatibilidad, esa es la
base de la demo. Si el producto quiere “quienes pertenecían al grupo en cada fecha”,
se necesita historial de altas/bajas con intervalos y otra consulta; no se puede
inferir de la membresía actual. Acordar la regla antes de publicar comparaciones.

Revisar índices y planes de consulta sobre relación chat/partida/jugador,
`Mensaje.timestamp` y `opcion_banco_id`; ya existe índice en este último. No exigir
una tabla nueva de decisiones si los mensajes existentes son suficientes. Cualquier
migración adicional debe justificarse con el esquema y el volumen real.

## 6. Comparación de períodos de un grupo

### 6.1. Interfaz y llamadas actuales

Ruta de pantalla: `/admin/grupos/{id}/comparar`, desde “Comparar períodos” del
reporte grupal. Dos rangos A y B. Valida cada uno y comprueba pertenencia antes de
consultar los dos agregados con `Promise.all`. No publica resultados parciales;
una consulta inválida elimina la comparación visible. Se vuelve a comprobar
pertenencia incluso al repetir las mismas fechas.

Hoy se usan dos llamadas a `obtenerReporteGrupo(id, {periodo})`; no hay un endpoint
de comparación implementado. Basta conectar los dos reportes filtrados para
mantener esta arquitectura. Como alternativa recomendada para coherencia de lectura,
un único endpoint puede devolver ambos agregados desde la misma instantánea.

### 6.2. Endpoint unificado opcional propuesto

```text
GET /api/grupos/{id}/comparacion/?a_inicio=2026-10-05&a_fin=2026-10-05&b_inicio=2026-10-15&b_fin=2026-10-15
```

Propuesta de respuesta:

```json
{
  "grupo_id":"<uuid>",
  "generado_en":"2026-10-19T15:00:00Z",
  "periodo_a":{"inicio":"2026-10-05","fin":"2026-10-05","reporte":"<DTO ReporteGrupo completo>"},
  "periodo_b":{"inicio":"2026-10-15","fin":"2026-10-15","reporte":"<DTO ReporteGrupo completo>"},
  "diferencias":[{"tematica":"desconocidos","disponible":true,"diferencia_pp":100,"motivo":null}]
}
```

Los marcadores `<DTO ...>` son abreviaturas del documento, no strings literales
del contrato final. Devolver objetos reales con las tres temáticas. Si se elige
esta alternativa, agregar el método a `FuentePanel` y cambiar la pantalla: hoy
espera dos `ReporteGrupo`, no la respuesta unificada.

### 6.3. Reglas de presentación y diferencia

Cada temática muestra barras A/B en la misma escala 0–100, fechas de ambos rangos,
porcentajes y cantidades evaluadas cuando son publicables. La diferencia es:

```text
diferencia_pp = 100 × seguras_B/evaluadas_B − 100 × seguras_A/evaluadas_A
```

Calcular con los contadores originales y redondear solo al presentar, a un decimal.
No restar porcentajes ya redondeados ni llamarlo crecimiento porcentual. Ejemplo:
40% → 60% equivale a +20 puntos porcentuales, no +20% de crecimiento relativo.

Si una temática no tiene datos o no alcanza el mínimo en A o B: diferencia `null`,
motivo explícito y ningún delta numérico. Un período suficiente puede seguir
mostrando sus propios datos; el período suprimido no expone sus conteos.
Los rangos iguales o superpuestos están permitidos actualmente; decisiones
compartidas pertenecen a ambos. Definir una prohibición exigiría cambiar validación
y mensajes. No exigir que B sea posterior a A: las letras fijan el orden de la resta.

Responder `400` por rangos inválidos, `401` sin sesión, `403` por rol y `404` para
grupo inexistente/ajeno según la política actual. Ante error de permisos no devolver
metadata ni resultados parciales del grupo.

## 7. Errores y cambios de cliente necesarios

Propuesta de error nuevo consistente:

```json
{"code":"rango_invalido","detail":"La fecha inicial no puede ser posterior a la final.","errors":{"inicio":["Debe ser anterior o igual a fin."]}}
```

El cliente actual tiene `ApiError` y traducciones por HTTP; no todos los errores
de negocio muestran `detail` ni `errors`. Incorporar mapeo explícito para código
inválido/vencido, demasiados intentos, contraseña actual incorrecta, correo/nombre
duplicado y fechas inválidas. Un error esperado de campo no debe transformarse en
“revisa tu conexión”. Mantener errores DRF actuales donde ya existen para no romper
login, registro e invitaciones.

En `web/src/api/panelReal.ts`, el helper `reporte()` **rechaza hoy `opciones.periodo`**
para no aparentar un filtrado real. Al integrar: serializar `inicio` y `fin` en query,
conservar `signal`, retirar ese rechazo y habilitar los controles con el proveedor
real. La propiedad `Consulta.periodo` es un objeto interno del cliente, no un query
que se envíe automáticamente.

El PDF acumulado se genera hoy en frontend con jsPDF. Con un filtro activo, su
descarga está deshabilitada para evitar exportar un acumulado presentado como
filtrado. Para habilitarla: consultar el mismo rango, incluir fechas y zona horaria
en el documento y respetar supresión y permisos. El PDF comparativo no fue parte
del trabajo entregado y requiere alcance adicional.

## 8. Integración de ramas y modos de muestra

Hay archivos compartidos modificados en las tres ramas: rutas, layout, arranque,
sesión/adaptadores, mocks y estilos. Integrarlas requiere resolver esos cambios
conservando las tres funcionalidades, no escoger íntegramente una versión de archivo.
Especial cuidado con el índice `/`, el nuevo `/panel`, el menú de cuenta y la
navegación de profesores.

- CambioDeDatos: `VITE_FRONTEND_MOCK=true`, solo desarrollo.
- ReportePorPeriodo: `VITE_PERIODO_DEMO=true`, solo desarrollo.
- `.env.local` está ignorado y **no viaja en los commits**. Documentar la variable
  necesaria al entregar el entorno; reiniciar Vite al cambiarla.
- Unificar los dos proveedores de demo al integrar. No habilitar dos proveedores
  de sesión simultáneos ni usar la demo como fallback de errores reales.
- Los clientes de demo bloquean llamadas reales, no guardan tokens de acceso y
  usan perfiles ficticios. La cuenta de profesor de períodos puede recordarse en
  `sessionStorage`, exclusivamente para la demo.
- Los IDs `grupo-demo-1002-a` y `101` son fixtures; no enviarlos al backend como si
  fueran grupos/usuarios reales. Los grupos reales usan UUID.
- Producción debe usar proveedor y autorización reales. Los controles visuales
  no sustituyen validación del servidor.

## 9. Pruebas de aceptación para backend e integración

| Área | Casos mínimos |
| --- | --- |
| Perfil | Lectura propia; PATCH parcial; campo protegido rechazado; unicidad concurrente de correo/nombre; fecha futura; `null` para borrar nacimiento; error sin cambios parciales |
| Cambio de clave | Actual incorrecta; nueva inválida; confirmación distinta; éxito; hash correcto; revocación y efecto en Unity/portal |
| Recuperación | Correo válido/inválido; respuesta no enumerativa; envío y fallo de proveedor; código `012345`; código incorrecto/vencido/usado; límites y reenvío; dos verificaciones/restablecimientos simultáneos; reset token no sirve como token de sesión |
| Registro/login | Contratos existentes; no elevar rol; no sesión automática de registro; conservar invitación y destino |
| Período | Sin fechas = acumulado; una fecha = 400; fecha inexistente; inicio > fin; mismo día; límites locales y horario de verano; solo decisiones evaluadas; cero decisiones = null; cero seguras con evaluadas = 0% |
| Grupo | Menor ajeno y grupo ajeno denegados; no incluir hermanos de otro curso; mínimos por temática/período; participantes distintos; no datos personales en agregados |
| Comparación | Ambos válidos; porcentajes/conteos/fechas/delta; delta desde valores sin redondear; mínimo insuficiente solo en A/B; ambos sin datos; rangos iguales/superpuestos; ningún resultado parcial si una consulta falla; revocación de acceso antes de comparar |
| Exportación | Mismo período y mismas reglas del reporte en pantalla; fechas visibles; permisos y supresión; nunca sustituir silenciosamente por acumulado |

Validación realizada durante el frontend (ejecuciones separadas, no una sola suite
global): cuenta/configuración y acceso relacionados: 50 pruebas; portada/acceso/
invitaciones/permisos: 56; recuperación con código: 5; reportes/permisos/períodos:
44; comparación/períodos/permisos: 39. Compilaciones frontend correctas. Estos
resultados **no validan correo, autenticación nueva ni consultas reales por rango**.

Pruebas específicas de referencia: `web/tests/configuracion.test.tsx` en la primera
rama; `bienvenida.test.tsx` y `recuperar-contrasena.test.tsx` en la segunda;
`periodo.test.tsx` y `comparacion.test.tsx` en la tercera.

## 10. Orden de implementación recomendado

1. Integrar las tres ramas y acordar nombres/errores, política de claves y roles.
2. Implementar PATCH propio y cambio autenticado de contraseña; conectar configuración.
3. Implementar solicitud/verificación/reset, correo y límites; completar la pantalla
   de nueva contraseña y retirar avisos de simulación solo después de conectar.
4. Extender el cálculo actual para rangos, metadata y mínimos, con pruebas de zona horaria.
5. Conectar los adaptadores reales y la comparación, con autorización en cada consulta.
6. Habilitar exportación filtrada, verificar regresiones y desactivar mocks para entrega.

Decisiones pendientes explícitas: política uniforme de contraseña; verificación del
nuevo correo; plazos/límites de recuperación; completitud histórica; miembros
actuales versus históricos; acceso del admin del portal a comparación; dos llamadas
o endpoint unificado; límites máximos de consulta según volumen real. No son
comportamientos de backend ya implementados por estas ramas.
