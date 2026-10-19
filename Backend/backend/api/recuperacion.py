"""Recuperar la contraseña con un código de 6 dígitos enviado al correo de la cuenta.

Tres pasos: `solicitar` manda el código, `verificar` lo canjea por un permiso de un
solo uso y `cambiar` fija la contraseña nueva con ese permiso. Un código de 6
dígitos son solo un millón de combinaciones, así que la seguridad está en los
límites: vence en minutos, se anula al quinto error y los envíos tienen tope.

`solicitar` responde lo mismo exista o no la cuenta, para no servir de buscador
de correos de familias. (El registro igual delata un correo repetido; esto no lo
empeora.)
"""
import hashlib
import hmac
import logging
import secrets
from datetime import timedelta

from django.conf import settings
from django.contrib.auth.password_validation import validate_password
from django.core.exceptions import ValidationError as ErrorDeDjango
from django.core.mail import EmailMultiAlternatives
from django.db import transaction
from django.db.models import F
from django.template.loader import render_to_string
from django.utils import timezone
from rest_framework import serializers
from rest_framework.authtoken.models import Token
from rest_framework.decorators import api_view, authentication_classes, permission_classes, throttle_classes
from rest_framework.exceptions import ValidationError
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from rest_framework.throttling import SimpleRateThrottle

from .invitaciones import NoDisponible, correo_configurado, huella, validar
from .models import AdultoResponsable, CodigoRecuperacion

logger = logging.getLogger(__name__)

RESPUESTA_SOLICITUD = "Si el correo está registrado en Fishy, te enviamos un código. Revisa también la carpeta de spam."
CODIGO_INVALIDO = "El código no es correcto o ya venció. Revisa el correo o pide uno nuevo."
PERMISO_INVALIDO = "El plazo para cambiar la contraseña terminó. Pide un código nuevo."
ESPERA_ENTRE_CODIGOS = timedelta(seconds=60)


class LimitePorIP(SimpleRateThrottle):
    scope = "recuperacion_ip"
    rate = "20/hour"

    def get_cache_key(self, request, view):
        return self.cache_format % {"scope": self.scope, "ident": self.get_ident(request)}


class LimitePorCorreo(SimpleRateThrottle):
    # Que nadie use el formulario para llenarle la bandeja a otra persona.
    scope = "recuperacion_correo"
    rate = "5/hour"

    def get_cache_key(self, request, view):
        email = str(request.data.get("email", "")).strip().lower()
        return self.cache_format % {"scope": self.scope, "ident": huella(email)} if email else None


class DatosSolicitud(serializers.Serializer):
    email = serializers.EmailField(max_length=254)


class DatosCodigo(DatosSolicitud):
    codigo = serializers.RegexField(r"^\d{6}$", max_length=6)


class DatosCambio(serializers.Serializer):
    permiso = serializers.RegexField(r"^[A-Za-z0-9_-]{43}$", max_length=43)
    password = serializers.CharField(max_length=128, trim_whitespace=False)


def firma(codigo_id, codigo):
    # El id va dentro: el mismo código en dos filas no deja la misma huella.
    return hmac.new(settings.SECRET_KEY.encode(), f"{codigo_id}:{codigo}".encode(), hashlib.sha256).hexdigest()


def cuenta_de(email):
    # El correo es único, pero el registro no lo normaliza a minúsculas: si hubiera
    # dos que solo difieren en mayúsculas, no se adivina a cuál mandar.
    cuentas = list(AdultoResponsable.objects.filter(email__iexact=email)[:2])
    return cuentas[0] if len(cuentas) == 1 else None


def sin_cache(respuesta):
    respuesta["Cache-Control"] = "no-store"
    return respuesta


def enviar_codigo(adulto, codigo):
    contexto = {"nombre": adulto.nombre, "codigo": codigo, "minutos": settings.FISHY_RECUPERACION_MINUTOS}
    mensaje = EmailMultiAlternatives(
        "Fishy: tu código para recuperar la contraseña", render_to_string("api/recuperacion.txt", contexto),
        settings.DEFAULT_FROM_EMAIL, [adulto.email])
    mensaje.attach_alternative(render_to_string("api/recuperacion.html", contexto), "text/html")
    if mensaje.send(fail_silently=False) != 1:
        raise OSError("El servicio de correo no aceptó el mensaje")


