# Invitaciones a un niño de un curso

Implementación del portal y API de grupos, correo y aceptación. Todavía no se ha
elegido proveedor ni remitente: el envío está desactivado de forma explícita.
No se enviaron mensajes externos ni se migró la base compartida durante el desarrollo.

## Modelo y recorrido

1. Un profesor crea un `GrupoTutor` del que es propietario.
2. Envía una `InvitacionGrupo` con correo del padre y nombre del niño. Se asigna
   UUID, secreto aleatorio de 256 bits, hash SHA-256 y vencimiento de siete días.
3. El servidor confirma la fila antes de enviar el correo HTML y texto plano.
   El mensaje contiene profesor, curso, nombre del niño y enlace, sin RUT, edad,
   fecha de nacimiento, conversaciones ni resultados.
4. El enlace abre el frontend en `/invitacion#<token>`. La lectura no lo consume.
5. El adulto se registra o inicia sesión con el correo destinatario. No puede
   aceptar con una cuenta de profesor o de otra familia.
6. Confirma un perfil existente del niño o crea uno. Un perfil existente debe
   pertenecerle y coincidir con el nombre invitado (NFC, espacios y mayúsculas
   normalizados). La confirmación explícita y el ID elegido identifican el perfil;
   nunca se vincula por coincidencia de nombres sin selección del padre.
7. En una transacción se crea la membresía `grupo + jugador`, se crea el perfil
   solo si es necesario y se marca la invitación aceptada. Un reintento de la
   aceptación exitosa devuelve el mismo vínculo sin duplicarlo.

Un correo puede recibir invitaciones para distintos hijos. Cada una afecta a un
solo niño. Si el padre ya tiene el perfil con otro nombre, debe pedir al profesor
que cancele y corrija la invitación; no se renombra ni duplica automáticamente.

## Endpoints (prefijo `/api`)

| Método y ruta | Cuerpo / resultado |
|---|---|
| GET /grupos/ | Grupos del profesor autenticado |
| POST /grupos/ | `{ nombre, descripcion? }` → grupo UUID |
| GET /grupos/:id/ | Grupo, miembros con nombre/correo e invitaciones sin secretos |
| DELETE /grupos/:id/ | Borra el grupo y sus enlaces; conserva perfiles y progreso |
| POST /grupos/:id/invitaciones/ | `{ email, nombre_nino }` → invitación enviada |
| POST /grupos/:id/invitaciones/:invitacion/ | Reenvía; rota el secreto y reinicia vencimiento |
| DELETE /grupos/:id/invitaciones/:invitacion/ | Cancela una invitación pendiente o vencida |
| POST /invitaciones/consultar/ | `{ token }` → curso, profesor, niño, email y vencimiento |
| POST /invitaciones/aceptar/ | `{ token, confirmar: true, jugador_id }` o `{ token, confirmar: true, crear_perfil: true }` |
| DELETE /grupos/:id/miembros/:miembro/ | Retira solo ese niño del grupo |
| GET /grupos/:id/reporte/ | Agregado con mínimo tres niños por temática |
| GET /grupos/:id/seguimiento/ | Seguimiento privado de apoyo por alumno, solo profesor propietario |
| GET /jugadores/:id/reporte/ | Resumen del hijo del padre autenticado |

La aceptación devuelve `{ aceptada: true, jugador_id, grupo }`. No recibe
`adulto_id`, `grupo_id`, nombre del niño ni correo modificables: se derivan del
token autenticado y de la invitación inmutable.

`estado`: pendiente, aceptada o cancelada (vencida se calcula al consultar).
`estado_envio`: enviando, enviado o fallido. Un error de SMTP devuelve 503,
conserva la invitación fallida y permite reenvío. No se afirma recepción en bandeja:
enviado significa que el servidor de correo aceptó el mensaje.

