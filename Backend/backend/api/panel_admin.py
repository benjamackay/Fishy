"""Panel del admin del portal: el equipo de Fishy! mirando profesores y grupos.

Por ahora es de LECTURA, más una sola escritura: marcar a una cuenta como
profesor o devolverla a padre. Crear, borrar grupos o quitar integrantes siguen
siendo del profesor dueño (`grupo_propio`); abrirlos al admin después es usar
`grupo_visible` en esas vistas.

El admin tampoco ve datos por niño: ni `jugadores/`, ni el reporte individual,
ni el seguimiento del grupo (nombre, correo de la familia y nivel de riesgo de
cada alumno). Sí ve el detalle del grupo y su reporte agregado, igual que el
profesor dueño.
"""
from django.db import transaction
from django.db.models import Count, Q
from django.shortcuts import get_object_or_404
from rest_framework import serializers
from rest_framework.decorators import api_view
from rest_framework.exceptions import PermissionDenied, ValidationError
from rest_framework.response import Response

from .invitaciones import Conflicto, datos_grupo, validar
from .models import AdultoResponsable, GrupoTutor

# Lo que el panel puede asignar. `admin` queda fuera a propósito: se da solo
# desde /admin/ de Django, para que nadie se autoascienda desde el portal.
ROLES_ASIGNABLES = (AdultoResponsable.ROL_PADRE, AdultoResponsable.ROL_PROFESOR)


class DatosRol(serializers.Serializer):
    rol = serializers.ChoiceField(choices=ROLES_ASIGNABLES)


def admin_portal(request):
    if not request.user.es_admin_portal:
        raise PermissionDenied("Solo el equipo de Fishy! puede usar este panel.")


def datos_cuenta(cuenta):
    return {"id": cuenta.pk, "nombre": cuenta.nombre, "apellido": cuenta.apellido,
            "email": cuenta.email, "rol": cuenta.rol, "fecha_creacion": cuenta.fecha_creacion}


def datos_profesor(cuenta):
    return {"id": cuenta.pk, "nombre": cuenta.nombre, "apellido": cuenta.apellido, "email": cuenta.email}


@api_view(["GET"])
def profesores(request):
    """Todos los profesores, con cuántos grupos tiene cada uno."""
    admin_portal(request)
    qs = (AdultoResponsable.objects.filter(rol=AdultoResponsable.ROL_PROFESOR)
          .annotate(total_grupos=Count("grupos")).order_by("nombre"))
    return Response([{**datos_cuenta(p), "total_grupos": p.total_grupos} for p in qs])


@api_view(["GET"])
def grupos(request):
    """Todos los grupos con su profesor. `?profesor=<id>` filtra por uno."""
    admin_portal(request)
    # prefetch: `datos_grupo` cuenta los miembros de cada grupo, y así no es una consulta por grupo.
    qs = GrupoTutor.objects.select_related("tutor").prefetch_related("miembros").order_by("-fecha_creacion")
    profesor_id = request.query_params.get("profesor")
    if profesor_id is not None:
        if not profesor_id.isdigit():
            raise ValidationError({"profesor": "Debe ser el id numérico de un profesor."})
        qs = qs.filter(tutor_id=int(profesor_id))
    return Response([{**datos_grupo(g), "profesor": datos_profesor(g.tutor)} for g in qs])


@api_view(["GET"])
def cuentas(request):
    """Busca una cuenta por correo o nombre de usuario EXACTOS, para marcarla como
    profesor. Exacto a propósito: el panel no es un listado de todas las familias."""
    admin_portal(request)
    buscar = request.query_params.get("buscar", "").strip()
    if not buscar:
        raise ValidationError({"buscar": "Indica el correo o el nombre de usuario exacto."})
    qs = (AdultoResponsable.objects.filter(Q(email__iexact=buscar) | Q(nombre__iexact=buscar))
          .annotate(total_perfiles=Count("jugadores", distinct=True), total_grupos=Count("grupos", distinct=True))
          .order_by("nombre"))
    return Response([{**datos_cuenta(c), "total_perfiles": c.total_perfiles, "total_grupos": c.total_grupos}
                     for c in qs])


@api_view(["PATCH"])
def cambiar_rol(request, cuenta_id):
    """Cambia una cuenta entre `padre` y `profesor`. Body: `{"rol": "profesor"}`."""
    admin_portal(request)
    nuevo = validar(DatosRol, request.data)["rol"]
    with transaction.atomic():
        cuenta = get_object_or_404(AdultoResponsable.objects.select_for_update(), pk=cuenta_id)
        if cuenta.pk == request.user.pk:
            raise PermissionDenied("No puedes cambiar tu propio rol.")
        if cuenta.es_admin_portal:
            raise PermissionDenied("Las cuentas admin se gestionan desde el administrador de Django.")
        if cuenta.rol != nuevo:
            # Una cuenta tiene un solo rol. Pasar de un lado a otro con cosas a
            # cuestas las dejaría huérfanas: los niños, invisibles para su
            # familia; los grupos, sin nadie que los pueda gestionar.
            if nuevo == AdultoResponsable.ROL_PROFESOR and cuenta.jugadores.exists():
                raise Conflicto("La cuenta tiene perfiles de niños. Un profesor no puede tener perfiles, "
                                "así que primero hay que eliminarlos o usar otra cuenta.")
            if nuevo == AdultoResponsable.ROL_PADRE and cuenta.grupos.exists():
                raise Conflicto("La cuenta todavía tiene grupos. Primero hay que eliminarlos.")
            cuenta.rol = nuevo
            cuenta.save(update_fields=["rol"])
    return Response(datos_cuenta(cuenta))
