# Alertas del grupo

Las alertas se muestran junto al nombre en la lista de integrantes: icono
ambar, etiqueta Necesita apoyo y etiquetas de las areas por reforzar. El boton
Apoyo cuenta a esos integrantes y filtra la lista, con los prioritarios primero.
No hay panel adicional, puntajes ni porcentajes individuales.

Los datos salen de `GET /grupos/<id>/seguimiento/`, que entrega solo estados:
un area tiene alerta si su estado es `apoyo` o `prioritario`. Si la consulta
falla, la pagina lo dice y no muestra un conteo: nunca se lee como "sin alertas".

La demostracion existente usa senales ficticias y las identifica como
Alertas de ejemplo. Las cuentas reales no reciben alertas inventadas.
La ausencia de una alerta no se presenta como un resultado positivo.