El reenvío espera al menos un minuto, invalida el enlace previo y conserva el
registro de invitación. El profesor puede cancelar y crear otra para corregir
datos. El límite de solicitudes protege el envío y la consulta de enlaces.
Los bloqueos de grupo/invitación/adulto y las restricciones de unicidad evitan
dobles membresías. SQLite valida lógica y restricciones; la concurrencia por
filas se ejecuta con PostgreSQL en producción.

## Activación en el entorno elegido

El seguimiento por alumno está implementado en `api/seguimiento.py` y sus
criterios iniciales en `settings.FISHY_SEGUIMIENTO`. Usa las últimas 20 decisiones
clasificadas por alumno y temática; requiere 5 para evaluar apoyo (<60%) o
prioridad (<40%). El resumen general necesita al menos 10 decisiones en 2
temáticas. Cada tema de la muestra que incluye datos de hace más de 30 días
se señala como antiguo; no se evalúan decisiones futuras. Las reglas pueden
ajustarse antes de activar el entorno con el equipo educativo.

Este endpoint no lee textos de las decisiones y no modifica resultados, perfiles
o membresías. El nombre y correo provienen solo de miembros aceptados del curso.
Las invitaciones informan a las familias de esta visibilidad; el PDF sigue usando
exclusivamente el endpoint agregado. No se añade otra migración para seguimiento.
El detalle del contrato y los casos borde están en `web/INTEGRACION_FRONTEND.md`.

1. Aplicar la migración aditiva `0012_grupos_invitaciones` con el proceso habitual
   del equipo y las variables de su base: `python manage.py migrate` desde
   `Backend/backend`. No importa grupos ficticios del navegador ni modifica
   partidas o relaciones padre-hijo existentes.
2. Completar en `Backend/.env` las variables de `Backend/.env.example`:
   `FISHY_WEB_URL` (URL pública HTTPS del portal), `DEFAULT_FROM_EMAIL`,
   `EMAIL_HOST`, `EMAIL_PORT`, `EMAIL_HOST_USER`, `EMAIL_HOST_PASSWORD`.
   Para SMTP de puerto 587 suele usarse `EMAIL_USE_TLS=True` y
   `EMAIL_USE_SSL=False`; ajustar según el proveedor. No activar ambas.
3. Establecer `FISHY_EMAIL_ENABLED=True` y reiniciar el backend mediante el
   lanzador que carga `.env`. Las credenciales nunca van en variables `VITE_*`.
4. Validar un envío al destinatario de prueba autorizado y la aceptación con
   una cuenta de padre. Configurar el proxy web para que `/invitacion` sirva
   la aplicación (igual que `/login` y las demás rutas).

El envío utiliza el backend SMTP estándar de Django y un timeout de diez segundos.
La operación es síncrona; no requiere instalar Celery ni un servicio de colas.
Si el proveedor acepta el correo pero hay una interrupción antes de guardar el
resultado, la fila puede quedar en `enviando`; reenviarla después de un minuto
rota el secreto y permite recuperar el flujo. No hay reintentos automáticos.

Referencia técnica: [correo en Django](https://docs.djangoproject.com/en/5.2/topics/email/)
y [recomendaciones OWASP para tokens por correo](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html).

## Pruebas sin proveedor ni base compartida

Desde `Backend/backend`, con el entorno Python del proyecto activo:

```sh
python manage.py test api.tests.test_invitaciones --settings=juego_backend.settings_test_sqlite
python manage.py test api --settings=juego_backend.settings_test_sqlite
python manage.py makemigrations --check --dry-run --settings=juego_backend.settings_test_sqlite
```

Las pruebas de invitaciones usan el buzón en memoria de Django. Verifican el
contenido y destinatario de los correos, expiración, rotación, cancelación,
cuentas nuevas/existentes, aislamiento de hermanos, rollback y fallos de envío.
No envían correo externo. En el frontend: `npm test`, `npm run build`, `npm run lint`.

La demo permite simular invitaciones pendientes, reenvío y cancelación. Está
rotulada y no genera enlaces reales ni incorpora niños sin una aceptación real.
Los grupos de muestra conservan sus integrantes ficticios existentes.
