from rest_framework.decorators import api_view
from rest_framework.response import Response


@api_view(["GET"])
def health_check(request):
    """Endpoint de prueba para verificar que el servidor responde."""
    return Response({"status": "ok"})
