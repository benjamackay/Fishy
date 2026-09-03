from django.contrib import admin
from django.contrib.auth.admin import UserAdmin as BaseUserAdmin
from .models import (
    AdultoResponsable, UsuarioJugador, Zona,
    NivelRiesgo, Partida, PersonajeJugador,
    NPC, Chat, Mensaje, PosibleRespuesta,
    PreguntaBanco, OpcionBanco,
    CasoDetective, MensajeDetective, CasoDetectiveProgreso,
    Mision, DialogoNPC, RecompensaAlbum, RecompensaObtenida,
)


class UsuarioJugadorInline(admin.TabularInline):
    model = UsuarioJugador
    extra = 0


@admin.register(AdultoResponsable)
class AdultoResponsableAdmin(BaseUserAdmin):
    list_display  = ("nombre", "apellido", "email", "is_admin", "fecha_creacion")
    list_filter   = ("is_admin",)
    search_fields = ("nombre", "apellido", "email")
    ordering      = ("nombre",)
    filter_horizontal = ()
    inlines = (UsuarioJugadorInline,)
    fieldsets = (
        (None,          {"fields": ("nombre", "password")}),
        ("Datos personales", {"fields": ("apellido", "email", "edad", "fecha_nacimiento")}),
        ("Permisos",    {"fields": ("is_admin",)}),
    )
    add_fieldsets = (
        (None, {"fields": ("nombre", "email", "password1", "password2")}),
    )


@admin.register(UsuarioJugador)
class UsuarioJugadorAdmin(admin.ModelAdmin):
    list_display  = ("nombre", "edad", "adulto", "fecha_creacion")
    search_fields = ("nombre", "adulto__nombre")


@admin.register(Zona)
class ZonaAdmin(admin.ModelAdmin):
    list_display = ("nombre", "descripcion")


@admin.register(NivelRiesgo)
class NivelRiesgoAdmin(admin.ModelAdmin):
    list_display = ("nombre", "descripcion")


@admin.register(Partida)
class PartidaAdmin(admin.ModelAdmin):
    list_display  = ("id", "usuario_jugador", "nivel_riesgo", "progreso", "fecha_inicio")
    list_filter   = ("nivel_riesgo",)
    search_fields = ("usuario_jugador__nombre", "usuario_jugador__adulto__nombre")


@admin.register(PersonajeJugador)
class PersonajeJugadorAdmin(admin.ModelAdmin):
    list_display = ("id", "partida")


@admin.register(NPC)
class NPCAdmin(admin.ModelAdmin):
    list_display  = ("nombre", "tipo", "area", "confianza", "partida")
    list_filter   = ("tipo",)
    search_fields = ("nombre",)


@admin.register(Chat)
class ChatAdmin(admin.ModelAdmin):
    list_display  = ("id", "npc", "partida", "categoria_riesgo", "fecha_inicio")
    list_filter   = ("categoria_riesgo",)


@admin.register(Mensaje)
class MensajeAdmin(admin.ModelAdmin):
    list_display  = ("id", "chat", "tipo", "calidad_respuesta", "timestamp")
    list_filter   = ("tipo", "calidad_respuesta")


@admin.register(PosibleRespuesta)
class PosibleRespuestaAdmin(admin.ModelAdmin):
    list_display = ("id", "mensaje", "orden", "texto")



# ─────────────────────────────────────────────────────────────────────────────
# CONTENIDO DEL BANCO  (solo lectura)
#
# Todo lo de abajo se carga desde banco_preguntas/ con `manage.py cargar_banco`
# y `cargar_detective`. El JSON es la fuente de verdad: Luis confirmó que esa
# carpeta manda. Por eso el admin NO deja editar acá — una edición a mano se
# vería aplicada hasta la próxima carga, que la pisaría sin avisar, y quedaría
# una divergencia silenciosa entre el JSON, la base y Unity. Para cambiar el
# contenido se edita el JSON y se vuelve a cargar.
# ─────────────────────────────────────────────────────────────────────────────

class SoloLecturaAdmin(admin.ModelAdmin):
    """Se puede mirar y buscar, no crear ni editar ni borrar."""

    def has_add_permission(self, request):
        return False

    def has_change_permission(self, request, obj=None):
        return False

    def has_delete_permission(self, request, obj=None):
        return False


class OpcionBancoInline(admin.TabularInline):
    model = OpcionBanco
    extra = 0
    can_delete = False
    fields = ("opcion_id", "tipo", "impacto_puntuacion", "texto", "siguiente_pregunta")
    readonly_fields = fields

    def has_add_permission(self, request, obj=None):
        return False


@admin.register(PreguntaBanco)
class PreguntaBancoAdmin(SoloLecturaAdmin):
    list_display  = ("pregunta_id", "zona", "npc_nombre", "categoria",
                     "nivel_riesgo", "es_mensaje_riesgo", "escenario_id")
    list_filter   = ("zona", "hdu", "categoria", "es_mensaje_riesgo",
                     "es_fin_de_npc", "es_fin_de_zona")
    search_fields = ("pregunta_id", "mensaje_npc", "npc_nombre", "escenario_nombre")
    inlines = (OpcionBancoInline,)


