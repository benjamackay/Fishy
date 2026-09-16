# Pendiente backend: Recompensas del Modo Detective (HDU-11)

**Fecha:** 2026-09-16 · **Alcance:** exponer en la API el bloque `recompensa`
que `banco_preguntas/detective_cases.json` ya trae por caso. **No bloquea el
juego** —Unity ya funciona sin esto, con un respaldo local— pero **si se
implementa, Unity ya está listo para usarlo sin ningún cambio adicional**:
ver "Cómo lo va a usar Unity en cuanto exista" más abajo.

## Contexto

Esta sesión implementó HDU-11 **solo en Unity**: al resolver un caso
Detective con acierto ≥ umbral, el juego agrega un ítem "pin" al inventario
(`InventoryManager`), sin duplicar si ya lo tiene, y muestra un popup
temporal. También adelantó una versión mínima de HDU-12 (álbum de
evidencias): un ítem "Álbum de Evidencias" que aparece junto con el primer
pin y, al tocarlo, lista los pines obtenidos.

Unity resuelve qué pin corresponde a cada caso con un **fallback en dos
pasos**: primero mira si el caso que acaba de cargar ya trae su propia
recompensa (eso vendría del backend, si se implementa lo de abajo); si no
trae nada ahí —que es la situación de hoy, porque el backend todavía no
manda el campo— cae a un catálogo local nuevo
(`Fishy!/Assets/Scripts/Detective/CatalogoRecompensasDetective.cs`) que lee
`Fishy!/Assets/Resources/detective_recompensas.json`:
```json
{
  "recompensas": [
    { "caseId": "DC_CASO_01", "itemId": "PIN_VIGIA_SILENCIOSO", "nombre": "Pin del Vigía Silencioso",
      "accesorioHdu06": "Gorro de detective con visera", "umbralAciertos": 0.5, "noDuplicaAlRepetir": true },
    { "caseId": "DC_CASO_02", "itemId": "PIN_TESTIGO_SOLIDARIO", ... },
    { "caseId": "DC_CASO_03", "itemId": "PIN_ANALISTA_CORRIENTES", ... }
  ]
}
```
Esto es una copia, en formato Unity, del bloque `recompensa` que
`banco_preguntas/detective_cases.json` ya trae por caso (fuente original):
```json
"DC_CASO_01": {
  "recompensa": {
    "item_id": "PIN_VIGIA_SILENCIOSO",
    "nombre": "Pin del Vigía Silencioso",
    "accesorio_hdu06": "Gorro de detective con visera",
    "umbral_aciertos": 0.5,
    "no_duplica_al_repetir": true
  }
}
```
**El backend real (`cargar_detective.py`, modelo `CasoDetective`, su
serializer) sigue sin leer ni exponer `recompensa` hoy.** Unity no depende de
eso para funcionar, pero ya tiene todo el lado suyo listo: `CasoDetectiveDto`
(`ApiManager.cs`) ya declara los 5 campos `recompensa_*`, y
`DetectiveCaseLoader.FromDto` ya los copia al caso. Si el backend nunca los
manda, llegan vacíos y el juego sigue funcionando con el catálogo local — así
que esto se puede implementar cuando convenga, sin coordinar un despliegue
con Unity. Si en algún momento se quiere que diseño pueda ajustar
recompensas sin tocar el repo de Unity, esto es lo que falta:

## Qué falta (si se decide hacerlo)

### 1. Modelo — `backend/api/models.py`, clase `CasoDetective` (línea 435)

Agregar 5 campos planos, mismo criterio que `permiso_player_text` /
`permiso_npc_nombre` / `permiso_npc_response` que ya tiene el modelo (un solo
grupo de datos por caso, no una tabla aparte):

```python
recompensa_item_id               = models.CharField(max_length=60, blank=True, default="")
recompensa_nombre                = models.CharField(max_length=150, blank=True, default="")
recompensa_accesorio_hdu06       = models.CharField(max_length=150, blank=True, default="")
recompensa_umbral_aciertos       = models.FloatField(default=0.5)
recompensa_no_duplica_al_repetir = models.BooleanField(default=True)
```

Esto es independiente de `RecompensaAlbum` (models.py:582, la tabla que ya
existe para las recompensas de Misión) — no hace falta tocar esa tabla ni su
`CheckConstraint`. El pin de HDU-11 va directo al inventario del jugador, que
ya es genérico por `item_id` de texto.

