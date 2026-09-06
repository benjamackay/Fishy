import logging

from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from rest_framework import status
from rest_framework.authtoken.models import Token
from django.contrib.auth import authenticate
from django.shortcuts import get_object_or_404
from django.db import transaction
from django.db.models import Count
from django.utils import timezone

from .models import (
    UsuarioJugador, NivelRiesgo, Partida, NPC, Chat, Mensaje,
    PosibleRespuesta, PreguntaBanco, OpcionBanco,
    CasoDetective, CasoDetectiveProgreso,
    DialogoNPC, Mision, MisionProgreso, ZonaProgreso, ItemInventario,
)
from .serializers import (
    RegistroSerializer, AdultoResponsableSerializer, UsuarioJugadorSerializer,
    NivelRiesgoSerializer, PartidaSerializer,
    NPCSerializer, ChatSerializer, MensajeSerializer,
    PreguntaBancoSerializer,
    CasoDetectiveSerializer, CasoDetectiveProgresoSerializer,
    DialogoNPCSerializer, MisionProgresoSerializer, ZonaProgresoSerializer,
    ItemInventarioSerializer,
)

logger = logging.getLogger(__name__)


# ── Health ────────────────────────────────────────────────────────────────────

@api_view(["GET"])
@permission_classes([AllowAny])
def health_check(request):
    return Response({"status": "ok"})


# ── Auth ──────────────────────────────────────────────────────────────────────

@api_view(["POST"])
@permission_classes([AllowAny])
def registro(request):
    """Crea la cuenta del adulto responsable. Body: nombre, email, password
    (+ apellido, edad, fecha_nacimiento opcionales)."""
    serializer = RegistroSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    adulto = serializer.save()
    token, _ = Token.objects.get_or_create(user=adulto)
    return Response({"token": token.key, "adulto_id": adulto.pk}, status=status.HTTP_201_CREATED)


@api_view(["POST"])
@permission_classes([AllowAny])
def auth_login(request):
    nombre = request.data.get("nombre")
    password = request.data.get("password")
    adulto = authenticate(request, username=nombre, password=password)
    if adulto is None:
        return Response({"error": "Credenciales inválidas"}, status=status.HTTP_401_UNAUTHORIZED)
    token, _ = Token.objects.get_or_create(user=adulto)
    return Response({"token": token.key, "adulto_id": adulto.pk})


@api_view(["GET"])
def perfil_adulto(request):
    """Datos de la cuenta autenticada."""
    return Response(AdultoResponsableSerializer(request.user).data)


# ── Perfiles de menores (control parental) ────────────────────────────────────

@api_view(["GET", "POST"])
def jugadores(request):
    """GET: perfiles del adulto autenticado. POST: crea uno. Body: nombre (+ edad)."""
    if request.method == "GET":
        qs = request.user.jugadores.all()
        return Response(UsuarioJugadorSerializer(qs, many=True).data)
    serializer = UsuarioJugadorSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    # `adulto` es read_only, así que DRF no puede validar solo la restricción
    # de unicidad (adulto, nombre): sin esto el duplicado explota como 500.
    if request.user.jugadores.filter(nombre=serializer.validated_data["nombre"]).exists():
        return Response(
            {"nombre": ["Ya tienes un perfil con ese nombre."]},
            status=status.HTTP_400_BAD_REQUEST,
        )
    jugador = serializer.save(adulto=request.user)
    return Response(UsuarioJugadorSerializer(jugador).data, status=status.HTTP_201_CREATED)


@api_view(["GET"])
def partidas_jugador(request, jugador_id):
    """Partidas de un perfil de menor, de la jugada más reciente a la más antigua.

    Es lo que permite **retomar** el avance: cada menor conserva su propia
    partida entre sesiones. El cliente elige el perfil, pide esta lista, y si
    viene algo continúa con la primera; si viene vacía, crea una con
    `POST /partidas/`.
    """
    jugador = get_object_or_404(UsuarioJugador, pk=jugador_id, adulto=request.user)
    partidas = jugador.partidas.order_by("-fecha_update")
    return Response(PartidaSerializer(partidas, many=True).data)


@api_view(["GET", "PATCH", "DELETE"])
def jugador_detalle(request, jugador_id):
    jugador = get_object_or_404(UsuarioJugador, pk=jugador_id, adulto=request.user)
    if request.method == "GET":
        return Response(UsuarioJugadorSerializer(jugador).data)
    if request.method == "DELETE":
        jugador.delete()   # arrastra sus partidas en cascada
        return Response(status=status.HTTP_204_NO_CONTENT)
    serializer = UsuarioJugadorSerializer(jugador, data=request.data, partial=True)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    nombre = serializer.validated_data.get("nombre")
    if nombre and request.user.jugadores.filter(nombre=nombre).exclude(pk=jugador.pk).exists():
        return Response(
            {"nombre": ["Ya tienes un perfil con ese nombre."]},
            status=status.HTTP_400_BAD_REQUEST,
        )
    serializer.save()
    return Response(serializer.data)


