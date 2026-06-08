from rest_framework import serializers
from .models import Usuario, NivelRiesgo, Partida, NPC, Chat, Mensaje, PosibleRespuesta, PreguntaBanco, OpcionBanco


class RegistroSerializer(serializers.ModelSerializer):
    password = serializers.CharField(write_only=True, min_length=4)

    class Meta:
        model = Usuario
        fields = ["id", "nombre", "password"]

    def create(self, validated_data):
        return Usuario.objects.create_user(**validated_data)


class NivelRiesgoSerializer(serializers.ModelSerializer):
    class Meta:
        model = NivelRiesgo
        fields = ["id", "nombre", "descripcion", "puntaje"]


class PartidaSerializer(serializers.ModelSerializer):
    class Meta:
        model = Partida
        fields = ["id", "usuario", "nivel_riesgo", "progreso", "fecha_inicio", "fecha_update"]
        read_only_fields = ["id", "usuario", "fecha_inicio", "fecha_update"]


class NPCSerializer(serializers.ModelSerializer):
    class Meta:
        model = NPC
        fields = ["id", "partida", "nombre", "area", "tipo", "confianza"]
        read_only_fields = ["id", "partida"]


class PosibleRespuestaSerializer(serializers.ModelSerializer):
    class Meta:
        model = PosibleRespuesta
        fields = ["id", "texto", "orden", "calidad_respuesta"]


class MensajeSerializer(serializers.ModelSerializer):
    posibles_respuestas = PosibleRespuestaSerializer(many=True, read_only=True)

    class Meta:
        model = Mensaje
        fields = [
            "id", "chat", "tipo", "respuesta",
            "calidad_respuesta", "pregunta_banco_id",
            "timestamp", "posibles_respuestas",
        ]
        read_only_fields = ["id", "chat", "timestamp"]


class ChatSerializer(serializers.ModelSerializer):
    class Meta:
        model = Chat
        fields = ["id", "partida", "npc", "categoria_riesgo", "fecha_inicio", "fecha_termino"]
        read_only_fields = ["id", "fecha_inicio", "fecha_termino"]


class OpcionBancoSerializer(serializers.ModelSerializer):
    class Meta:
        model = OpcionBanco
        fields = [
            "id", "opcion_id", "texto", "tipo",
            "consecuencia_narrativa", "impacto_puntuacion",
            "siguiente_pregunta", "orden",
        ]


class PreguntaBancoSerializer(serializers.ModelSerializer):
    opciones = OpcionBancoSerializer(many=True, read_only=True)

    class Meta:
        model = PreguntaBanco
        fields = [
            "id", "pregunta_id", "hdu", "zona",
            "npc_id", "npc_nombre", "npc_avatar", "fase", "orden_en_fase",
            "narrativa_continuacion",
            "escenario_id", "escenario_nombre", "historial_previo",
            "categoria", "nivel_riesgo", "es_mensaje_riesgo",
            "es_fin_de_npc", "es_fin_de_zona",
            "mensaje_npc", "etiquetas_ml",
            "opciones",
        ]
