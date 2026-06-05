from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from rest_framework import status
from rest_framework.authtoken.models import Token
from django.contrib.auth import authenticate
from django.shortcuts import get_object_or_404

from .models import NivelRiesgo, Partida, NPC, Chat, Mensaje, PosibleRespuesta
from .serializers import (
    RegistroSerializer, NivelRiesgoSerializer, PartidaSerializer,
    NPCSerializer, ChatSerializer, MensajeSerializer,
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
    serializer = RegistroSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    user = serializer.save()
    token, _ = Token.objects.get_or_create(user=user)
    return Response({"token": token.key, "usuario_id": user.pk}, status=status.HTTP_201_CREATED)


@api_view(["POST"])
@permission_classes([AllowAny])
def auth_login(request):
    nombre = request.data.get("nombre")
    password = request.data.get("password")
    user = authenticate(request, username=nombre, password=password)
    if user is None:
        return Response({"error": "Credenciales inválidas"}, status=status.HTTP_401_UNAUTHORIZED)
    token, _ = Token.objects.get_or_create(user=user)
    return Response({"token": token.key, "usuario_id": user.pk})


# ── Catálogos ─────────────────────────────────────────────────────────────────

@api_view(["GET"])
def niveles_riesgo(request):
    return Response(NivelRiesgoSerializer(NivelRiesgo.objects.all(), many=True).data)


# ── Partida (HDU-2) ───────────────────────────────────────────────────────────

@api_view(["POST"])
def crear_partida(request):
    serializer = PartidaSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    partida = serializer.save(usuario=request.user)
    return Response(PartidaSerializer(partida).data, status=status.HTTP_201_CREATED)


@api_view(["GET", "PATCH"])
def partida_detalle(request, partida_id):
    partida = get_object_or_404(Partida, pk=partida_id, usuario=request.user)
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
    partida = get_object_or_404(Partida, pk=partida_id, usuario=request.user)
    if request.method == "GET":
        return Response(NPCSerializer(partida.npcs.all(), many=True).data)
    serializer = NPCSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    npc = serializer.save(partida=partida)
    return Response(NPCSerializer(npc).data, status=status.HTTP_201_CREATED)


@api_view(["PATCH"])
def npc_actualizar(request, npc_id):
    npc = get_object_or_404(NPC, pk=npc_id, partida__usuario=request.user)
    serializer = NPCSerializer(npc, data=request.data, partial=True)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    serializer.save()
    return Response(serializer.data)


# ── Chat (HDU-8) ──────────────────────────────────────────────────────────────

@api_view(["POST"])
def iniciar_chat(request):
    """Body: { "partida_id": int, "npc_id": int, "categoria_riesgo": str (opcional) }"""
    partida = get_object_or_404(Partida, pk=request.data.get("partida_id"), usuario=request.user)
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
    chat = get_object_or_404(Chat, pk=chat_id, partida__usuario=request.user)
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
    chat = get_object_or_404(Chat, pk=chat_id, partida__usuario=request.user)
    if chat.fecha_termino is not None:
        return Response({"error": "El chat ya finalizó"}, status=status.HTTP_400_BAD_REQUEST)

    serializer = MensajeSerializer(data=request.data)
    if not serializer.is_valid():
        return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
    mensaje = serializer.save(chat=chat)

    for i, opcion in enumerate(request.data.get("posibles_respuestas", [])):
        PosibleRespuesta.objects.create(
            mensaje=mensaje,
            texto=opcion.get("texto", ""),
            orden=opcion.get("orden", i),
            calidad_respuesta=opcion.get("calidad_respuesta", ""),
        )

    return Response(MensajeSerializer(mensaje).data, status=status.HTTP_201_CREATED)


@api_view(["POST"])
def finalizar_chat(request, chat_id):
    """Cierra el chat registrando un mensaje END. Body (opcional): { "respuesta": str }"""
    chat = get_object_or_404(Chat, pk=chat_id, partida__usuario=request.user)
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
