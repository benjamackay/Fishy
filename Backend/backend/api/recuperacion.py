"""Recuperar la contraseña con un código de 6 dígitos enviado al correo de la cuenta.

Contrato acordado con el portal (docs/INFORME_INTEGRACION_BACKEND_TRES_FEATURES.md,
sección 4): `solicitar` manda el código y devuelve un `solicitud_id` opaco,
`verificar` canjea solicitud + código por un `reset_token` de un solo uso y
`restablecer` fija la contraseña nueva con ese token. Un código de 6 dígitos son
solo un millón de combinaciones, así que la seguridad está en los límites: vence
en minutos, se anula al quinto error y los envíos tienen tope.

`solicitar` se ve igual desde afuera exista o no la cuenta: misma respuesta, un
`solicitud_id` también para correos sin cuenta y el correo sale en segundo plano,
así que ni el tiempo de respuesta ni una caída del proveedor delatan la cuenta.
(El registro igual delata un correo repetido; esto no lo empeora.)
"""
import hashlib
import hmac
import logging
import secrets
import threading
import uuid
from datetime import timedelta

from django.conf import settings
from django.contrib.auth.password_validation import validate_password
from django.core.cache import cache
from django.core.exceptions import ValidationError as ErrorDeDjango
from django.core.mail import EmailMultiAlternatives
from django.db import connection, transaction
from django.db.models import F
from django.template.loader import render_to_string
from django.utils import timezone
from rest_framework import serializers
from rest_framework.authtoken.models import Token
from rest_framework.decorators import api_view, authentication_classes, permission_classes, throttle_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from rest_framework.throttling import SimpleRateThrottle

from .invitaciones import correo_configurado, huella
from .models import AdultoResponsable, CodigoRecuperacion

logger = logging.getLogger(__name__)

RESPUESTA_SOLICITUD = "Si existe una cuenta asociada, recibirás un código en tu correo. Revisa también la carpeta de spam."
CODIGO_INVALIDO = "El código no es correcto o ya venció. Revisa el correo o pide uno nuevo."
TOKEN_INVALIDO = "El plazo para cambiar la contraseña terminó. Pide un código nuevo."


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


class DatosCodigo(serializers.Serializer):
    solicitud_id = serializers.UUIDField()
    codigo = serializers.RegexField(r"^\d{6}$", max_length=6)


class DatosRestablecer(serializers.Serializer):
    reset_token = serializers.RegexField(r"^[A-Za-z0-9_-]{43}$", max_length=43)
    password_nueva = serializers.CharField(max_length=128, trim_whitespace=False)
    password_confirmacion = serializers.CharField(max_length=128, trim_whitespace=False)


def error(code, detail, errors=None, status=400, headers=None):
    """Formato de error propuesto por el portal: {code, detail, errors}."""
    cuerpo = {"code": code, "detail": detail}
    if errors:
        cuerpo["errors"] = errors
    return sin_cache(Response(cuerpo, status=status, headers=headers))


def leer(clase, data):
    serializer = clase(data=data)
    if serializer.is_valid():
        return serializer.validated_data, None
    return None, error("datos_invalidos", "Revisa los datos enviados.", serializer.errors)


def sin_cache(respuesta):
    respuesta["Cache-Control"] = "no-store"
    return respuesta


def segundos(minutos):
    return int(timedelta(minutes=minutos).total_seconds())


def firma(codigo_id, codigo):
    # El id va dentro: el mismo código en dos filas no deja la misma huella.
    return hmac.new(settings.SECRET_KEY.encode(), f"{codigo_id}:{codigo}".encode(), hashlib.sha256).hexdigest()


def cuenta_de(email):
    # El correo es único, pero el registro no lo normaliza a minúsculas: si hubiera
    # dos que solo difieren en mayúsculas, no se adivina a cuál mandar.
    cuentas = list(AdultoResponsable.objects.filter(email__iexact=email)[:2])
    return cuentas[0] if len(cuentas) == 1 else None


def despachar(tarea):
    """Corre la tarea en otro hilo. Los tests la reemplazan para correrla en línea."""
    def correr():
        try:
            tarea()
        finally:
            connection.close()  # el hilo abrió su propia conexión
    threading.Thread(target=correr, daemon=True).start()


def enviar_codigo(fila_id, codigo):
    try:
        fila = CodigoRecuperacion.objects.select_related("adulto").get(pk=fila_id)
        contexto = {"nombre": fila.adulto.nombre, "codigo": codigo, "minutos": settings.FISHY_RECUPERACION_MINUTOS}
        mensaje = EmailMultiAlternatives(
            "Fishy: tu código para recuperar la contraseña", render_to_string("api/recuperacion.txt", contexto),
            settings.DEFAULT_FROM_EMAIL, [fila.adulto.email])
        mensaje.attach_alternative(render_to_string("api/recuperacion.html", contexto), "text/html")
        if mensaje.send(fail_silently=False) != 1:
            raise OSError("El servicio de correo no aceptó el mensaje")
    except CodigoRecuperacion.DoesNotExist:
        pass  # otra solicitud lo reemplazó antes de salir
    except Exception:
        # No registrar el correo, el código ni la excepción del proveedor.
        logger.warning("No se pudo enviar el código de recuperación %s", fila_id)
        CodigoRecuperacion.objects.filter(pk=fila_id).delete()