### 2. Migración

```
python manage.py makemigrations api
```
Debería generar algo como `0013_casodetective_recompensa_...py` (la carpeta
llega hasta `0012_personaje_zona_actual.py` a la fecha de este documento).

### 3. `backend/api/management/commands/cargar_detective.py` (línea 57)

Leer el bloque `recompensa` igual que ya se lee `permiso`, y agregarlo a
`defaults`:

```python
permiso = c.get("permiso") or {}
recompensa = c.get("recompensa") or {}
defaults = {
    "titulo":               c.get("titulo", ""),
    "zona":                 c.get("zona", ""),
    "etiquetas_ml":         c.get("etiquetas_ml", []),
    "permiso_player_text":  permiso.get("player_text", ""),
    "permiso_npc_nombre":   permiso.get("npc_nombre", ""),
    "permiso_npc_response": permiso.get("npc_response", ""),
    "recompensa_item_id":               recompensa.get("item_id", ""),
    "recompensa_nombre":                recompensa.get("nombre", ""),
    "recompensa_accesorio_hdu06":       recompensa.get("accesorio_hdu06", ""),
    "recompensa_umbral_aciertos":       recompensa.get("umbral_aciertos", 0.5),
    "recompensa_no_duplica_al_repetir": recompensa.get("no_duplica_al_repetir", True),
}
```

### 4. `backend/api/serializers.py`, `CasoDetectiveSerializer` (línea 122)

Agregar los 5 campos a `fields` (línea 128):

```python
fields = [
    "id", "caso_id", "titulo", "zona", "etiquetas_ml",
    "permiso_player_text", "permiso_npc_nombre", "permiso_npc_response",
    "recompensa_item_id", "recompensa_nombre", "recompensa_accesorio_hdu06",
    "recompensa_umbral_aciertos", "recompensa_no_duplica_al_repetir",
    "mensajes",
]
```

### 5. Recargar datos y verificar

```
python manage.py cargar_detective --limpiar
```
y confirmar que el endpoint de casos Detective trae los 5 campos
`recompensa_*` no vacíos.

## Cómo lo va a usar Unity en cuanto exista

`DetectiveCaseManager.OtorgarRecompensaSiCorresponde` (vía
`ResolverRecompensa`) prueba primero si el caso recién cargado ya trae su
propia recompensa (`DetectiveCase.TieneRecompensa`, que mira
`recompensaItemId`); si sí, usa esos 5 valores tal cual —los que mandó el
backend— sin tocar el catálogo local. Solo si vienen vacíos (backend sin
implementar, como hoy; o caso cargado desde el respaldo local en
`Resources/`) cae a `CatalogoRecompensasDetective`. Es el mismo patrón
try-backend-then-local que ya usa `DetectiveCaseLoader.LoadAsync` para el
contenido del caso, aplicado a la recompensa. Implementarlo **no requiere
ningún cambio en Unity**: en cuanto el serializer mande los 5 campos, dejan
de estar vacíos y automáticamente pasan a ser la fuente de verdad — sin
desplegar nada nuevo de Unity ni coordinar el momento. Verificado con un test
headless (`FishyPruebasDetectiveRecompensa.ProbarUsaRecompensaDelBackendSiElCasoLaTrae`)
que simula un caso con recompensa propia y confirma que gana por sobre la del
catálogo local.

## Sobre el álbum mínimo (adelanto de HDU-12)

No requiere ningún cambio de backend: es un ítem más de inventario
(`ITEM_ALBUM_EVIDENCIAS`), y `InventarioBackendSync` ya sube la mochila
completa al guardar. La HDU-12 completa (agrupar evidencias por caso,
registrar cada señal de riesgo identificada, `EvidenceAlbumManager`, etc.) es
una historia aparte, no cubierta acá.

## Fuera de alcance (a propósito)

- No se toca `RecompensaAlbum` ni se crea álbum server-side: HDU-12 completa
  queda para más adelante.
- No hay endpoint nuevo para "reclamar" el pin: Unity lo agrega directamente
  al inventario del jugador por el mecanismo normal de guardado de mochila.
  El backend no necesita "saber" que un ítem vino de una recompensa
  Detective — es solo otro ítem en la mochila.