# ── Catálogos ─────────────────────────────────────────────────────────────────

@api_view(["GET"])
def niveles_riesgo(request):
    return Response(NivelRiesgoSerializer(NivelRiesgo.objects.all(), many=True).data)


# ── Partida (HDU-2) ───────────────────────────────────────────────────────────

# La zona donde empieza el juego. No es contenido del banco sino una regla de la
# partida: Otto parte en el Bosque de los Desconocidos y esa zona nunca está
# oscurecida, así que tiene que existir en la BD desde el minuto cero. Sin esto,
# una partida recién creada aparecía con cero zonas desbloqueadas — que es lo que
# se lee como "el mapa está todo cerrado" — hasta que el niño abría la zona 2.
ZONA_INICIAL = "desconocidos"


@api_view(["POST"])
def crear_partida(request):
    """Body: { "usuario_jugador_id": int, "nivel_riesgo": int (opcional) }.
    El perfil debe pertenecer al adulto autenticado.

    La partida nace con `ZONA_INICIAL` ya desbloqueada (HDU-3 CA5 / HDU-4 CA5: el
    mapa se abre por zonas, y la primera viene abierta de fábrica)."""
    jugador = get_object_or_404(
        UsuarioJugador,
        pk=request.data.get("usuario_jugador_id"),
        adulto=request.user,
    )
    serializer = PartidaSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    partida = serializer.save(usuario_jugador=jugador)
    ZonaProgreso.objects.get_or_create(partida=partida, zona=ZONA_INICIAL)
    return Response(PartidaSerializer(partida).data, status=status.HTTP_201_CREATED)


@api_view(["GET", "PATCH"])
def partida_detalle(request, partida_id):
    partida = get_object_or_404(Partida, pk=partida_id, usuario_jugador__adulto=request.user)
    if request.method == "GET":
        return Response(PartidaSerializer(partida).data)
    serializer = PartidaSerializer(partida, data=request.data, partial=True)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    serializer.save()
    return Response(serializer.data)


# ── NPC (HDU-2) ───────────────────────────────────────────────────────────────

@api_view(["GET", "POST"])
def npcs_partida(request, partida_id):
    partida = get_object_or_404(Partida, pk=partida_id, usuario_jugador__adulto=request.user)
    if request.method == "GET":
        return Response(NPCSerializer(partida.npcs.all(), many=True).data)
    serializer = NPCSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    npc = serializer.save(partida=partida)
    return Response(NPCSerializer(npc).data, status=status.HTTP_201_CREATED)


@api_view(["PATCH"])
def npc_actualizar(request, npc_id):
    npc = get_object_or_404(NPC, pk=npc_id, partida__usuario_jugador__adulto=request.user)
    serializer = NPCSerializer(npc, data=request.data, partial=True)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    serializer.save()
    return Response(serializer.data)


# ── Chat (HDU-8) ──────────────────────────────────────────────────────────────

@api_view(["POST"])
def iniciar_chat(request):
    """Body: { "partida_id": int, "npc_id": int, "categoria_riesgo": str (opcional) }"""
    partida = get_object_or_404(
        Partida, pk=request.data.get("partida_id"), usuario_jugador__adulto=request.user
    )
    npc = get_object_or_404(NPC, pk=request.data.get("npc_id"), partida=partida)
    chat = Chat.objects.create(
        partida=partida,
        npc=npc,
        categoria_riesgo=request.data.get("categoria_riesgo", ""),
    )
    return Response(ChatSerializer(chat).data, status=status.HTTP_201_CREATED)


@api_view(["GET"])
def mensajes_chat(request, chat_id):
    """Retorna el historial completo del chat con posibles respuestas anidadas."""
    chat = get_object_or_404(Chat, pk=chat_id, partida__usuario_jugador__adulto=request.user)
    mensajes = chat.mensajes.prefetch_related("posibles_respuestas").all()
    return Response(MensajeSerializer(mensajes, many=True).data)