@api_view(["POST"])
@authentication_classes([])
@permission_classes([AllowAny])
@throttle_classes([LimitePorIP, LimitePorCorreo])
def solicitar(request):
    datos, fallo = leer(DatosSolicitud, request.data)
    if fallo:
        return fallo
    if not correo_configurado():
        return error("correo_no_disponible", "La recuperación de contraseña aún no está disponible. Contacta al equipo de Fishy.", status=503)
    reenvio = settings.FISHY_RECUPERACION_REENVIO_SEGUNDOS
    # La espera va por correo y vale igual para correos sin cuenta: si no, pedir dos
    # veces seguidas distinguiría unos de otros.
    if not cache.add("recuperacion_reenvio:" + huella(datos["email"].lower()), True, reenvio):
        return error("reenvio_muy_pronto", f"Espera {reenvio} segundos antes de pedir otro código.",
                     status=429, headers={"Retry-After": str(reenvio)})
    adulto = cuenta_de(datos["email"])
    solicitud_id = uuid.uuid4()
    if adulto is not None:
        codigo = f"{secrets.randbelow(10**6):06d}"
        with transaction.atomic():
            AdultoResponsable.objects.select_for_update().get(pk=adulto.pk)  # serializa pedidos simultáneos
            # Pedir un código nuevo anula los anteriores y cualquier reset_token sin usar.
            adulto.codigos_recuperacion.all().delete()
            fila = CodigoRecuperacion.objects.create(
                adulto=adulto, solicitud_id=solicitud_id, codigo_hash="",
                vence_en=timezone.now() + timedelta(minutes=settings.FISHY_RECUPERACION_MINUTOS))
            fila.codigo_hash = firma(fila.pk, codigo)
            fila.save(update_fields=["codigo_hash"])
            transaction.on_commit(lambda: despachar(lambda: enviar_codigo(fila.pk, codigo)))
    return sin_cache(Response({
        "detail": RESPUESTA_SOLICITUD,
        "solicitud_id": str(solicitud_id),
        "expira_en_segundos": segundos(settings.FISHY_RECUPERACION_MINUTOS),
        "reenvio_en_segundos": reenvio,
    }, status=202))


@api_view(["POST"])
@authentication_classes([])
@permission_classes([AllowAny])
@throttle_classes([LimitePorIP])
def verificar(request):
    datos, fallo = leer(DatosCodigo, request.data)
    if fallo:
        return fallo
    with transaction.atomic():
        fila = (CodigoRecuperacion.objects.select_for_update()
                .filter(solicitud_id=datos["solicitud_id"], permiso_hash__isnull=True, vence_en__gt=timezone.now(),
                        intentos__lt=settings.FISHY_RECUPERACION_INTENTOS)
                .first())
        if fila is None:
            return error("codigo_invalido", CODIGO_INVALIDO)
        if not hmac.compare_digest(fila.codigo_hash, firma(fila.pk, datos["codigo"])):
            fila.intentos = F("intentos") + 1
            fila.save(update_fields=["intentos"])
            return error("codigo_invalido", CODIGO_INVALIDO)
        reset_token = secrets.token_urlsafe(32)
        fila.permiso_hash = huella(reset_token)
        fila.permiso_vence_en = timezone.now() + timedelta(minutes=settings.FISHY_RECUPERACION_PERMISO_MINUTOS)
        fila.save(update_fields=["permiso_hash", "permiso_vence_en"])
    return sin_cache(Response({"reset_token": reset_token,
                               "expira_en_segundos": segundos(settings.FISHY_RECUPERACION_PERMISO_MINUTOS)}))


@api_view(["POST"])
@authentication_classes([])
@permission_classes([AllowAny])
@throttle_classes([LimitePorIP])
def restablecer(request):
    datos, fallo = leer(DatosRestablecer, request.data)
    if fallo:
        return fallo
    with transaction.atomic():
        fila = (CodigoRecuperacion.objects.select_for_update().select_related("adulto")
                .filter(permiso_hash=huella(datos["reset_token"]), permiso_vence_en__gt=timezone.now())
                .first())
        if fila is None:
            return error("reset_token_invalido", TOKEN_INVALIDO)
        # El token sigue vigente en estos dos errores: puede corregir sin pedir otro código.
        if datos["password_nueva"] != datos["password_confirmacion"]:
            return error("password_invalida", "Las contraseñas no coinciden.",
                         {"password_confirmacion": ["Debe ser igual a la contraseña nueva."]})
        adulto = fila.adulto
        try:
            validate_password(datos["password_nueva"], adulto)
        except ErrorDeDjango as e:
            return error("password_invalida", "La contraseña nueva no es segura.", {"password_nueva": list(e.messages)})
        adulto.set_password(datos["password_nueva"])
        adulto.save(update_fields=["password"])
        adulto.codigos_recuperacion.all().delete()
        # Cierra las sesiones abiertas (juego y portal): si alguien más tenía acceso, lo pierde.
        Token.objects.filter(user=adulto).delete()
    return sin_cache(Response({"detail": "Contraseña actualizada. Inicia sesión nuevamente.", "requiere_login": True}))