@api_view(["POST"])
@authentication_classes([])
@permission_classes([AllowAny])
@throttle_classes([LimitePorIP, LimitePorCorreo])
def solicitar(request):
    email = validar(DatosSolicitud, request.data)["email"]
    if not correo_configurado():
        raise NoDisponible("La recuperación de contraseña aún no está disponible. Contacta al equipo de Fishy.")
    adulto = cuenta_de(email)
    if adulto is None:
        return sin_cache(Response({"detalle": RESPUESTA_SOLICITUD}, status=202))
    ahora = timezone.now()
    with transaction.atomic():
        AdultoResponsable.objects.select_for_update().get(pk=adulto.pk)  # serializa pedidos simultáneos
        if adulto.codigos_recuperacion.filter(fecha_creacion__gt=ahora - ESPERA_ENTRE_CODIGOS).exists():
            # El anterior aún está llegando; otro código solo confundiría cuál vale.
            return sin_cache(Response({"detalle": RESPUESTA_SOLICITUD}, status=202))
        # Pedir un código nuevo anula los anteriores y cualquier permiso sin usar.
        adulto.codigos_recuperacion.all().delete()
        codigo = f"{secrets.randbelow(10**6):06d}"
        fila = CodigoRecuperacion.objects.create(
            adulto=adulto, codigo_hash="", vence_en=ahora + timedelta(minutes=settings.FISHY_RECUPERACION_MINUTOS))
        fila.codigo_hash = firma(fila.pk, codigo)
        fila.save(update_fields=["codigo_hash"])
    try:
        enviar_codigo(adulto, codigo)
    except Exception:
        # No registrar el correo, el código ni la excepción del proveedor.
        logger.warning("No se pudo enviar el código de recuperación %s", fila.pk)
        fila.delete()
        raise NoDisponible("No pudimos enviar el correo. Intenta de nuevo en unos minutos.")
    return sin_cache(Response({"detalle": RESPUESTA_SOLICITUD}, status=202))


@api_view(["POST"])
@authentication_classes([])
@permission_classes([AllowAny])
@throttle_classes([LimitePorIP])
def verificar(request):
    datos = validar(DatosCodigo, request.data)
    adulto = cuenta_de(datos["email"])
    if adulto is None:
        raise ValidationError(CODIGO_INVALIDO)
    with transaction.atomic():
        fila = (CodigoRecuperacion.objects.select_for_update()
                .filter(adulto=adulto, permiso_hash__isnull=True, vence_en__gt=timezone.now(),
                        intentos__lt=settings.FISHY_RECUPERACION_INTENTOS)
                .first())
        if fila is None:
            raise ValidationError(CODIGO_INVALIDO)
        if not hmac.compare_digest(fila.codigo_hash, firma(fila.pk, datos["codigo"])):
            CodigoRecuperacion.objects.filter(pk=fila.pk).update(intentos=F("intentos") + 1)
            # El error se responde fuera del atomic: si no, el rollback deshace el intento.
            fallo = True
        else:
            fallo = False
            permiso = secrets.token_urlsafe(32)
            fila.permiso_hash = huella(permiso)
            fila.permiso_vence_en = timezone.now() + timedelta(minutes=settings.FISHY_RECUPERACION_PERMISO_MINUTOS)
            fila.save(update_fields=["permiso_hash", "permiso_vence_en"])
    if fallo:
        raise ValidationError(CODIGO_INVALIDO)
    return sin_cache(Response({"permiso": permiso, "vence_en": fila.permiso_vence_en}))


@api_view(["POST"])
@authentication_classes([])
@permission_classes([AllowAny])
@throttle_classes([LimitePorIP])
def cambiar(request):
    datos = validar(DatosCambio, request.data)
    with transaction.atomic():
        fila = (CodigoRecuperacion.objects.select_for_update().select_related("adulto")
                .filter(permiso_hash=huella(datos["permiso"]), permiso_vence_en__gt=timezone.now())
                .first())
        if fila is None:
            raise ValidationError(PERMISO_INVALIDO)
        adulto = fila.adulto
        try:
            validate_password(datos["password"], adulto)
        except ErrorDeDjango as e:
            # El permiso sigue vigente: puede corregir la contraseña sin pedir otro código.
            raise ValidationError({"password": list(e.messages)})
        adulto.set_password(datos["password"])
        adulto.save(update_fields=["password"])
        adulto.codigos_recuperacion.all().delete()
        # Cierra las sesiones abiertas (juego y portal): si alguien más tenía acceso, lo pierde.
        Token.objects.filter(user=adulto).delete()
    return sin_cache(Response({"detalle": "Listo. Ya puedes iniciar sesión con tu contraseña nueva."}))