@api_view(["POST"])
def registrar_mensaje(request, chat_id):
    """
    Body: {
        "tipo": "start"|"chain"|"request",
        "respuesta": str,
        "calidad_respuesta": "buena"|"neutral"|"mala" (solo si tipo=request),
        "pregunta_banco_id": str  (opcional, ej: "HDU2_NPC01_F2_Q01"),
        "opcion_banco_id": str    (opcional, ej: "HDU2_NPC01_F2_Q01_R2"),
        "posibles_respuestas": [{"texto": str, "orden": int, "calidad_respuesta": str}]  (opcional)
    }

    `opcion_banco_id` identifica la opción exacta que eligió el jugador. Es lo que
    permite acumular riesgo por zona con el puntaje real del banco (-1 / +1 / +2)
    en vez de deducirlo de `calidad_respuesta`, que no distingue una respuesta
    segura básica de una óptima. Ver `riesgo_por_zona`.
    """
    chat = get_object_or_404(Chat, pk=chat_id, partida__usuario_jugador__adulto=request.user)
    if chat.fecha_termino is not None:
        return Response({"error": "El chat ya finalizó"}, status=status.HTTP_400_BAD_REQUEST)

    serializer = MensajeSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)

    # pregunta_banco_id / opcion_banco_id opcionales: vinculan la respuesta con el banco
    pregunta_banco_id = request.data.get("pregunta_banco_id") or None
    opcion_banco_id   = request.data.get("opcion_banco_id") or None
    mensaje = serializer.save(
        chat=chat,
        pregunta_banco_id=pregunta_banco_id,
        opcion_banco_id=opcion_banco_id,
    )

    for i, opcion in enumerate(request.data.get("posibles_respuestas", [])):
        PosibleRespuesta.objects.create(
            mensaje=mensaje,
            texto=opcion.get("texto", ""),
            orden=opcion.get("orden", i),
            calidad_respuesta=opcion.get("calidad_respuesta", ""),
        )

    return Response(MensajeSerializer(mensaje).data, status=status.HTTP_201_CREATED)


# ── Banco de Preguntas (HDU-2 / HDU-8) ───────────────────────────────────────

def _filtros_banco(qs, params):
    """Aplica los filtros opcionales comunes a un queryset de PreguntaBanco.

    Compartido por /banco/preguntas/ y /banco/zonas/<zona>/preguntas/ para que
    ambos acepten exactamente los mismos filtros.
    """
    for param, campo in [
        ("zona", "zona"),
        ("npc_id", "npc_id"),
        ("escenario_id", "escenario_id"),
        ("hdu", "hdu"),
    ]:
        val = params.get(param)
        if val:
            qs = qs.filter(**{campo: val})
    fase = params.get("fase")
    if fase is not None:
        qs = qs.filter(fase=int(fase))
    if params.get("solo_riesgo", "").lower() == "true":
        qs = qs.filter(es_mensaje_riesgo=True)
    if params.get("fin_de_zona", "").lower() == "true":
        qs = qs.filter(es_fin_de_zona=True)
    if params.get("fin_de_npc", "").lower() == "true":
        qs = qs.filter(es_fin_de_npc=True)
    return qs


@api_view(["GET"])
def preguntas_banco(request):
    """
    Devuelve preguntas del banco filtradas por query params opcionales:
      ?zona=desconocidos
      ?npc_id=NPC_01
      ?fase=1
      ?escenario_id=CHAT_GROOMING_01
      ?hdu=HDU-2
      ?solo_riesgo=true   (solo mensajes que requieren respuesta del jugador)
    """
    qs = _filtros_banco(PreguntaBanco.objects.prefetch_related("opciones").all(),
                        request.query_params)
    return Response(PreguntaBancoSerializer(qs, many=True).data)


@api_view(["GET"])
def zonas_banco(request):
    """
    Catalogo de zonas que existen en el banco, con cuantas preguntas tiene cada
    una. Se arma desde la BD y no desde una lista fija: al cargar un banco con
    una zona nueva, aparece aqui sola, sin tocar codigo.
    """
    filas = (PreguntaBanco.objects
             .values("zona", "hdu")
             .annotate(n=Count("id"))
             .order_by("zona", "hdu"))
    zonas = {}
    for f in filas:
        z = zonas.setdefault(f["zona"], {"zona": f["zona"], "preguntas": 0, "hdus": []})
        z["preguntas"] += f["n"]
        if f["hdu"] and f["hdu"] not in z["hdus"]:
            z["hdus"].append(f["hdu"])
    salida = []
    for z in zonas.values():
        # Hoy cada zona corresponde a una sola HDU; si alguna llegara a cubrir
        # varias, se listan todas separadas por coma en vez de perder el dato.
        salida.append({"zona": z["zona"],
                       "preguntas": z["preguntas"],
                       "hdu": ", ".join(z["hdus"])})
    return Response(sorted(salida, key=lambda z: z["zona"]))


@api_view(["GET"])
def preguntas_zona(request, zona):
    """
    Preguntas de una zona concreta. Acepta los mismos filtros que
    /banco/preguntas/, salvo ?zona=, que lo manda la ruta.

    Devuelve 404 si la zona no existe en el banco, para poder distinguirla de
    una zona real que todavia no tiene preguntas cargadas.
    """
    if not PreguntaBanco.objects.filter(zona=zona).exists():
        return Response({"detail": f"La zona '{zona}' no existe."},
                        status=status.HTTP_404_NOT_FOUND)
    params = request.query_params.copy()
    params.pop("zona", None)
    qs = _filtros_banco(
        PreguntaBanco.objects.prefetch_related("opciones").filter(zona=zona),
        params,
    )
    return Response(PreguntaBancoSerializer(qs, many=True).data)


@api_view(["GET"])
def pregunta_detalle(request, pregunta_id):
    """Devuelve una pregunta concreta por su pregunta_id (ej: HDU2_NPC01_F2_Q01)."""
    pregunta = get_object_or_404(PreguntaBanco, pregunta_id=pregunta_id)
    return Response(PreguntaBancoSerializer(pregunta).data)


