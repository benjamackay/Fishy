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

    # Catálogos
    path("niveles-riesgo/", views.niveles_riesgo, name="niveles_riesgo"),

    # Partida (HDU-2)
    path("partidas/", views.crear_partida, name="crear_partida"),
    path("partidas/<int:partida_id>/", views.partida_detalle, name="partida_detalle"),
    path("partidas/<int:partida_id>/npcs/", views.npcs_partida, name="npcs_partida"),

    # NPC (HDU-2)
    path("npcs/<int:npc_id>/", views.npc_actualizar, name="npc_actualizar"),

    # Chat (HDU-8)
    path("chats/", views.iniciar_chat, name="iniciar_chat"),
    path("chats/<int:chat_id>/mensajes/", views.mensajes_chat, name="mensajes_chat"),
    path("chats/<int:chat_id>/mensajes/registrar/", views.registrar_mensaje, name="registrar_mensaje"),
    path("chats/<int:chat_id>/finalizar/", views.finalizar_chat, name="finalizar_chat"),

    # Banco de Preguntas (HDU-2 y HDU-8)
    path("banco/preguntas/", views.preguntas_banco, name="preguntas_banco"),
    path("banco/preguntas/<str:pregunta_id>/", views.pregunta_detalle, name="pregunta_detalle"),
]
