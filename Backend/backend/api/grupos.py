"""Respuesta del portal mientras el backend conserva el esquema de dev.

Grupos e invitaciones no tienen modelos ni migraciones activos. Se conservan
las rutas para que el frontend muestre indisponibilidad, sin simular un envío
ni consultar tablas inexistentes.
"""
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response


@api_view(["GET", "POST", "DELETE"])
@permission_classes([AllowAny])
def no_disponible(request, **kwargs):
    return Response(
        {"detail": "La gestión de grupos e invitaciones no está habilitada en este backend."},
        status=503,
        headers={"Cache-Control": "no-store"},
    )