@api_view(["POST"])
def finalizar_chat(request, chat_id):
    """Cierra el chat registrando un mensaje END. Body (opcional): { "respuesta": str }"""
    chat = get_object_or_404(Chat, pk=chat_id, partida__usuario_jugador__adulto=request.user)
    if chat.fecha_termino is not None:
        return Response({"error": "El chat ya finalizó"}, status=status.HTTP_400_BAD_REQUEST)
    mensaje_end = Mensaje.objects.create(
        chat=chat,
        tipo=Mensaje.Tipo.END,
        respuesta=request.data.get("respuesta", ""),
        calidad_respuesta="",
    )
    # El Mensaje.save() automáticamente actualiza Chat.fecha_termino
    return Response(MensajeSerializer(mensaje_end).data)


# ── Riesgo acumulado por zona (HDU-2 / HDU-8) ────────────────────────────────

@api_view(["GET"])
def riesgo_por_zona(request, partida_id):
    """
    Riesgo acumulado de una partida, agrupado por zona del banco de preguntas.

    Suma el `impacto_puntuacion` de cada opción que el jugador eligió, agrupado por
    la `zona` de la pregunta a la que pertenece esa opción. El puntaje sale del
    banco, no de `calidad_respuesta`: `insegura=-1`, `segura_basica=+1`,
    `segura_optima=+2`.

    **Signo: más alto = más seguro.** Un total negativo significa que el menor
    eligió mayoritariamente respuestas inseguras.

    Solo cuentan los mensajes que traen `opcion_banco_id` y cuyo id existe en el
    banco. Los flujos que no reportan la opción elegida (p. ej. el módulo de
    diálogo antiguo de Desconocidos, con nodos escritos a mano) quedan fuera del
    cálculo a propósito, en vez de contribuir con datos inventados.

    Respuesta:
    {
      "partida_id": 1,
      "zonas": [
        {"zona": "desconocidos", "riesgo_acumulado": 3, "respuestas": 4,
         "minimo_posible": -4, "maximo_posible": 8}
      ],
      "total": 3,
      "respuestas": 4,
      "sin_clasificar": 0
    }

    `minimo_posible` / `maximo_posible` son las cotas de esas mismas preguntas si
    el menor hubiera elegido siempre la peor / la mejor opción. Sirven para
    mostrar el resultado como una escala en vez de un número suelto.
    """
    partida = get_object_or_404(
        Partida, pk=partida_id, usuario_jugador__adulto=request.user
    )

    elegidos = list(
        Mensaje.objects
        .filter(chat__partida=partida)
        .exclude(opcion_banco_id__isnull=True)
        .exclude(opcion_banco_id="")
        .values_list("opcion_banco_id", flat=True)
    )

    # Una sola consulta al banco para resolver todas las opciones elegidas.
    opciones = {
        o.opcion_id: o
        for o in OpcionBanco.objects
        .filter(opcion_id__in=set(elegidos))
        .select_related("pregunta")
        .prefetch_related("pregunta__opciones")
    }

    zonas = {}
    contadas = 0
    for opcion_id in elegidos:
        opcion = opciones.get(opcion_id)
        if opcion is None:
            continue  # id que no existe en el banco (contenido viejo o typo)
        contadas += 1
        zona = zonas.setdefault(
            opcion.pregunta.zona,
            {"zona": opcion.pregunta.zona, "riesgo_acumulado": 0, "respuestas": 0,
             "minimo_posible": 0, "maximo_posible": 0},
        )
        zona["riesgo_acumulado"] += opcion.impacto_puntuacion
        zona["respuestas"] += 1

        # Cotas: peor y mejor opción de la pregunta que originó esta respuesta.
        impactos = [o.impacto_puntuacion for o in opcion.pregunta.opciones.all()]
        if impactos:
            zona["minimo_posible"] += min(impactos)
            zona["maximo_posible"] += max(impactos)

    return Response({
        "partida_id": partida.id,
        "zonas": sorted(zonas.values(), key=lambda z: z["zona"]),
        "total": sum(z["riesgo_acumulado"] for z in zonas.values()),
        "respuestas": contadas,
        "sin_clasificar": len(elegidos) - contadas,
    })


