"""
Token de DRF con vencimiento.

El token de DRF no vence nunca: uno robado (de un log, un PC compartido, el
almacenamiento del navegador) sirve para siempre. Aquí vence a los
`FISHY_TOKEN_DIAS` días de creado, y el cliente vuelve al login al recibir 401.

Es un solo token por cuenta, compartido por el juego y el portal (los dos
entran con la cuenta del adulto). Por eso el login NO crea uno nuevo cada vez:
que el apoderado entrara al portal le cortaría la sesión al juego a mitad de
partida. Lo renueva solo cuando ya pasó la mitad de su vida, así un login
siempre entrega un token con al menos la mitad del plazo por delante (Unity lo
guarda solo en memoria y lo pide en cada arranque: nunca se topa con el
vencimiento dentro de una partida).
"""
from datetime import timedelta

from django.conf import settings
from django.utils import timezone
from rest_framework.authentication import TokenAuthentication
from rest_framework.authtoken.models import Token
from rest_framework.exceptions import AuthenticationFailed


def duracion():
    return timedelta(days=settings.FISHY_TOKEN_DIAS)


class TokenConVencimiento(TokenAuthentication):
    def authenticate_credentials(self, key):
        usuario, token = super().authenticate_credentials(key)
        if timezone.now() >= token.created + duracion():
            raise AuthenticationFailed("La sesión venció. Vuelve a iniciar sesión.")
        return usuario, token


def token_para(adulto):
    """El token de la cuenta: el mismo mientras no pase la mitad de su vida,
    uno nuevo después. Dos logins simultáneos no chocan: el borrado solo toca
    tokens viejos, y get_or_create resuelve la carrera al crear."""
    Token.objects.filter(user=adulto, created__lte=timezone.now() - duracion() / 2).delete()
    return Token.objects.get_or_create(user=adulto)[0]
