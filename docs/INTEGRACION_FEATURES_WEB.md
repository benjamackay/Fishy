# Integración de las tres interfaces con dev

Se integraron `Feature/CambioDeDatos`, `Feature/PantallaPreLogin` y
`Feature/ReportePorPeriodo` con `origin/dev` hasta `1a7aa2c`.
El informe original `INFORME_INTEGRACION_BACKEND_TRES_FEATURES.md` describe
las propuestas anteriores a esta implementación.

## Funcionalidad conectada

- Portada pública, acceso y registro separados; se conservan invitaciones y destinos.
- Configuración: `PATCH /api/auth/perfil/` edita solo nombre, apellido, correo y
  nacimiento de la cuenta autenticada. Devuelve el perfil actualizado para el encabezado.
  El nacimiento acepta `null`; la edad se recalcula. Campos de privilegios se rechazan.
- `POST /api/auth/cambiar-contrasena/` comprueba la contraseña actual, confirma
  la nueva y aplica los validadores de Django. Revoca las sesiones del portal y
  del juego; el usuario debe iniciar sesión nuevamente.
- Recuperación: se usan los tres endpoints de `api/recuperacion.py` que trae dev.
  La pantalla conserva solicitud y permiso temporal en memoria, permite reenviar
  respetando la espera y agrega la entrada de nueva contraseña. El backend decide
  vigencia, intentos y validez; ningún código se acepta por su formato solamente.
- Reportes individuales y grupales: `inicio` y `fin` opcionales, juntos, en formato
  `YYYY-MM-DD`. Sin fechas se conserva el acumulado. Rangos inválidos devuelven 400.
  Se filtra `Mensaje.timestamp` entre medianoches de `America/Santiago`, con fin
  exclusivo al día siguiente. No se reconstruyen períodos desde totales.
- Se mantienen permisos y mínimo de participantes por temática y período.
  La comparación realiza dos consultas filtradas y comprueba acceso antes de ambas.
- El PDF grupal reconsulta el mismo período e incluye fechas y zona horaria.

## Decisiones de compatibilidad

Se consideran los integrantes actuales del grupo. La completitud corresponde
al estado al final del período, sin incorporar finalizaciones posteriores;
la fecha de actualización solo incluye actividad dentro del rango.
El registro conserva su contrato anterior (mínimo de cuatro caracteres) para
compatibilidad con Unity. Cambio y recuperación aplican los validadores de Django,
incluido el mínimo de ocho; no se modifican contraseñas existentes.
El correo nuevo se guarda directamente, como en el registro existente.

Las variables `VITE_FRONTEND_MOCK=true` y `VITE_PERIODO_DEMO=true` seleccionan
un único proveedor de demostración, exclusivamente en desarrollo. Producción
usa el backend real y los errores nunca activan datos ficticios.

## Validación y publicación

La suite Django usa SQLite de prueba en memoria; no modifica Supabase.
Incluye pruebas de edición propia, campos protegidos, unicidad de correo,
revocación de tokens, límites locales de fechas y supresión por muestra.
Las pruebas del portal verifican el flujo completo de recuperación y los contratos HTTP.

Para publicar, desplegar Django con `backend/requirements.txt`, aplicar las
migraciones `0021` y `0022` que ya trae dev y configurar Brevo/remitente.
Luego generar y publicar `web/dist`. Esta integración no aplica migraciones
en la base compartida ni verifica entregas reales de correo.
