# Desglose de reportes por subcategoría

Los endpoints `GET /api/grupos/<uuid>/reporte/` y
`GET /api/jugadores/<id>/reporte/` conservan las métricas por temática y
agregan `subcategorias` dentro de cada elemento de `tematicas`:

```json
{
  "tematica": "desconocidos",
  "metricas": {
    "decisiones_seguras": 8,
    "decisiones_evaluadas": 10,
    "completada": false
  },
  "subcategorias": [
    {
      "id": "datos_personales",
      "nombre": "Protección de datos personales",
      "metricas": {"decisiones_seguras": 4, "decisiones_evaluadas": 5}
    },
    {
      "id": "secretos",
      "nombre": "Secretos y aislamiento",
      "metricas": null,
      "motivo": "muestra_insuficiente"
    }
  ]
}
```

## Clasificación y alcance

- La taxonomía se define en `backend/api/subcategorias_reportes.py`.
- Se utilizan `PreguntaBanco.categoria` y `PreguntaBanco.etiquetas_ml`,
  vinculadas a las decisiones por `Mensaje.opcion_banco_id`.
- Cada decisión cuenta una vez en cada subcategoría aplicable, aunque varias
  etiquetas coincidan. Una decisión puede trabajar varias subcategorías, por
  lo que sus totales no se suman para reconstruir el total de la temática.
- Las opciones `segura_basica` y `segura_optima` son seguras; `insegura` se
  incluye solo en el denominador. Se conserva el alcance acumulado del reporte.
- Preguntas `neutral` y etiquetas técnicas como `exito`, `fallo` o `fin_npc`
  no generan subcategorías. Las categorías desconocidas conservan su
  contribución al total de la temática, sin inventar un desglose.
- Son etiquetas del contenido del banco, no predicciones de un modelo ML ni
  diagnósticos del niño. No se incluyen resultados del modo Detective en estas
  métricas de decisiones del chat.

## Privacidad y compatibilidad

El reporte grupal exige el mínimo configurado, al menos tres participantes
distintos, **para cada subcategoría**. Varias decisiones de una misma persona
no satisfacen ese mínimo. Si hay evidencia pero no alcanza, `metricas` es
`null` y `motivo` es `muestra_insuficiente`. Si no hay evidencia, el motivo es
`sin_resultados`. No se publican cantidades de participantes por subcategoría,
identificadores, nombres de integrantes ni transcripciones.

El reporte individual conserva la autorización del adulto responsable y usa
un mínimo de un participante. Los nombres del catálogo se devuelven siempre,
independientemente de la actividad, para evitar inferir categorías a partir
de la presencia o ausencia de filas.

El frontend mantiene compatibilidad con respuestas anteriores sin el campo
`subcategorias`, informando que el desglose no está disponible. Las vistas
individual y grupal permiten abrir el desglose de cada temática. El PDF grupal
agrega páginas para las subcategorías, con las mismas reglas de supresión.

No se requieren migraciones ni recarga del banco: se usan campos existentes.
La disponibilidad efectiva depende de las categorías y etiquetas guardadas
en la base de datos.
