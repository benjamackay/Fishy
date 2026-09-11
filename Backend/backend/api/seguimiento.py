"""Seguimiento privado del curso. Reglas educativas explícitas, no diagnósticos."""
from datetime import timedelta

from django.conf import settings
from django.core.exceptions import ImproperlyConfigured
from django.db.models import F, Window
from django.db.models.functions import RowNumber
from django.utils import timezone
from rest_framework.decorators import api_view

from .invitaciones import grupo_propio
from .models import Mensaje, OpcionBanco
from .reportes_web import TEMAS, TIPOS, sin_cache


def criterios_seguimiento():
    reglas = dict(settings.FISHY_SEGUIMIENTO)
    if (any(type(v) is not int for v in reglas.values())
            or not 0 < reglas["umbral_prioridad"] < reglas["umbral_apoyo"] <= 100
            or not 1 <= reglas["minimo_decisiones"] <= reglas["ventana_decisiones"] <= 100
            or not 1 <= reglas["minimo_tematicas_general"] <= len(TEMAS)
            or not 1 <= reglas["minimo_decisiones_general"] <= reglas["ventana_decisiones"] * len(TEMAS)
            or reglas["dias_datos_antiguos"] < 1):
        raise ImproperlyConfigured("Revisa los criterios de FISHY_SEGUIMIENTO")
    return reglas


def nivel(seguras, evaluadas, reglas):
    # Comparar contadores originales: el redondeo de presentación no decide la alerta.
    if seguras * 100 < evaluadas * reglas["umbral_prioridad"]:
        return "prioritario"
    if seguras * 100 < evaluadas * reglas["umbral_apoyo"]:
        return "apoyo"
    return "sin_alertas"


def evaluar_alumno(miembro, muestras, reglas, ahora):
    temas = []
    for tema in TEMAS:
        datos = muestras.get(tema, [])
        total = len(datos)
        seguras = sum(d[0] for d in datos)
        fecha = max((d[1] for d in datos), default=None)
        suficiente = total >= reglas["minimo_decisiones"]
        estado = nivel(seguras, total, reglas) if suficiente else "muestra_insuficiente" if total else "sin_datos"
        temas.append({"tematica": tema, "estado": estado, "decisiones_evaluadas": total,
                      "decisiones_seguras": seguras if suficiente else None,
                      "porcentaje_seguro": round(seguras * 100 / total, 1) if suficiente else None,
                      "actualizado_en": fecha,
                      "datos_antiguos": any(d[1] < ahora - timedelta(days=reglas["dias_datos_antiguos"]) for d in datos)})
    evaluables = [t for t in temas if t["porcentaje_seguro"] is not None]
    total = sum(t["decisiones_evaluadas"] for t in evaluables)
    seguras = sum(t["decisiones_seguras"] for t in evaluables)
    general = None
    if len(evaluables) >= reglas["minimo_tematicas_general"] and total >= reglas["minimo_decisiones_general"]:
        general = {"estado": nivel(seguras, total, reglas), "decisiones_seguras": seguras,
                   "decisiones_evaluadas": total, "porcentaje_seguro": round(seguras * 100 / total, 1),
                   "tematicas_evaluadas": len(evaluables)}
    niveles = [t["estado"] for t in temas] + ([general["estado"]] if general else [])
    estado = next((n for n in ("prioritario", "apoyo", "sin_alertas", "muestra_insuficiente") if n in niveles), "sin_datos")
    return {"miembro_id": str(miembro.pk), "nombre_nino": miembro.nombre_invitado,
            "email_familia": miembro.jugador.adulto.email, "estado": estado,
            "tematicas": temas, "general": general,
            "actualizado_en": max((t["actualizado_en"] for t in temas if t["actualizado_en"]), default=None)}


@api_view(["GET"])
def seguimiento_grupo(request, grupo_id):
    grupo = grupo_propio(request, grupo_id)
    reglas = criterios_seguimiento()
    ahora = timezone.now()
    miembros = list(grupo.miembros.select_related("jugador__adulto").order_by("fecha_ingreso", "id"))
    ids = [m.jugador_id for m in miembros]
    muestras = {i: {} for i in ids}
    if ids:
        # Solo metadatos de clasificación. No se lee el texto elegido ni el del chat.
        opciones = list(OpcionBanco.objects.filter(tipo__in=TIPOS, pregunta__zona__in=TEMAS)
                        .values_list("opcion_id", "tipo", "pregunta__zona"))
        for tema in TEMAS:
            tipos = {oid: tipo for oid, tipo, zona in opciones if zona == tema}
            if not tipos:
                continue
            # Límite por niño y temática dentro de SQL; no descargar todo su historial.
            recientes = (Mensaje.objects.filter(chat__partida__usuario_jugador_id__in=ids,
                         opcion_banco_id__in=tipos, timestamp__lte=ahora)
                         .annotate(posicion=Window(expression=RowNumber(),
                             partition_by=[F("chat__partida__usuario_jugador_id")],
                             order_by=[F("timestamp").desc(), F("pk").desc()]))
                         .filter(posicion__lte=reglas["ventana_decisiones"])
                         .order_by().values_list("chat__partida__usuario_jugador_id", "opcion_banco_id", "timestamp"))
            for jugador_id, opcion_id, fecha in recientes:
                muestras[jugador_id].setdefault(tema, []).append((tipos[opcion_id] != "insegura", fecha))
    alumnos = [evaluar_alumno(m, muestras[m.jugador_id], reglas, ahora) for m in miembros]
    prioridad = {"prioritario": 0, "apoyo": 1, "muestra_insuficiente": 2, "sin_datos": 3, "sin_alertas": 4}
    alumnos.sort(key=lambda a: (prioridad[a["estado"]], a["nombre_nino"].casefold(), a["miembro_id"]))
    return sin_cache({"grupo_id": str(grupo.pk), "nombre_grupo": grupo.nombre,
                      "consultado_en": ahora, "criterios": reglas, "alumnos": alumnos})
