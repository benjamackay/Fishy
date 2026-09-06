from rest_framework import serializers
from .models import (
    AdultoResponsable, UsuarioJugador, NivelRiesgo, Partida, NPC, Chat,
    Mensaje, PosibleRespuesta, PreguntaBanco, OpcionBanco,
    CasoDetective, MensajeDetective, CasoDetectiveProgreso,
    DialogoNPC, MisionProgreso, ZonaProgreso, ItemInventario,
)


class RegistroSerializer(serializers.ModelSerializer):
    """Alta de la cuenta del adulto responsable (la única con login)."""
    password = serializers.CharField(write_only=True, min_length=4)

    class Meta:
        model = AdultoResponsable
        fields = ["id", "nombre", "apellido", "email", "edad", "fecha_nacimiento", "password"]

    def create(self, validated_data):
        return AdultoResponsable.objects.create_user(**validated_data)


class AdultoResponsableSerializer(serializers.ModelSerializer):
    """Datos del adulto autenticado (sin password)."""
    class Meta:
        model = AdultoResponsable
        fields = ["id", "nombre", "apellido", "email", "edad", "fecha_nacimiento", "fecha_creacion"]
        read_only_fields = fields


class UsuarioJugadorSerializer(serializers.ModelSerializer):
    """Perfil de menor. `adulto` nunca viene del cliente: lo fija la vista con
    el usuario autenticado, para que nadie cree perfiles a nombre de otro."""
    class Meta:
        model = UsuarioJugador
        fields = ["id", "adulto", "nombre", "edad", "fecha_creacion"]
        read_only_fields = ["id", "adulto", "fecha_creacion"]


class NivelRiesgoSerializer(serializers.ModelSerializer):
    class Meta:
        model = NivelRiesgo
        fields = ["id", "nombre", "descripcion", "puntaje"]


class PartidaSerializer(serializers.ModelSerializer):
    class Meta:
        model = Partida
        fields = ["id", "usuario_jugador", "nivel_riesgo", "progreso", "fecha_inicio", "fecha_update"]
        read_only_fields = ["id", "usuario_jugador", "fecha_inicio", "fecha_update"]


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
            "calidad_respuesta", "pregunta_banco_id", "opcion_banco_id",
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


class MensajeDetectiveSerializer(serializers.ModelSerializer):
    class Meta:
        model = MensajeDetective
        fields = [
            "id", "mensaje_id", "npc_sender", "texto",
            "es_senal_riesgo", "es_ambiguo", "explicacion", "nota_ambiguo", "orden",
        ]


class CasoDetectiveSerializer(serializers.ModelSerializer):
    """Caso completo con sus mensajes anidados (mismo patrón que PreguntaBanco.opciones)."""
    mensajes = MensajeDetectiveSerializer(many=True, read_only=True)

    class Meta:
        model = CasoDetective
        fields = [
            "id", "caso_id", "titulo", "zona", "etiquetas_ml",
            "permiso_player_text", "permiso_npc_nombre", "permiso_npc_response",
            "mensajes",
        ]


class DialogoNPCSerializer(serializers.ModelSerializer):
    """Diálogo de un NPC neutro (sin árbol de decisiones): líneas planas + la
    misión que desbloquea, si tiene una (mision_id, no el FK entero)."""
    mision_id = serializers.CharField(source="mision.mision_id", read_only=True, default=None)

    class Meta:
        model = DialogoNPC
        fields = [
            "dialogo_id", "hdu", "zona", "npc_id", "npc_nombre", "npc_avatar",
            "tipo", "trigger", "lineas", "pista_mision", "mision_id",
        ]


class CasoDetectiveProgresoSerializer(serializers.ModelSerializer):
    """Resultado de un intento del jugador sobre un caso. `partida`/`caso`/`intentos`
    nunca vienen del cliente: los fija la vista (mismo patrón que UsuarioJugador.adulto).

    `caso` es la PK numérica; se agrega `caso_id` (el string, ej. DC_CASO_01) porque es
    lo que el cliente tiene serializado en el prefab y lo único con lo que puede saber
    si un caso ya está completado."""
    caso_id = serializers.CharField(source="caso.caso_id", read_only=True)

    class Meta:
        model = CasoDetectiveProgreso
        fields = [
            "id", "partida", "caso", "caso_id", "mensajes_marcados",
            "aciertos", "total_riesgo", "porcentaje", "intentos",
            "fecha_inicio", "fecha_termino",
        ]
        read_only_fields = ["id", "partida", "caso", "intentos", "fecha_inicio"]


class MisionProgresoSerializer(serializers.ModelSerializer):
    """Progreso de una mision dentro de una partida.

    `estado` y `en_catalogo` no son columnas: el primero se deriva de
    `fecha_completada` (ver el modelo) y el segundo lo resuelve la vista de una
    sola consulta al catalogo y lo pasa por `context["en_catalogo"]`, para no
    hacer un SELECT por fila."""
    estado      = serializers.CharField(read_only=True)
    nombre      = serializers.SerializerMethodField()
    zona        = serializers.SerializerMethodField()
    en_catalogo = serializers.SerializerMethodField()

    class Meta:
        model = MisionProgreso
        fields = [
            "id", "mision_id", "estado", "nombre", "zona", "en_catalogo",
            "fecha_desbloqueo", "fecha_completada",
        ]
        read_only_fields = fields

    def _catalogo(self, obj):
        return (self.context.get("catalogo") or {}).get(obj.mision_id)

    def get_nombre(self, obj):
        mision = self._catalogo(obj)
        return mision.nombre if mision else ""

    def get_zona(self, obj):
        mision = self._catalogo(obj)
        return mision.zona if mision else ""

    def get_en_catalogo(self, obj):
        return self._catalogo(obj) is not None


class ZonaProgresoSerializer(serializers.ModelSerializer):
    """Progreso de una zona dentro de una partida. Que la fila exista significa
    que la zona esta desbloqueada; `completada` se deriva de la fecha."""
    completada   = serializers.BooleanField(read_only=True)
    desbloqueada = serializers.SerializerMethodField()

    class Meta:
        model = ZonaProgreso
        fields = [
            "id", "zona", "desbloqueada", "completada",
            "fecha_desbloqueo", "fecha_completada",
        ]
        read_only_fields = fields

    def get_desbloqueada(self, obj):
        # Siempre True: la fila solo existe si la zona se desbloqueo. Va explicito
        # igual porque Unity pinta el mapa leyendo este campo, no la ausencia de fila.
        return True


class ItemInventarioSerializer(serializers.ModelSerializer):
    """Un objeto de la mochila de Otto dentro de una partida.

    Solo viaja el `item_id` y cuanto lleva: el nombre visible, el icono y la
    descripcion viven en el ScriptableObject de Unity y no se duplican aca (ver
    el comentario del modelo). No hay `en_catalogo` como en MisionProgreso
    porque no hay catalogo de items contra el cual comparar."""

    class Meta:
        model = ItemInventario
        fields = ["id", "item_id", "cantidad", "fecha_primera_vez", "fecha_actualizacion"]
        read_only_fields = fields