@api_view(["GET"])
def oportunidades_mejora(request, partida_id):
    """
    Decisiones inseguras del menor, con la alternativa que se le escapó.

    Es el "registro como oportunidad de mejora" que piden los criterios de
    aceptación de las zonas de riesgo (p. ej. HDU-3 CA3: el menor responde a un
    mensaje de ciberacoso con otra burla). No hay un campo que las marque: una
    oportunidad de mejora **es** un `Mensaje` cuyo `opcion_banco_id` resuelve a
    una `OpcionBanco` de tipo `insegura`. Se deriva en vez de guardarse aparte
    para que no pueda quedar desincronizada del banco: si mañana una opción deja
    de ser insegura, esta lista lo refleja sola.

    Solo cuenta `insegura`. Una `segura_basica` no es un error: es una respuesta
    correcta pero mejorable, y mezclarlas le quitaría sentido a la lista.

    Está pensado para el reporte del adulto responsable, no para mostrárselo al
    menor: etiquetarle la pantalla con sus errores lo señala y rompe el tono del
    juego, que corrige por consecuencia narrativa.

    Filtro opcional:  ?zona=ciberacoso

    Respuesta:
    {
      "partida_id": 42,
      "jugador": "Perfil 2",
      "oportunidades": [
        {"fecha": "...", "zona": "ciberacoso", "npc": "Flamenco",
         "pregunta_banco_id": "HDU3_NPC03_Q01", "mensaje_npc": "...",
         "eligio":       {"opcion_banco_id": "...", "texto": "...",
                          "impacto_puntuacion": -1, "consecuencia": "..."},
         "mejor_opcion": {"opcion_banco_id": "...", "texto": "...",
                          "impacto_puntuacion": 2,  "consecuencia": "..."},
         "puntos_perdidos": 3}
      ],
      "total": 1,
      "por_zona": [{"zona": "ciberacoso", "oportunidades": 1}]
    }

    `puntos_perdidos` es la distancia contra la mejor opción de esa misma
    pregunta. Sirve para ordenar por gravedad: no es lo mismo fallar donde la
    alternativa era reportar (+2) que donde era apartarse (+1).
    """
    partida = get_object_or_404(
        Partida, pk=partida_id, usuario_jugador__adulto=request.user
    )

    mensajes = list(
        Mensaje.objects
        .filter(chat__partida=partida)
        .exclude(opcion_banco_id__isnull=True)
        .exclude(opcion_banco_id="")
        .select_related("chat__npc")
        .order_by("timestamp")
    )

    # Una sola consulta al banco para resolver todas las opciones elegidas.
    opciones = {
        o.opcion_id: o
        for o in OpcionBanco.objects
        .filter(opcion_id__in={m.opcion_banco_id for m in mensajes})
        .select_related("pregunta")
        .prefetch_related("pregunta__opciones")
    }

    zona_filtro = request.query_params.get("zona")
    oportunidades = []
    por_zona = {}

    for mensaje in mensajes:
        elegida = opciones.get(mensaje.opcion_banco_id)
        if elegida is None or elegida.tipo != "insegura":
            continue
        pregunta = elegida.pregunta
        if zona_filtro and pregunta.zona != zona_filtro:
            continue

        # La mejor alternativa que tenía disponible en esa misma pregunta.
        mejor = max(
            pregunta.opciones.all(),
            key=lambda o: o.impacto_puntuacion,
            default=None,
        )

        oportunidades.append({
            "fecha": mensaje.timestamp,
            "zona": pregunta.zona,
            "categoria": pregunta.categoria,
            "npc": mensaje.chat.npc.nombre,
            "chat_id": mensaje.chat_id,
            "pregunta_banco_id": pregunta.pregunta_id,
            "mensaje_npc": pregunta.mensaje_npc,
            "eligio": {
                "opcion_banco_id": elegida.opcion_id,
                "texto": elegida.texto,
                "impacto_puntuacion": elegida.impacto_puntuacion,
                "consecuencia": elegida.consecuencia_narrativa,
            },
            "mejor_opcion": None if mejor is None else {
                "opcion_banco_id": mejor.opcion_id,
                "texto": mejor.texto,
                "impacto_puntuacion": mejor.impacto_puntuacion,
                "consecuencia": mejor.consecuencia_narrativa,
            },
            "puntos_perdidos": (
                0 if mejor is None
                else mejor.impacto_puntuacion - elegida.impacto_puntuacion
            ),
        })
        por_zona[pregunta.zona] = por_zona.get(pregunta.zona, 0) + 1

    return Response({
        "partida_id": partida.id,
        "jugador": partida.usuario_jugador.nombre,
        "oportunidades": oportunidades,
        "total": len(oportunidades),
        "por_zona": [
            {"zona": z, "oportunidades": n} for z, n in sorted(por_zona.items())
        ],
    })


# ── Modo Detective (HDU-10) ───────────────────────────────────────────────────

@api_view(["GET"])
def casos_detective(request):
    """
    Lista los casos del modo Detective, con sus mensajes anidados (mismo patrón
    que /banco/preguntas/ con sus opciones). Filtro opcional:
      ?zona=playa
    """
    qs = CasoDetective.objects.prefetch_related("mensajes").all()
    zona = request.query_params.get("zona")
    if zona:
        qs = qs.filter(zona=zona)
    return Response(CasoDetectiveSerializer(qs, many=True).data)


@api_view(["GET"])
def caso_detective_detalle(request, caso_id):
    """Un caso concreto por su caso_id (ej: caso_01)."""
    caso = get_object_or_404(
        CasoDetective.objects.prefetch_related("mensajes"), caso_id=caso_id
    )
    return Response(CasoDetectiveSerializer(caso).data)


