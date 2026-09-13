"""Métricas de decisiones clasificadas en el banco; jamás serializa conversaciones."""
from collections import defaultdict

from django.shortcuts import get_object_or_404
from rest_framework.decorators import api_view
from rest_framework.exceptions import PermissionDenied
from rest_framework.response import Response

from .models import Mensaje, OpcionBanco, UsuarioJugador, ZonaProgreso

TEMAS = ("desconocidos", "ciberacoso", "retos_virales")
TIPOS = {"segura_basica", "segura_optima", "insegura"}
# El banco, Unity y ZonaProgreso guardan `reto_viral`; el frontend espera `retos_virales`.
# Se aceptan ambos slugs al leer para no depender de renombrar registros históricos.
ALIAS_TEMA = {"reto_viral": "retos_virales"}
ZONAS_BD = set(TEMAS) | set(ALIAS_TEMA)


def tema_portal(zona):
    return ALIAS_TEMA.get(zona, zona)


def calcular(jugadores, minimo):
    ids = list(jugadores)
    # Selección explícita de columnas: ni respuestas, nombres, correos ni posibles respuestas.
    mensajes = list(Mensaje.objects.filter(chat__partida__usuario_jugador_id__in=ids)
                    .exclude(opcion_banco_id__isnull=True).exclude(opcion_banco_id="")
                    .values_list("chat__partida__usuario_jugador_id", "opcion_banco_id", "timestamp"))
    opciones = {o.opcion_id: (tema_portal(o.pregunta.zona), o.tipo) for o in OpcionBanco.objects
                .filter(opcion_id__in={m[1] for m in mensajes}, tipo__in=TIPOS, pregunta__zona__in=ZONAS_BD)
                .select_related("pregunta")}
    resultados = defaultdict(lambda: defaultdict(lambda: [0, 0]))
    actualizada = None
    for jugador_id, opcion_id, fecha in mensajes:
        if opcion_id not in opciones:
            continue
        tema, tipo = opciones[opcion_id]
        resultado = resultados[tema][jugador_id]
        resultado[0] += int(tipo in {"segura_basica", "segura_optima"})
        resultado[1] += 1
        actualizada = max(actualizada, fecha) if actualizada else fecha
    completadas = set()
    for jugador_id, zona, fecha in ZonaProgreso.objects.filter(partida__usuario_jugador_id__in=ids, fecha_completada__isnull=False).values_list("partida__usuario_jugador_id", "zona", "fecha_completada"):
        completadas.add((jugador_id, tema_portal(zona)))
        actualizada = max(actualizada, fecha) if actualizada else fecha
    tematicas = []
    for tema in TEMAS:
        valores = resultados[tema]
        if len(valores) < minimo:
            tematicas.append({"tematica": tema, "metricas": None, "motivo": "muestra_insuficiente" if valores else "sin_resultados"})
        else:
            tematicas.append({"tematica": tema, "metricas": {
                "decisiones_seguras": sum(v[0] for v in valores.values()),
                "decisiones_evaluadas": sum(v[1] for v in valores.values()),
                "completada": all((i, tema) in completadas for i in ids),
            }})
    participantes = len({i for valores in resultados.values() for i in valores})
    return tematicas, participantes, actualizada


def sin_cache(datos):
    respuesta = Response(datos)
    respuesta["Cache-Control"] = "no-store"
    return respuesta


@api_view(["GET"])
def reporte_nino(request, jugador_id):
    if request.user.es_profesor:
        raise PermissionDenied("Los profesores solo pueden consultar reportes grupales.")
    nino = get_object_or_404(UsuarioJugador, pk=jugador_id, adulto=request.user)
    temas, _, fecha = calcular([nino.pk], 1)
    return sin_cache({"nino": {"id": nino.pk, "adulto_id": nino.adulto_id, "nombre": nino.nombre,
                              "edad": nino.edad, "actualizado_en": fecha},
                      "actualizado_en": fecha, "tematicas": temas})
