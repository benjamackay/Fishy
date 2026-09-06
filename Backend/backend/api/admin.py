from django.contrib import admin
from django.contrib.auth.admin import UserAdmin as BaseUserAdmin
from django.db.models import Q
from .models import (
    AdultoResponsable, UsuarioJugador, Zona,
    NivelRiesgo, Partida, PersonajeJugador,
    NPC, Chat, Mensaje, PosibleRespuesta,
    PreguntaBanco, OpcionBanco,
    CasoDetective, MensajeDetective, CasoDetectiveProgreso,
    Mision, DialogoNPC, RecompensaAlbum, RecompensaObtenida,
    MisionProgreso, ZonaProgreso, ItemInventario,
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


class MensajeInline(admin.TabularInline):
    """El chat completo, en orden, dentro de la ficha del Chat.

    Es la forma de leer una conversación como conversación —qué dijo el NPC, qué
    contestó el niño y con qué opción del banco— en vez de reconstruirla saltando
    entre filas sueltas del listado de mensajes.
    """
    model = Mensaje
    extra = 0
    can_delete = False
    fields = ("timestamp", "tipo", "respuesta", "calidad_respuesta",
              "pregunta_banco_id", "opcion_banco_id")
    readonly_fields = fields
    ordering = ("timestamp",)

    def has_add_permission(self, request, obj=None):
        return False


@admin.register(Chat)
class ChatAdmin(admin.ModelAdmin):
    list_display  = ("id", "npc", "partida", "categoria_riesgo",
                     "fecha_inicio", "estado")
    list_filter   = ("categoria_riesgo",)
    search_fields = ("npc__nombre", "partida__usuario_jugador__nombre")
    list_select_related = ("npc", "partida__usuario_jugador")
    inlines = (MensajeInline,)

    @admin.display(description="estado")
    def estado(self, obj):
        # Un chat sin `fecha_termino` es uno que quedó abierto: la conversación se
        # cortó sin registrar el mensaje de cierre. Verlo de una permite pillar
        # ramas del banco que apuntan a contenido que no se cargó.
        return "cerrado" if obj.fecha_termino else "SIN CERRAR"


class SoloDecisionesFilter(admin.SimpleListFilter):
    """Separa las decisiones del jugador del resto del historial del chat.

    Un `Mensaje` puede ser el mensaje del NPC, las opciones ofrecidas o el cierre.
    Solo los que traen `opcion_banco_id` son una decisión que tomó el niño.
    """
    title = "es una decisión del jugador"
    parameter_name = "es_decision"

    def lookups(self, request, model_admin):
        return [("si", "Sí — el niño eligió una opción"),
                ("no", "No — mensaje del NPC o cierre")]

    def queryset(self, request, qs):
        if self.value() == "si":
            return qs.exclude(opcion_banco_id__isnull=True).exclude(opcion_banco_id="")
        if self.value() == "no":
            return qs.filter(Q(opcion_banco_id__isnull=True) | Q(opcion_banco_id=""))
        return qs


class ZonaDelBancoFilter(admin.SimpleListFilter):
    """Filtra por la zona de la pregunta que originó el mensaje.

    `pregunta_banco_id` es un CharField, no una FK, así que no existe un camino
    ORM tipo `pregunta__zona`. Se resuelven los ids de la zona y se filtra por
    `pregunta_banco_id__in`: el banco son decenas de filas, así que sale barato y
    evita migrar el campo a FK solo para poder filtrar.
    """
    title = "zona del banco"
    parameter_name = "zona_banco"

    def lookups(self, request, model_admin):
        # El `.order_by()` vacío no es decorativo: PreguntaBanco.Meta.ordering
        # agrega sus campos al SELECT, y con eso el DISTINCT deja de deduplicar y
        # la zona sale repetida una vez por pregunta.
        zonas = (
            PreguntaBanco.objects
            .order_by()
            .values_list("zona", flat=True)
            .distinct()
        )
        return [(z, z) for z in sorted(zonas) if z]

    def queryset(self, request, qs):
        if not self.value():
            return qs
        ids = list(
            PreguntaBanco.objects
            .filter(zona=self.value())
            .values_list("pregunta_id", flat=True)
        )
        return qs.filter(pregunta_banco_id__in=ids)


@admin.register(Mensaje)
class MensajeAdmin(admin.ModelAdmin):
    """Lo que el niño respondió, legible desde el listado.

    Antes el listado mostraba solo id/chat/tipo/calidad/hora, así que "ver qué
    decidió el jugador" no se podía: ni el texto elegido ni el `opcion_banco_id`
    —la llave de la decisión— aparecían, y no había con qué buscar ni filtrar.
    """
    list_display  = ("timestamp", "jugador", "npc", "tipo", "eligio",
                     "opcion_banco_id", "tipo_de_opcion", "impacto", "zona")
    list_filter   = (SoloDecisionesFilter, ZonaDelBancoFilter, "tipo",
                     "calidad_respuesta", "chat__categoria_riesgo")
    search_fields = ("respuesta", "opcion_banco_id", "pregunta_banco_id",
                     "chat__partida__usuario_jugador__nombre", "chat__npc__nombre")
    date_hierarchy = "timestamp"
    list_select_related = ("chat__npc", "chat__partida__usuario_jugador")

    def get_queryset(self, request):
        # El banco completo son decenas de filas: se carga una vez por vista para
        # que las columnas que lo resuelven (tipo de opción, impacto, zona) no
        # hagan una consulta por fila. Se rearma en cada request, así que una
        # recarga del banco con `cargar_banco` se refleja de inmediato.
        self._banco = {
            o.opcion_id: o
            for o in OpcionBanco.objects.select_related("pregunta").all()
        }
        return super().get_queryset(request)

    def _opcion(self, obj):
        return getattr(self, "_banco", {}).get(obj.opcion_banco_id or "")

    @admin.display(description="menor")
    def jugador(self, obj):
        return obj.chat.partida.usuario_jugador.nombre

    @admin.display(description="NPC")
    def npc(self, obj):
        return obj.chat.npc.nombre

    @admin.display(description="lo que eligió")
    def eligio(self, obj):
        texto = obj.respuesta or ""
        return texto if len(texto) <= 70 else texto[:69] + "…"

    @admin.display(description="tipo de opción")
    def tipo_de_opcion(self, obj):
        opcion = self._opcion(obj)
        return opcion.tipo if opcion else "—"

    @admin.display(description="impacto")
    def impacto(self, obj):
        opcion = self._opcion(obj)
        return f"{opcion.impacto_puntuacion:+d}" if opcion else "—"

    @admin.display(description="zona")
    def zona(self, obj):
        opcion = self._opcion(obj)
        return opcion.pregunta.zona if opcion else "—"


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



# ─────────────────────────────────────────────────────────────────────────────
# PROGRESO POR PARTIDA  (misiones y zonas)
# ─────────────────────────────────────────────────────────────────────────────

@admin.register(MisionProgreso)
class MisionProgresoAdmin(admin.ModelAdmin):
    list_display  = ("mision_id", "jugador", "partida", "estado", "en_catalogo",
                     "fecha_desbloqueo", "fecha_completada")
    list_filter   = ("partida__usuario_jugador",)
    search_fields = ("mision_id", "partida__usuario_jugador__nombre")
    readonly_fields = ("fecha_desbloqueo",)

    def get_queryset(self, request):
        # El catálogo entero de una vez: son unas pocas decenas de misiones y
        # así la columna `en_catalogo` no dispara un SELECT por fila.
        qs = super().get_queryset(request).select_related("partida__usuario_jugador")
        self._catalogo = set(Mision.objects.values_list("mision_id", flat=True))
        return qs

    @admin.display(description="menor")
    def jugador(self, obj):
        return obj.partida.usuario_jugador.nombre

    @admin.display(description="estado")
    def estado(self, obj):
        return obj.estado

    @admin.display(description="en el banco", boolean=True)
    def en_catalogo(self, obj):
        # False significa que Unity manda un id que el banco no tiene — hoy pasa
        # con MISION_NPC_01 y MISION_NPC_02. El progreso se guarda igual.
        return obj.mision_id in getattr(self, "_catalogo", set())


@admin.register(ZonaProgreso)
class ZonaProgresoAdmin(admin.ModelAdmin):
    # Que la fila exista ya significa que la zona está desbloqueada; la columna
    # que importa mirar es si además quedó completada.
    list_display  = ("zona", "jugador", "partida", "completada",
                     "fecha_desbloqueo", "fecha_completada")
    list_filter   = ("zona",)
    search_fields = ("zona", "partida__usuario_jugador__nombre")
    readonly_fields = ("fecha_desbloqueo",)

    def get_queryset(self, request):
        return super().get_queryset(request).select_related("partida__usuario_jugador")

    @admin.display(description="menor")
    def jugador(self, obj):
        return obj.partida.usuario_jugador.nombre

    @admin.display(description="completada", boolean=True)
    def completada(self, obj):
        return obj.completada


@admin.register(ItemInventario)
class ItemInventarioAdmin(admin.ModelAdmin):
    # No hay catálogo de items en el backend (los objetos se crean en Unity), así
    # que aquí se ve el `item_id` crudo. Es a propósito: ver el id es justo lo que
    # sirve para detectar que Unity y la base dejaron de hablar el mismo idioma.
    list_display  = ("item_id", "cantidad", "jugador", "partida", "fecha_actualizacion")
    list_filter   = ("item_id",)
    search_fields = ("item_id", "partida__usuario_jugador__nombre")
    readonly_fields = ("fecha_agregado", "fecha_actualizacion")

    def get_queryset(self, request):
        return super().get_queryset(request).select_related("partida__usuario_jugador")

    @admin.display(description="menor")
    def jugador(self, obj):
        return obj.partida.usuario_jugador.nombre