# ── Diálogos de NPCs neutros (HDU-1) ───────────────────────────────────────────

@api_view(["GET"])
def dialogos_npc(request):
    """
    Lista los diálogos de NPCs neutros (sin árbol de decisiones). Filtro opcional:
      ?zona=playa
    """
    qs = DialogoNPC.objects.select_related("mision").all()
    zona = request.query_params.get("zona")
    if zona:
        qs = qs.filter(zona=zona)
    return Response(DialogoNPCSerializer(qs, many=True).data)


@api_view(["GET"])
def dialogo_npc_detalle(request, dialogo_id):
    """Un diálogo concreto por su dialogo_id (ej: NPC_FLAMENCO_SEC)."""
    dialogo = get_object_or_404(
        DialogoNPC.objects.select_related("mision"), dialogo_id=dialogo_id
    )
    return Response(DialogoNPCSerializer(dialogo).data)


def _corregir_caso(caso, mensajes_marcados):
    """Corrige un intento del Modo Detective contra las señales del propio caso.

    Es la misma fórmula que `DetectiveCaseManager` aplica en Unity (HDU-10 CA4 y
    CA5): solo cuentan los mensajes de riesgo **no ambiguos**, y un ambiguo que el
    jugador marcó no suma ni resta. Se calcula acá y no se copia lo que mandó el
    cliente porque este resultado alimenta el reporte del adulto (HDU-13): un
    cliente con un bug, una versión vieja del juego o una petición a mano dejarían
    números que después nadie puede auditar. Las marcas sí vienen del cliente —
    son lo que el niño/a hizo, no algo que el servidor pueda deducir.

    Devuelve (aciertos, total_riesgo, porcentaje).
    """
    marcados = set(mensajes_marcados or [])
    riesgo_real = [
        m.mensaje_id for m in caso.mensajes.all()
        if m.es_senal_riesgo and not m.es_ambiguo
    ]
    total = len(riesgo_real)
    aciertos = sum(1 for mid in riesgo_real if mid in marcados)
    # Sin señales de riesgo el caso está 100% resuelto por definición: dividir
    # daría ZeroDivision y un 0% castigaría al jugador por un caso sin trampa.
    porcentaje = (aciertos / total) if total else 1.0
    return aciertos, total, porcentaje


@api_view(["POST"])
def registrar_progreso_detective(request, caso_id):
    """
    Registra el resultado de un intento del jugador sobre un caso.

    Body: {
        "partida_id": int,
        "mensajes_marcados": [str],   # mensaje_id que el jugador marcó como riesgo
    }

    `aciertos`, `total_riesgo` y `porcentaje` **los calcula el servidor** a partir
    de las marcas y de las señales del caso (ver `_corregir_caso`). Si el cuerpo
    los trae igual, se usan solo para comparar: una diferencia se avisa por log,
    porque significa que el cliente y el banco no están viendo el mismo caso.

    Se guardan en vez de derivarse en cada lectura porque un intento es un hecho
    con fecha: si mañana se corrige el contenido del caso, el resultado de esa
    tarde no debería cambiar solo.

    Un mismo (partida, caso) tiene una sola fila (constraint del modelo):
    reintentar no crea una fila nueva, suma 1 a `intentos` y sobrescribe el
    resultado con el del último intento — mismo patrón idempotente que
    MissionManager.CompletarDesafio en Unity.
    """
    caso = get_object_or_404(
        CasoDetective.objects.prefetch_related("mensajes"), caso_id=caso_id
    )
    partida = get_object_or_404(
        Partida, pk=request.data.get("partida_id"), usuario_jugador__adulto=request.user
    )

    marcados = request.data.get("mensajes_marcados", [])
    aciertos, total, porcentaje = _corregir_caso(caso, marcados)

    enviados = request.data.get("aciertos")
    if enviados is not None and enviados != aciertos:
        logger.warning(
            "Detective %s: el cliente reportó %s aciertos y el caso da %s. Se guarda "
            "el del servidor. Revisa si Unity y el banco tienen la misma versión del caso.",
            caso.caso_id, enviados, aciertos,
        )

    progreso, created = CasoDetectiveProgreso.objects.get_or_create(
        partida=partida, caso=caso,
        defaults={
            "mensajes_marcados": marcados,
            "aciertos": aciertos,
            "total_riesgo": total,
            "porcentaje": porcentaje,
        },
    )
    if not created:
        progreso.mensajes_marcados = marcados
        progreso.aciertos = aciertos
        progreso.total_riesgo = total
        progreso.porcentaje = porcentaje
        progreso.intentos += 1
    progreso.fecha_termino = timezone.now()
    progreso.save()

    return Response(
        CasoDetectiveProgresoSerializer(progreso).data,
        status=status.HTTP_201_CREATED if created else status.HTTP_200_OK,
    )


