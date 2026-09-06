from django.urls import path
from . import views

urlpatterns = [
    # Health
    path("health/", views.health_check, name="health_check"),

    # Auth (cuenta del adulto responsable)
    path("auth/registro/", views.registro, name="registro"),
    path("auth/login/", views.auth_login, name="auth_login"),
    path("auth/perfil/", views.perfil_adulto, name="perfil_adulto"),

    # Perfiles de menores (control parental)
    path("jugadores/", views.jugadores, name="jugadores"),
    path("jugadores/<int:jugador_id>/", views.jugador_detalle, name="jugador_detalle"),
    path("jugadores/<int:jugador_id>/partidas/", views.partidas_jugador, name="partidas_jugador"),

    # Catálogos
    path("niveles-riesgo/", views.niveles_riesgo, name="niveles_riesgo"),

    # Partida (HDU-2)
    path("partidas/", views.crear_partida, name="crear_partida"),
    path("partidas/<int:partida_id>/", views.partida_detalle, name="partida_detalle"),
    path("partidas/<int:partida_id>/npcs/", views.npcs_partida, name="npcs_partida"),
    path("partidas/<int:partida_id>/riesgo-por-zona/", views.riesgo_por_zona, name="riesgo_por_zona"),
    path("partidas/<int:partida_id>/oportunidades-mejora/", views.oportunidades_mejora, name="oportunidades_mejora"),

    # Progreso por partida (HDU-1 CA4/CA5, HDU-3 CA5, HDU-4 CA5)
    path("partidas/<int:partida_id>/misiones/", views.misiones_partida, name="misiones_partida"),
    path("partidas/<int:partida_id>/zonas/", views.zonas_partida, name="zonas_partida"),

    # Inventario por partida (HDU-15)
    path("partidas/<int:partida_id>/inventario/", views.inventario_partida, name="inventario_partida"),
    path("partidas/<int:partida_id>/personaje/", views.personaje_partida, name="personaje_partida"),
    path("partidas/<int:partida_id>/objetos-recogidos/", views.objetos_recogidos_partida, name="objetos_recogidos_partida"),

    # NPC (HDU-2)
    path("npcs/<int:npc_id>/", views.npc_actualizar, name="npc_actualizar"),

    # Chat (HDU-8)
    path("chats/", views.iniciar_chat, name="iniciar_chat"),
    path("chats/<int:chat_id>/mensajes/", views.mensajes_chat, name="mensajes_chat"),
    path("chats/<int:chat_id>/mensajes/registrar/", views.registrar_mensaje, name="registrar_mensaje"),
    path("chats/<int:chat_id>/finalizar/", views.finalizar_chat, name="finalizar_chat"),

    # Banco de Preguntas (HDU-2 y HDU-8)
    path("banco/zonas/", views.zonas_banco, name="zonas_banco"),
    path("banco/zonas/<str:zona>/preguntas/", views.preguntas_zona, name="preguntas_zona"),
    path("banco/preguntas/", views.preguntas_banco, name="preguntas_banco"),
    path("banco/preguntas/<str:pregunta_id>/", views.pregunta_detalle, name="pregunta_detalle"),

    # Modo Detective (HDU-10)
    path("casos-detective/", views.casos_detective, name="casos_detective"),
    path("casos-detective/<str:caso_id>/", views.caso_detective_detalle, name="caso_detective_detalle"),
    path("casos-detective/<str:caso_id>/progreso/", views.registrar_progreso_detective, name="registrar_progreso_detective"),
    path("partidas/<int:partida_id>/casos-detective/", views.progreso_detective_partida, name="progreso_detective_partida"),

    # Diálogos de NPCs neutros (HDU-1)
    path("dialogos-npc/", views.dialogos_npc, name="dialogos_npc"),
    path("dialogos-npc/<str:dialogo_id>/", views.dialogo_npc_detalle, name="dialogo_npc_detalle"),
]
