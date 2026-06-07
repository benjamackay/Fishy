# Documentación Técnica — Banco de Preguntas (MLOps)

**Autor:** Luis González — MLOps  
**Última actualización:** 2026-06-06  
**Stack:** Django 6 · PostgreSQL · Unity C# · FastAPI (pendiente)

---

## Índice

1. [Arquitectura general](#1-arquitectura-general)
2. [Archivos del banco](#2-archivos-del-banco)
3. [Modelos Django](#3-modelos-django)
4. [Migraciones](#4-migraciones)
5. [Comando de carga](#5-comando-de-carga)
6. [Endpoints API](#6-endpoints-api)
7. [Integración Unity](#7-integración-unity)
8. [Cómo ejecutar](#8-cómo-ejecutar)

---

## 1. Arquitectura general

```
banco_preguntas.json
       │
       ▼
python manage.py cargar_banco
       │
       ▼
PostgreSQL
  ├── PreguntaBanco
  └── OpcionBanco
       │
       ▼
GET /api/banco/preguntas/   ←── Unity (ApiManager.ObtenerPreguntas)
GET /api/banco/preguntas/<id>/  ←── Unity (ApiManager.ObtenerPregunta)
```

El banco es **contenido estático** que se carga una vez a la BD. En runtime, Unity consulta los endpoints para construir el flujo de conversación con los NPCs.

---

## 2. Archivos del banco

### `banco_preguntas/banco_preguntas.json`

Fuente de verdad del contenido. Estructura de alto nivel:

```json
{
  "version": "1.1",
  "formato_respuesta": { ... },
  "preguntas": [ ... ]
}
```

#### Schema de una pregunta

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `id` | string | Identificador único. Formato: `HDU2_NPC01_F2_Q01` |
| `hdu` | string | `"HDU-2"` o `"HDU-8"` |
| `zona` | string | `"desconocidos"` o `"chat_simulado"` |
| `npc_id` | string | `"NPC_01"`, `"NPC_02"` o `""` (HDU-8) |
| `npc_nombre` | string | Nombre visible del NPC |
| `npc_avatar` | string | Key del sprite en Unity |
| `fase` | int\|null | Fase de la conversación (1, 2, 3). `null` para estados de cierre |
| `orden_en_fase` | int\|null | Orden dentro de la fase |
| `narrativa_continuacion` | string\|null | ID de la siguiente pregunta (solo mensajes neutros) |
| `escenario_id` | string | ID del escenario HDU-8 (`"CHAT_GROOMING_01"`, etc.) |
| `escenario_nombre` | string | Nombre legible del escenario |
| `historial_previo` | array | Mensajes anteriores mostrados como contexto (HDU-8) |
| `categoria` | string | ID de categoría del archivo `categorias.json` |
| `nivel_riesgo` | int | 0 = neutral · 1 = bajo · 2 = medio · 3 = alto |
| `es_mensaje_riesgo` | bool | `true` → presenta opciones de respuesta al jugador |
| `es_fin_de_npc` | bool | `true` → estado final de interacción (FIN_SEGURO / FIN_INSEGURO) |
| `es_fin_de_zona` | bool | `true` → dispara desbloqueo de la siguiente zona (ZONA_FIN) |
| `mensaje_npc` | string | Texto que muestra el NPC en pantalla |
| `etiquetas_ml` | array | Tags para el clasificador FastAPI/Scikit-learn |
| `opciones_respuesta` | array\|null | `null` en mensajes neutros y estados de cierre |

#### Schema de una opción de respuesta

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `id` | string | Formato: `HDU2_NPC01_F2_Q01_R1` |
| `texto` | string | Texto que ve el jugador (máx. 1 línea) |
| `tipo` | string | `"insegura"`, `"segura_basica"` o `"segura_optima"` |
| `consecuencia_narrativa` | string | Texto de feedback tras elegir esta opción |
| `impacto_puntuacion` | int | −1, +1 o +2 |
| `siguiente_pregunta` | string\|null | ID de la siguiente pregunta. `null` = dead-end (fin de rama) |

---

### `banco_preguntas/categorias.json`

Define los 7 tipos de mensaje usados por el clasificador ML.

| ID | Nivel riesgo | Descripción |
|----|:---:|-------------|
| `neutral` | 0 | Sin señal de peligro |
| `grooming_confianza` | 1 | Construcción de confianza mediante halagos |
| `grooming_datos_personales` | 2 | Solicitud de datos identificables |
| `grooming_encuentro` | 3 | Propuesta de encuentro físico |
| `grooming_secreto` | 3 | Petición de secreto / aislamiento parental |
| `ciberacoso` | 2 | Insultos, humillaciones, difusión de rumores |
| `reto_viral` | 2 | Presión para realizar retos peligrosos |

Cada categoría incluye:
- `palabras_clave_ml`: términos que el modelo Scikit-learn usa para clasificación
- `tacticas_manipulacion`: tácticas asociadas a esa categoría

---

## 3. Modelos Django

Ubicación: `Backend/backend/api/models.py`

### `PreguntaBanco`

```python
class PreguntaBanco(models.Model):
    pregunta_id           # CharField unique — clave de negocio
    hdu                   # "HDU-2" | "HDU-8"
    zona                  # "desconocidos" | "chat_simulado"

    # HDU-2
    npc_id                # "NPC_01" | "NPC_02" | ""
    npc_nombre
    npc_avatar
    fase                  # PositiveSmallIntegerField | null
    orden_en_fase         # PositiveSmallIntegerField | null
    narrativa_continuacion

    # HDU-8
    escenario_id
    escenario_nombre
    historial_previo      # JSONField

    # Comunes
    categoria
    nivel_riesgo          # 0–3
    es_mensaje_riesgo     # bool
    es_fin_de_npc         # bool — True en FIN_SEGURO / FIN_INSEGURO
    es_fin_de_zona        # bool — True en ZONA_FIN
    mensaje_npc
    etiquetas_ml          # JSONField
```

### `OpcionBanco`

```python
class OpcionBanco(models.Model):
    pregunta              # FK → PreguntaBanco (related_name="opciones")
    opcion_id             # CharField unique
    texto
    tipo                  # "insegura" | "segura_basica" | "segura_optima"
    consecuencia_narrativa
    impacto_puntuacion    # SmallIntegerField: -1, 1, 2
    siguiente_pregunta    # CharField | null
    orden                 # PositiveSmallIntegerField
```

---

## 4. Migraciones

| Archivo | Contenido |
|---------|-----------|
| `0001_initial.py` | Modelos base del proyecto (Usuario, Partida, NPC, Chat, Mensaje, etc.) |
| `0002_chat_fecha_termino_nivelriesgo_puntaje_and_more.py` | Campos adicionales de la primera iteración |
| `0003_banco_preguntas.py` | **[Luis]** Crea tablas `PreguntaBanco` y `OpcionBanco` |
| `0004_preguntabanco_fin_flags.py` | **[Luis]** Añade `es_fin_de_npc` y `es_fin_de_zona` |

Para aplicar:
```bash
python manage.py migrate
```

---

## 5. Comando de carga

Ubicación: `Backend/backend/api/management/commands/cargar_banco.py`

```bash
# Carga normal (primera vez)
python manage.py cargar_banco

# Carga con ruta explícita
python manage.py cargar_banco --archivo /ruta/al/banco_preguntas.json

# Recarga completa (borra y vuelve a insertar todo)
python manage.py cargar_banco --limpiar
```

**Ruta por defecto:** `BASE_DIR.parent.parent / "banco_preguntas" / "banco_preguntas.json"`  
= `Fishy/banco_preguntas/banco_preguntas.json` relativo al repo.

**Comportamiento:**
- Usa `update_or_create` con `pregunta_id` como clave → idempotente.
- Borra y recrea las `OpcionBanco` de cada pregunta en cada ejecución.
- Imprime un resumen: preguntas creadas / actualizadas / opciones cargadas.

---

## 6. Endpoints API

Base URL: `/api/`  
Autenticación: Token (`Authorization: Token <token>`)

### `GET /api/banco/preguntas/`

Lista preguntas del banco. Todos los filtros son opcionales y combinables.

| Query param | Tipo | Ejemplo | Descripción |
|-------------|------|---------|-------------|
| `zona` | string | `?zona=desconocidos` | Filtra por zona |
| `npc_id` | string | `?npc_id=NPC_01` | Filtra por NPC |
| `escenario_id` | string | `?escenario_id=CHAT_GROOMING_01` | Filtra por escenario HDU-8 |
| `hdu` | string | `?hdu=HDU-2` | Filtra por HDU |
| `fase` | int | `?fase=2` | Filtra por fase (HDU-2) |
| `solo_riesgo` | bool | `?solo_riesgo=true` | Solo preguntas con opciones |
| `fin_de_npc` | bool | `?fin_de_npc=true` | Solo estados FIN_SEGURO / FIN_INSEGURO |
| `fin_de_zona` | bool | `?fin_de_zona=true` | Solo ZONA_FIN |

**Respuesta 200:**
```json
[
  {
    "id": 1,
    "pregunta_id": "HDU2_NPC01_F2_Q01",
    "hdu": "HDU-2",
    "zona": "desconocidos",
    "npc_id": "NPC_01",
    "categoria": "grooming_datos_personales",
    "nivel_riesgo": 2,
    "es_mensaje_riesgo": true,
    "es_fin_de_npc": false,
    "es_fin_de_zona": false,
    "mensaje_npc": "Para entrar a mi grupo necesito saber...",
    "opciones": [
      {
        "opcion_id": "HDU2_NPC01_F2_Q01_R1",
        "texto": "Le doy mi nombre completo...",
        "tipo": "insegura",
        "impacto_puntuacion": -1,
        "siguiente_pregunta": "HDU2_NPC01_F2_Q02"
      },
      ...
    ]
  }
]
```

### `GET /api/banco/preguntas/<pregunta_id>/`

Retorna una pregunta específica por su `pregunta_id`.

```bash
GET /api/banco/preguntas/HDU2_NPC01_F2_Q01/
```

---

## 7. Integración Unity

Ubicación: `Assets/Scripts/ApiManager.cs`  
Namespace: `Fishy.Net`

### DTOs nuevos

```csharp
public class OpcionBancoDto
{
    public int id;
    public string opcion_id;
    public string texto;
    public string tipo;               // insegura | segura_basica | segura_optima
    public string consecuencia_narrativa;
    public int impacto_puntuacion;    // -1, 1 o 2
    public string siguiente_pregunta; // null si es final de rama
    public int orden;
}

public class PreguntaDto
{
    public string pregunta_id;
    public string hdu;
    public string zona;
    public string npc_id;
    public string npc_nombre;
    public string npc_avatar;
    public int? fase;
    public int? orden_en_fase;
    public string narrativa_continuacion;
    public string escenario_id;
    public string escenario_nombre;
    public bool es_mensaje_riesgo;
    public bool es_fin_de_npc;
    public bool es_fin_de_zona;
    public string mensaje_npc;
    public List<OpcionBancoDto> opciones;
}
```

### Métodos nuevos en `ApiManager`

```csharp
// Cargar lista filtrada de preguntas
ApiManager.Instance.ObtenerPreguntas(
    zona: "desconocidos",
    npcId: "NPC_01",
    fase: 1,
    onSuccess: preguntas => { /* iterar preguntas */ },
    onError:   err => Debug.LogError(err)
);

// Cargar una pregunta por ID (útil para navegación por siguiente_pregunta)
ApiManager.Instance.ObtenerPregunta(
    preguntaId: "HDU2_NPC01_F2_Q01",
    onSuccess: pregunta => { /* mostrar mensaje y opciones */ },
    onError:   err => Debug.LogError(err)
);
```

### Flujo típico de una conversación HDU-2 en Unity (Benjamín)

```
1. ObtenerPreguntas(zona:"desconocidos", npcId:"NPC_01", fase:1)
   → Mostrar mensajes neutros secuencialmente (narrativa_continuacion)

2. Al llegar a es_mensaje_riesgo=true:
   → Mostrar mensaje_npc + botones con opciones[i].texto

3. Al elegir opción:
   → Registrar impacto_puntuacion en sesión
   → ObtenerPregunta(siguiente_pregunta) para continuar el flujo

4. Al llegar a es_fin_de_npc=true:
   → Marcar NPC como completado en sesión local

5. Cuando ambos NPCs tienen es_fin_de_npc=true:
   → ObtenerPreguntas(zona:"desconocidos", fin_de_zona:true)
   → Mostrar ZONA_FIN y desbloquear siguiente zona
```

---

## 8. Cómo ejecutar

### Primera vez (desarrollo local)

```bash
# 1. Entrar al backend
cd Fishy/Backend/backend

# 2. Instalar dependencias
pip install -r requirements.txt

# 3. Aplicar migraciones
python manage.py migrate

# 4. Cargar el banco de preguntas
python manage.py cargar_banco

# 5. Levantar el servidor
python manage.py runserver
```

### Verificar que el banco está cargado

```bash
curl -H "Authorization: Token <tu_token>" \
  http://127.0.0.1:8000/api/banco/preguntas/?zona=desconocidos
```

### Recargar el banco tras cambios en el JSON

```bash
python manage.py cargar_banco --limpiar
```

### Endpoints disponibles para pruebas rápidas

```
GET /api/health/                                     → OK
GET /api/banco/preguntas/                            → todas las preguntas
GET /api/banco/preguntas/?zona=desconocidos          → zona HDU-2
GET /api/banco/preguntas/?npc_id=NPC_01              → solo Alex
GET /api/banco/preguntas/?fin_de_zona=true           → solo ZONA_FIN
GET /api/banco/preguntas/HDU2_NPC01_F2_Q01/          → una pregunta específica
```