@api_view(["GET"])
def progreso_detective_partida(request, partida_id):
    """Progreso de todos los casos Detective jugados en una partida (para que
    Unity sepa cuáles ya están completados, igual que hace localmente
    MissionManager con PlayerPrefs)."""
    partida = get_object_or_404(
        Partida, pk=partida_id, usuario_jugador__adulto=request.user
    )
    progresos = partida.casos_detective.select_related("caso").all()
    return Response(CasoDetectiveProgresoSerializer(progresos, many=True).data)


# ── Progreso de misiones (HDU-1 CA4 y CA5) ────────────────────────────────────

@api_view(["GET", "POST"])
def misiones_partida(request, partida_id):
    """
    GET  — misiones que esta partida tiene desbloqueadas, con su estado.
    POST — registra o actualiza una. Body:
           { "mision_id": "MISION_NPC_01", "estado": "disponible" | "completada" }

    Es idempotente, igual que `MissionManager.CompletarDesafio` en Unity: repetir
    el POST no duplica la fila ni mueve `fecha_completada`. **Una mision
    completada no vuelve a disponible**: un POST con `disponible` sobre una ya
    completada se ignora, porque el orden en que llegan los mensajes desde el
    juego no esta garantizado y el registro para el adulto no puede retroceder.

    Un `mision_id` que no esta en el catalogo se guarda igual (el progreso del
    nino no depende de que el banco este al dia) pero se avisa por log y la
    respuesta lo marca con `en_catalogo: false`.
    """
    partida = get_object_or_404(
        Partida, pk=partida_id, usuario_jugador__adulto=request.user
    )

    if request.method == "POST":
        mision_id = (request.data.get("mision_id") or "").strip()
        if not mision_id:
            return Response(
                {"mision_id": "Este campo es obligatorio."},
                status=status.HTTP_400_BAD_REQUEST,
            )

        estado = (request.data.get("estado") or "disponible").strip()
        if estado not in ("disponible", "completada"):
            return Response(
                {"estado": "Debe ser 'disponible' o 'completada'."},
                status=status.HTTP_400_BAD_REQUEST,
            )

        if not Mision.objects.filter(mision_id=mision_id).exists():
            logger.warning(
                "MisionProgreso: '%s' no existe en el catalogo de misiones. Se guarda "
                "igual, pero el id de Unity y el del banco no estan alineados.",
                mision_id,
            )

        progreso, created = MisionProgreso.objects.get_or_create(
            partida=partida, mision_id=mision_id
        )
        if estado == "completada" and progreso.fecha_completada is None:
            progreso.fecha_completada = timezone.now()
            progreso.save(update_fields=["fecha_completada"])

        progresos = [progreso]
        codigo = status.HTTP_201_CREATED if created else status.HTTP_200_OK
    else:
        progresos = list(partida.misiones.all())
        codigo = status.HTTP_200_OK

    catalogo = {
        m.mision_id: m
        for m in Mision.objects.filter(
            mision_id__in={p.mision_id for p in progresos}
        )
    }
    datos = MisionProgresoSerializer(
        progresos, many=True, context={"catalogo": catalogo}
    ).data
    return Response(datos[0] if request.method == "POST" else datos, status=codigo)


# ── Progreso de zonas (HDU-3 CA5 y HDU-4 CA5) ─────────────────────────────────

@api_view(["GET", "POST"])
def zonas_partida(request, partida_id):
    """
    GET  — zonas desbloqueadas en esta partida, y cuales estan completadas.
    POST — desbloquea o completa una. Body:
           { "zona": "ciberacoso", "completada": true | false }

    Que exista la fila significa que la zona esta desbloqueada, asi que un POST
    con `completada: false` es lo que se manda al abrir una zona nueva. Igual que
    las misiones, completar es un camino de ida: `completada: false` sobre una
    zona ya completada no la reabre.

    La `zona` es el slug del banco (`desconocidos`, `ciberacoso`, `reto_viral`).
    No se valida contra una lista fija a proposito: agregar una tematica es
    contenido, no una migracion ni un cambio de codigo.
    """
    partida = get_object_or_404(
        Partida, pk=partida_id, usuario_jugador__adulto=request.user
    )

    if request.method == "GET":
        return Response(ZonaProgresoSerializer(partida.zonas.all(), many=True).data)

    zona = (request.data.get("zona") or "").strip()
    if not zona:
        return Response(
            {"zona": "Este campo es obligatorio."},
            status=status.HTTP_400_BAD_REQUEST,
        )

    progreso, created = ZonaProgreso.objects.get_or_create(partida=partida, zona=zona)
    if request.data.get("completada") and progreso.fecha_completada is None:
        progreso.fecha_completada = timezone.now()
        progreso.save(update_fields=["fecha_completada"])

    return Response(
        ZonaProgresoSerializer(progreso).data,
        status=status.HTTP_201_CREATED if created else status.HTTP_200_OK,
    )


# ── Inventario (la mochila de Otto — por partida) ─────────────────────────────

