"""Métricas de decisiones clasificadas en el banco y del Modo Detective; jamás serializa conversaciones."""
from collections import defaultdict
from datetime import date, datetime, time, timedelta, timezone as utc
from zoneinfo import ZoneInfo
import re

from django.conf import settings
from django.shortcuts import get_object_or_404
from rest_framework.decorators import api_view
from rest_framework.exceptions import PermissionDenied, ValidationError
from rest_framework.response import Response

from .invitaciones import grupo_visible
from .models import CasoDetectiveProgreso, Mensaje, OpcionBanco, UsuarioJugador, ZonaProgreso

TEMAS = ("desconocidos", "ciberacoso", "retos_virales")
TIPOS = {"segura_basica", "segura_optima", "insegura"}
# El banco, Unity y ZonaProgreso guardan `reto_viral`; el frontend espera `retos_virales`.
# Se aceptan ambos slugs al leer para no depender de renombrar registros históricos.
ALIAS_TEMA = {"reto_viral": "retos_virales"}
ZONAS_BD = set(TEMAS) | set(ALIAS_TEMA)


def tema_portal(zona):
    return ALIAS_TEMA.get(zona, zona)


def leer_periodo(request):
    inicio, fin = request.query_params.get('inicio'), request.query_params.get('fin')
    if inicio is None and fin is None:
        return None
    try:
        if not inicio or not fin or not all(re.fullmatch(r'\d{4}-\d{2}-\d{2}', v) for v in (inicio, fin)):
            raise ValueError
        a, b = date.fromisoformat(inicio), date.fromisoformat(fin)
        if a > b:
            raise ValueError
        zona = ZoneInfo('America/Santiago')
        # Cada medianoche se convierte por separado para respetar cambios de hora.
        limites = (datetime.combine(a, time.min, zona).astimezone(utc.utc),
                   datetime.combine(b + timedelta(days=1), time.min, zona).astimezone(utc.utc))
    except (ValueError, OverflowError):
        raise ValidationError({'code': 'rango_invalido', 'detail': 'Selecciona fechas validas, con inicio anterior o igual a fin.'})
    return {'inicio': inicio, 'fin': fin, 'zona_horaria': 'America/Santiago'}, limites


def calcular(jugadores, minimo, periodo=None):
    ids = list(jugadores)
    # Selección explícita de columnas: ni respuestas, nombres, correos ni posibles respuestas.
    consulta = Mensaje.objects.filter(chat__partida__usuario_jugador_id__in=ids)
    if periodo:
        consulta = consulta.filter(timestamp__gte=periodo[1][0], timestamp__lt=periodo[1][1])
    mensajes = list(consulta
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
    zonas = ZonaProgreso.objects.filter(partida__usuario_jugador_id__in=ids, fecha_completada__isnull=False)
    if periodo:
        # Completitud al final del periodo; nunca incorpora una finalizacion posterior.
        zonas = zonas.filter(fecha_completada__lt=periodo[1][1])
    for jugador_id, zona, fecha in zonas.values_list("partida__usuario_jugador_id", "zona", "fecha_completada"):
        completadas.add((jugador_id, tema_portal(zona)))
        if not periodo or fecha >= periodo[1][0]:
            actualizada = max(actualizada, fecha) if actualizada else fecha
    detective, actualizada = calcular_detective(ids, periodo, actualizada)
    tematicas = []
    for tema in TEMAS:
        valores = resultados[tema]
        if len(valores) < minimo:
            entrada = {"tematica": tema, "metricas": None, "motivo": "muestra_insuficiente" if valores else "sin_resultados"}
        else:
            entrada = {"tematica": tema, "metricas": {
                "decisiones_seguras": sum(v[0] for v in valores.values()),
                "decisiones_evaluadas": sum(v[1] for v in valores.values()),
                "completada": all((i, tema) in completadas for i in ids),
            }}
        # Métrica aparte y no sumada a las decisiones: el chat mide qué elige el niño,
        # el detective si reconoce señales de riesgo en una conversación ajena.
        casos = detective[tema]
        entrada["detective"] = None if len({j for j, _ in casos}) < minimo else {
            "senales_detectadas": sum(v[0] for v in casos.values()),
            "senales_totales": sum(v[1] for v in casos.values()),
            "casos": len(casos),
        }
        tematicas.append(entrada)
    participantes = len({i for valores in resultados.values() for i in valores}
                        | {j for casos in detective.values() for j, _ in casos})
    return tematicas, participantes, actualizada


def calcular_detective(ids, periodo, actualizada):
    """Último intento de cada niño en cada caso, agrupado por temática del caso.

    Dentro de una partida reintentar ya sobrescribe la fila; entre partidas se toma
    también el más reciente, para que repetir un caso no lo cuente dos veces.
    """
    progresos = CasoDetectiveProgreso.objects.filter(
        partida__usuario_jugador_id__in=ids, fecha_termino__isnull=False, caso__zona__in=ZONAS_BD)
    if periodo:
        progresos = progresos.filter(fecha_termino__gte=periodo[1][0], fecha_termino__lt=periodo[1][1])
    detective = defaultdict(dict)
    for jugador_id, caso_id, zona, aciertos, total, fecha in (progresos.order_by("fecha_termino").values_list(
            "partida__usuario_jugador_id", "caso_id", "caso__zona", "aciertos", "total_riesgo", "fecha_termino")):
        detective[tema_portal(zona)][(jugador_id, caso_id)] = (aciertos, total)
        actualizada = max(actualizada, fecha) if actualizada else fecha
    return detective, actualizada


def sin_cache(datos):
    respuesta = Response(datos)
    respuesta["Cache-Control"] = "no-store"
    return respuesta


@api_view(["GET"])
def reporte_grupo(request, grupo_id):
    grupo = grupo_visible(request, grupo_id)
    # No usar jugador__adulto__in: eso incorporaría hermanos de otros cursos.
    ids = list(grupo.miembros.values_list("jugador_id", flat=True))
    minimo = max(3, settings.FISHY_REPORTE_MINIMO)
    periodo = leer_periodo(request)
    temas, participantes, fecha = calcular(ids, minimo, periodo)
    return sin_cache({"grupo_id": str(grupo.pk), "nombre_grupo": grupo.nombre, "total_integrantes": len(ids),
                      "participantes_con_resultados": participantes, "minimo_participantes": minimo,
                      "actualizado_en": fecha, "periodo": periodo[0] if periodo else None, "tematicas": temas})


@api_view(["GET"])
def reporte_nino(request, jugador_id):
    if not request.user.gestiona_menores:
        raise PermissionDenied("Esta cuenta solo puede consultar reportes grupales.")
    nino = get_object_or_404(UsuarioJugador, pk=jugador_id, adulto=request.user)
    periodo = leer_periodo(request)
    temas, _, fecha = calcular([nino.pk], 1, periodo)
    return sin_cache({"nino": {"id": nino.pk, "adulto_id": nino.adulto_id, "nombre": nino.nombre,
                              "edad": nino.edad, "actualizado_en": fecha},
                      "actualizado_en": fecha, "periodo": periodo[0] if periodo else None, "tematicas": temas})
