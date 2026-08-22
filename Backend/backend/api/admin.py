from django.contrib import admin
from django.contrib.auth.admin import UserAdmin as BaseUserAdmin
from .models import (
    AdultoResponsable, UsuarioJugador, Zona,
    NivelRiesgo, Partida, PersonajeJugador,
    NPC, Chat, Mensaje, PosibleRespuesta
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