# Los limites de las columnas, para poder responder 400 en vez de dejar que
# reviente Postgres con un 500. Hace falta decirlo aca porque `save()` NO llama a
# `full_clean()`: Django manda el valor tal cual y el error aparece recien en la
# base. Y no se nota en los tests, porque la suite corre sobre SQLite y SQLite no
# valida ni el largo de un varchar ni el rango de un smallint — ambos casos
# devolvian 200 ahi y 500 contra Supabase.
LARGO_MAX_ITEM_ID = ItemInventario._meta.get_field("item_id").max_length
CANTIDAD_MAX = 32767   # tope de PositiveSmallIntegerField (smallint de Postgres)


@api_view(["GET", "PUT"])
def inventario_partida(request, partida_id):
    """
    GET — lo que Otto lleva encima en esta partida.
    PUT — **reemplaza** la mochila completa. Body:
          { "items": [ {"item_id": "ITEM_BRUJULA", "cantidad": 2}, ... ] }

    **Por que PUT de reemplazo y no POST incremental como en misiones:** una
    mision solo crece —se desbloquea y se completa, nunca se "descompleta"—, asi
    que ahi el POST por fila es natural y ademas protege el registro del adulto.
    El inventario encoge: `ItemType.Consumable` significa que un objeto usado sale
    de la mochila. Con POST por fila no hay forma de decir "ya no tengo esto" sin
    inventar un DELETE por objeto, y bastaria con que una de esas llamadas se
    perdiera para que el nino viera un objeto fantasma al retomar. Mandando la
    lista completa, el estado del servidor no puede quedar a medio camino: lo que
    no viene, no esta.

    Idempotente por construccion: mandar dos veces la misma lista deja lo mismo.
    Va en una transaccion para que no exista un instante con la mochila a medias.

    Una `cantidad` de 0 o menos se trata como "no lo tengo": no se guarda la fila.
    Es lo mismo que omitir el objeto, pero se acepta para que Unity pueda mandar su
    lista tal cual sin filtrarla antes.
    """
    partida = get_object_or_404(
        Partida, pk=partida_id, usuario_jugador__adulto=request.user
    )

    if request.method == "PUT":
        items = request.data.get("items")
        if items is None or not isinstance(items, list):
            return Response(
                {"items": "Se espera una lista, aunque sea vacia."},
                status=status.HTTP_400_BAD_REQUEST,
            )

        # Se normaliza antes de tocar la base: si un item viene mal, no se escribe
        # nada. Un PUT a medias dejaria la mochila en un estado que el nino nunca
        # tuvo, que es peor que rechazar la llamada entera.
        limpios = {}
        for i, crudo in enumerate(items):
            if not isinstance(crudo, dict):
                return Response(
                    {"items": f"El elemento {i} no es un objeto."},
                    status=status.HTTP_400_BAD_REQUEST,
                )

            item_id = (crudo.get("item_id") or "").strip()
            if not item_id:
                return Response(
                    {"items": f"El elemento {i} no trae `item_id`."},
                    status=status.HTTP_400_BAD_REQUEST,
                )

            if len(item_id) > LARGO_MAX_ITEM_ID:
                return Response(
                    {"items": f"El `item_id` del elemento {i} pasa los "
                              f"{LARGO_MAX_ITEM_ID} caracteres."},
                    status=status.HTTP_400_BAD_REQUEST,
                )

            try:
                cantidad = int(crudo.get("cantidad", 1))
            except (TypeError, ValueError):
                return Response(
                    {"items": f"La cantidad de '{item_id}' no es un numero."},
                    status=status.HTTP_400_BAD_REQUEST,
                )

            if cantidad <= 0:
                continue

            if cantidad > CANTIDAD_MAX:
                return Response(
                    {"items": f"La cantidad de '{item_id}' pasa el maximo "
                              f"de {CANTIDAD_MAX}."},
                    status=status.HTTP_400_BAD_REQUEST,
                )

            # Repetido en el mismo PUT: se suman en vez de que gane el ultimo. Que
            # Unity mande dos filas del mismo objeto seria un bug suyo, pero
            # perder unidades en silencio es peor que quedarse con las dos.
            limpios[item_id] = limpios.get(item_id, 0) + cantidad

        # Cada sumando cabia, pero la suma puede no caber.
        for item_id, cantidad in limpios.items():
            if cantidad > CANTIDAD_MAX:
                return Response(
                    {"items": f"La cantidad total de '{item_id}' pasa el maximo "
                              f"de {CANTIDAD_MAX}."},
                    status=status.HTTP_400_BAD_REQUEST,
                )

        with transaction.atomic():
            partida.inventario.exclude(item_id__in=limpios.keys()).delete()

            for item_id, cantidad in limpios.items():
                ItemInventario.objects.update_or_create(
                    partida=partida, item_id=item_id,
                    defaults={"cantidad": cantidad},
                )

    inventario = partida.inventario.all()
    return Response(ItemInventarioSerializer(inventario, many=True).data)
