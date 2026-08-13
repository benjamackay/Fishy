from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from rest_framework import status
from rest_framework.authtoken.models import Token
from django.contrib.auth import authenticate
from django.shortcuts import get_object_or_404

from .models import (
    UsuarioJugador, NivelRiesgo, Partida, NPC, Chat, Mensaje,
    PosibleRespuesta, PreguntaBanco,
)
from .serializers import (
    RegistroSerializer, AdultoResponsableSerializer, UsuarioJugadorSerializer,
    NivelRiesgoSerializer, PartidaSerializer,
    NPCSerializer, ChatSerializer, MensajeSerializer,
    PreguntaBancoSerializer,
)


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

@api_view(["POST"])
def crear_partida(request):
    """Body: { "usuario_jugador_id": int, "nivel_riesgo": int (opcional) }.
    El perfil debe pertenecer al adulto autenticado."""
    jugador = get_object_or_404(
        UsuarioJugador,
        pk=request.data.get("usuario_jugador_id"),
        adulto=request.user,
    )
    serializer = PartidaSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    partida = serializer.save(usuario_jugador=jugador)
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
        "posibles_respuestas": [{"texto": str, "orden": int, "calidad_respuesta": str}]  (opcional)
    }
    """
    chat = get_object_or_404(Chat, pk=chat_id, partida__usuario_jugador__adulto=request.user)
    if chat.fecha_termino is not None:
        return Response({"error": "El chat ya finalizó"}, status=status.HTTP_400_BAD_REQUEST)

    serializer = MensajeSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)

    # pregunta_banco_id opcional: vincula la respuesta con la pregunta del banco
    pregunta_banco_id = request.data.get("pregunta_banco_id") or None
    mensaje = serializer.save(chat=chat, pregunta_banco_id=pregunta_banco_id)

    for i, opcion in enumerate(request.data.get("posibles_respuestas", [])):
        PosibleRespuesta.objects.create(
            mensaje=mensaje,
            texto=opcion.get("texto", ""),
            orden=opcion.get("orden", i),
            calidad_respuesta=opcion.get("calidad_respuesta", ""),
        )

    return Response(MensajeSerializer(mensaje).data, status=status.HTTP_201_CREATED)


# ── Banco de Preguntas (HDU-2 / HDU-8) ───────────────────────────────────────

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
    qs = PreguntaBanco.objects.prefetch_related("opciones").all()
    for param, campo in [
        ("zona", "zona"),
        ("npc_id", "npc_id"),
        ("escenario_id", "escenario_id"),
        ("hdu", "hdu"),
    ]:
        val = request.query_params.get(param)
        if val:
            qs = qs.filter(**{campo: val})
    fase = request.query_params.get("fase")
    if fase is not None:
        qs = qs.filter(fase=int(fase))
    if request.query_params.get("solo_riesgo", "").lower() == "true":
        qs = qs.filter(es_mensaje_riesgo=True)
    if request.query_params.get("fin_de_zona", "").lower() == "true":
        qs = qs.filter(es_fin_de_zona=True)
    if request.query_params.get("fin_de_npc", "").lower() == "true":
        qs = qs.filter(es_fin_de_npc=True)
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