@admin.register(OpcionBanco)
class OpcionBancoAdmin(SoloLecturaAdmin):
    # Se registra aparte del inline porque `opcion_id` es la llave que manda
    # Unity al responder, y a veces hay que buscar una suelta para depurar el
    # riesgo por zona sin saber a qué pregunta pertenece.
    list_display  = ("opcion_id", "pregunta", "tipo", "impacto_puntuacion", "orden")
    list_filter   = ("tipo", "pregunta__zona")
    search_fields = ("opcion_id", "texto")


# ─────────────────────────────────────────────────────────────────────────────
# MODO DETECTIVE
# ─────────────────────────────────────────────────────────────────────────────

class MensajeDetectiveInline(admin.TabularInline):
    model = MensajeDetective
    extra = 0
    can_delete = False
    fields = ("orden", "mensaje_id", "npc_sender", "texto", "es_senal_riesgo", "es_ambiguo")
    readonly_fields = fields

    def has_add_permission(self, request, obj=None):
        return False


@admin.register(CasoDetective)
class CasoDetectiveAdmin(SoloLecturaAdmin):
    list_display  = ("caso_id", "titulo", "zona", "permiso_npc_nombre")
    list_filter   = ("zona",)
    search_fields = ("caso_id", "titulo")
    inlines = (MensajeDetectiveInline,)


@admin.register(MensajeDetective)
class MensajeDetectiveAdmin(SoloLecturaAdmin):
    list_display  = ("mensaje_id", "caso", "orden", "npc_sender",
                     "es_senal_riesgo", "es_ambiguo")
    list_filter   = ("es_senal_riesgo", "es_ambiguo", "caso__zona")
    search_fields = ("mensaje_id", "texto", "npc_sender")


@admin.register(CasoDetectiveProgreso)
class CasoDetectiveProgresoAdmin(admin.ModelAdmin):
    # Progreso, no contenido: se genera jugando. Hoy está vacío porque no hay
    # endpoint que lo escriba (HDU-10). Ver el módulo Detective del backend.
    list_display  = ("caso", "partida", "aciertos", "total_riesgo",
                     "porcentaje", "intentos", "fecha_termino")
    list_filter   = ("caso", "intentos")
    search_fields = ("partida__usuario_jugador__nombre",)
    readonly_fields = ("fecha_inicio",)


# ─────────────────────────────────────────────────────────────────────────────
# MISIONES, DIÁLOGOS Y ÁLBUM  (HDU-1 / HDU-11 / HDU-12)
# ─────────────────────────────────────────────────────────────────────────────

class RecompensaAlbumInline(admin.TabularInline):
    model = RecompensaAlbum
    extra = 0
    can_delete = False
    fields = ("recompensa_id", "nombre", "tip_educativo")
    readonly_fields = fields

    def has_add_permission(self, request, obj=None):
        return False


@admin.register(Mision)
class MisionAdmin(SoloLecturaAdmin):
    list_display  = ("mision_id", "nombre", "tipo", "zona")
    list_filter   = ("tipo", "zona")
    search_fields = ("mision_id", "nombre")
    inlines = (RecompensaAlbumInline,)


@admin.register(DialogoNPC)
class DialogoNPCAdmin(SoloLecturaAdmin):
    list_display  = ("dialogo_id", "zona", "npc_nombre", "trigger",
                     "mision", "cantidad_lineas")
    list_filter   = ("zona", "hdu", "npc_nombre")
    search_fields = ("dialogo_id", "npc_nombre", "pista_mision")

    @admin.display(description="líneas")
    def cantidad_lineas(self, obj):
        return len(obj.lineas or [])


@admin.register(RecompensaAlbum)
class RecompensaAlbumAdmin(SoloLecturaAdmin):
    list_display  = ("recompensa_id", "nombre", "origen", "tip_educativo")
    list_filter   = ("mision__zona", "mision__tipo")
    search_fields = ("recompensa_id", "nombre", "tip_educativo")

    @admin.display(description="viene de")
    def origen(self, obj):
        # Cada recompensa cuelga de una misión o de una opción, nunca de las dos
        # (lo garantiza el CheckConstraint). Mostrarlo evita tener que abrir la
        # fila para saber de dónde salió.
        if obj.mision_id:
            return f"misión {obj.mision.mision_id}"
        return f"opción {obj.opcion_banco_id}"


@admin.register(RecompensaObtenida)
class RecompensaObtenidaAdmin(admin.ModelAdmin):
    # Progreso: esto es lo que responde "qué desbloqueó este niño".
    list_display  = ("recompensa", "partida", "fecha")
    list_filter   = ("recompensa__mision__zona",)
    search_fields = ("partida__usuario_jugador__nombre", "recompensa__nombre")
    readonly_fields = ("fecha",)
