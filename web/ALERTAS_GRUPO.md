# Alertas del grupo

Rama `Feature/AlertaGrupo`, desde `dev`.

Las alertas se muestran junto al nombre en la lista de integrantes: icono
ambar, etiqueta Necesita apoyo y etiquetas de las areas por reforzar.
No hay panel adicional, filtros, puntajes ni porcentajes individuales.

La demostracion existente usa senales ficticias y las identifica como
Alertas de ejemplo. Las cuentas reales no reciben alertas inventadas.
El campo opcional frontend `MiembroGrupo.areas_apoyo` permite representar
las areas; no se agregaron llamadas, endpoints ni cambios de backend.
La ausencia de una alerta no se presenta como un resultado positivo.
