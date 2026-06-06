from django.contrib import admin
from django.contrib.auth.admin import UserAdmin as BaseUserAdmin
from .models import (
    Usuario, NivelRiesgo, Partida, PersonajeJugador,
    NPC, Chat, Mensaje, PosibleRespuesta
)


@admin.register(Usuario)
class UsuarioAdmin(BaseUserAdmin):
    list_display  = ("nombre", "is_admin")
    list_filter = ("is_admin",)
    search_fields = ("nombre",)
    ordering      = ("nombre",)
    filter_horizontal = ()
    fieldsets = (
        (None,          {"fields": ("nombre", "password")}),
        ("Permisos",    {"fields": ("is_admin",)}),
    )
    add_fieldsets = (
        (None, {"fields": ("nombre", "password1", "password2")}),
    )


@admin.register(NivelRiesgo)
class NivelRiesgoAdmin(admin.ModelAdmin):
    list_display = ("nombre", "descripcion")


@admin.register(Partida)
class PartidaAdmin(admin.ModelAdmin):
    list_display  = ("id", "usuario", "nivel_riesgo", "progreso", "fecha_inicio")
    list_filter   = ("nivel_riesgo",)
    search_fields = ("usuario__nombre",)


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

