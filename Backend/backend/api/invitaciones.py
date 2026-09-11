"""Invitaciones a un niño concreto. El enlace no concede acceso sin la cuenta destinataria."""
import hashlib
import logging
import secrets
import unicodedata
from datetime import timedelta
from urllib.parse import urlsplit

from django.conf import settings
from django.core.mail import EmailMultiAlternatives
from django.db import transaction
from django.shortcuts import get_object_or_404
from django.template.loader import render_to_string
from django.utils import timezone
from rest_framework import serializers
from rest_framework.decorators import api_view, permission_classes, throttle_classes
from rest_framework.exceptions import APIException, PermissionDenied, ValidationError
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from rest_framework.throttling import SimpleRateThrottle

from .models import AdultoResponsable, GrupoTutor, InvitacionGrupo, MiembroGrupo, UsuarioJugador

logger = logging.getLogger(__name__)


class Conflicto(APIException):
    status_code = 409


class NoDisponible(APIException):
    status_code = 503


class LimiteInvitaciones(SimpleRateThrottle):
    scope = "invitaciones"
    rate = "30/hour"

    def get_cache_key(self, request, view):
        return self.cache_format % {"scope": self.scope, "ident": request.user.pk if request.user.is_authenticated else self.get_ident(request)}


def nombre_clave(nombre):
    return " ".join(unicodedata.normalize("NFC", nombre).casefold().split())


def huella(token):
    return hashlib.sha256(token.encode()).hexdigest()


def profesor(request):
    if not request.user.is_admin:
        raise PermissionDenied("Solo los profesores pueden gestionar grupos.")


def grupo_propio(request, grupo_id, bloquear=False):
    profesor(request)
    qs = GrupoTutor.objects.select_for_update() if bloquear else GrupoTutor.objects
    return get_object_or_404(qs, pk=grupo_id, tutor=request.user)


class DatosGrupo(serializers.Serializer):
    nombre = serializers.CharField(max_length=80)
    descripcion = serializers.CharField(max_length=280, required=False, allow_blank=True, default="")


class DatosInvitacion(serializers.Serializer):
    email = serializers.EmailField(max_length=254)
    nombre_nino = serializers.CharField(max_length=150)


class DatosToken(serializers.Serializer):
    token = serializers.RegexField(r"^[A-Za-z0-9_-]{43}$", max_length=43)


class DatosAceptacion(DatosToken):
    jugador_id = serializers.IntegerField(min_value=1, required=False)
    crear_perfil = serializers.BooleanField(default=False)
    confirmar = serializers.BooleanField()

    def validate(self, attrs):
        if not attrs["confirmar"]:
            raise ValidationError("Confirma que se trata del niño de la invitación.")
        if bool(attrs.get("jugador_id")) == attrs["crear_perfil"]:
            raise ValidationError("Selecciona un perfil o crea el perfil del niño invitado.")
        return attrs


def validar(clase, data):
    serializer = clase(data=data)
    serializer.is_valid(raise_exception=True)
    return serializer.validated_data


def datos_invitacion(inv):
    estado = "vencida" if inv.estado == "pendiente" and inv.vence_en <= timezone.now() else inv.estado
    return {"id": str(inv.pk), "email": inv.email, "nombre_nino": inv.nombre_nino,
            "estado": estado, "estado_envio": inv.estado_envio, "vence_en": inv.vence_en,
            "fecha_creacion": inv.fecha_creacion, "enviada_en": inv.enviada_en}


def datos_grupo(grupo, detalle=False):
    data = {"id": str(grupo.pk), "nombre": grupo.nombre, "descripcion": grupo.descripcion,
            "fecha_creacion": grupo.fecha_creacion, "total_miembros": grupo.miembros.count()}
    if detalle:
        data["miembros"] = [{"id": str(m.pk), "email": m.jugador.adulto.email,
                             "nombre_nino": m.nombre_invitado, "fecha_ingreso": m.fecha_ingreso}
                            for m in grupo.miembros.select_related("jugador__adulto").order_by("fecha_ingreso")]
        data["invitaciones"] = [datos_invitacion(i) for i in grupo.invitaciones.order_by("-fecha_creacion")]
    return data


def comprobar_correo():
    if not settings.FISHY_EMAIL_ENABLED or not settings.DEFAULT_FROM_EMAIL:
        raise NoDisponible("El envío de invitaciones aún no está configurado. Contacta al equipo de Fishy.")
    if settings.EMAIL_BACKEND == "django.core.mail.backends.smtp.EmailBackend" and not settings.EMAIL_HOST:
        raise NoDisponible("El envío de invitaciones aún no está configurado.")
    url = urlsplit(settings.FISHY_WEB_URL)
    if not url.netloc or url.query or url.fragment or url.username or (url.scheme != "https" and not (settings.DEBUG and url.scheme == "http" and url.hostname in {"localhost", "127.0.0.1"})):
        raise NoDisponible("La dirección del sitio para las invitaciones no está configurada correctamente.")


def entregar(inv, token):
    """Se llama después de confirmar la fila en BD. Los fallos quedan visibles y permiten reenvío."""
    enlace = settings.FISHY_WEB_URL + "/invitacion#" + token
    contexto = {"grupo": inv.grupo.nombre, "profesor": str(inv.grupo.tutor), "nombre_nino": inv.nombre_nino,
                "enlace": enlace, "dias": settings.FISHY_INVITACION_DIAS}
    mensaje = EmailMultiAlternatives(
        "Fishy: invitación a un curso", render_to_string("api/invitacion.txt", contexto),
        settings.DEFAULT_FROM_EMAIL, [inv.email])
    mensaje.attach_alternative(render_to_string("api/invitacion.html", contexto), "text/html")
    try:
        if mensaje.send(fail_silently=False) != 1:
            raise OSError("El servicio de correo no aceptó el mensaje")
    except Exception:
        # No registrar el correo, el nombre, el enlace ni la excepción del proveedor.
        logger.warning("No se pudo enviar la invitación %s", inv.pk)
        InvitacionGrupo.objects.filter(pk=inv.pk, token_hash=huella(token), estado="pendiente").update(estado_envio="fallido")
        raise NoDisponible("No pudimos enviar el correo. La invitación quedó guardada; puedes reenviarla desde el grupo.")
    InvitacionGrupo.objects.filter(pk=inv.pk, token_hash=huella(token), estado="pendiente").update(estado_envio="enviado", enviada_en=timezone.now())
    inv.refresh_from_db()
    return Response(datos_invitacion(inv), status=201)


@api_view(["GET", "POST"])
def grupos(request):
    profesor(request)
    if request.method == "GET":
        return Response([datos_grupo(g) for g in request.user.grupos.order_by("-fecha_creacion")])
    return Response(datos_grupo(GrupoTutor.objects.create(tutor=request.user, **validar(DatosGrupo, request.data))), status=201)


@api_view(["GET", "DELETE"])
def grupo_detalle(request, grupo_id):
    with transaction.atomic():
        grupo = grupo_propio(request, grupo_id, bloquear=request.method == "DELETE")
        if request.method == "GET":
            return Response(datos_grupo(grupo, detalle=True))
        grupo.delete()
    return Response(status=204)


@api_view(["DELETE"])
def quitar_miembro(request, grupo_id, miembro_id):
    with transaction.atomic():
        grupo = grupo_propio(request, grupo_id, bloquear=True)
        get_object_or_404(MiembroGrupo, pk=miembro_id, grupo=grupo).delete()
    return Response(status=204)


@api_view(["POST"])
@throttle_classes([LimiteInvitaciones])
def invitar(request, grupo_id):
    profesor(request)
    datos = validar(DatosInvitacion, request.data)
    comprobar_correo()
    email = datos["email"].lower()
    clave = nombre_clave(datos["nombre_nino"])
    token = secrets.token_urlsafe(32)
    ahora = timezone.now()
    with transaction.atomic():
        grupo = grupo_propio(request, grupo_id, bloquear=True)
        if any(nombre_clave(m.nombre_invitado) == clave for m in grupo.miembros.filter(jugador__adulto__email__iexact=email)):
            raise Conflicto("Ese niño ya forma parte del grupo con este padre o madre.")
        pendientes = grupo.invitaciones.filter(email=email, nombre_clave=clave, estado="pendiente")
        if pendientes.exists():
            raise Conflicto("Ya existe una invitación para ese niño y correo. Reenvíala o cancélala desde el grupo.")
        inv = InvitacionGrupo.objects.create(grupo=grupo, email=email, nombre_nino=datos["nombre_nino"],
             nombre_clave=clave, token_hash=huella(token), ultimo_intento=ahora,
             vence_en=ahora + timedelta(days=settings.FISHY_INVITACION_DIAS))
    return entregar(inv, token)


@api_view(["POST", "DELETE"])
@throttle_classes([LimiteInvitaciones])
def gestionar_invitacion(request, grupo_id, invitacion_id):
    with transaction.atomic():
        grupo = grupo_propio(request, grupo_id, bloquear=True)
        inv = get_object_or_404(InvitacionGrupo.objects.select_for_update(), pk=invitacion_id, grupo=grupo)
        if inv.estado != "pendiente":
            raise Conflicto("Esta invitación ya fue aceptada o cancelada.")
        if request.method == "DELETE":
            inv.estado = "cancelada"
            inv.save(update_fields=["estado"])
            return Response(status=204)
        comprobar_correo()
        ahora = timezone.now()
        if ahora - inv.ultimo_intento < timedelta(seconds=60):
            raise Conflicto("Espera un minuto antes de volver a enviar esta invitación.")
        token = secrets.token_urlsafe(32)
        inv.token_hash = huella(token)
        inv.estado_envio = "enviando"
        inv.enviada_en = None
        inv.ultimo_intento = ahora
        inv.vence_en = ahora + timedelta(days=settings.FISHY_INVITACION_DIAS)
        inv.save()
    return entregar(inv, token)


def invitacion_vigente(inv):
    if inv.estado != "pendiente" or inv.vence_en <= timezone.now() or inv.estado_envio != "enviado":
        raise Conflicto("Esta invitación ya no está disponible. Puede haber vencido, sido aceptada o cancelada. Pide al profesor un nuevo envío.")


@api_view(["POST"])
@permission_classes([AllowAny])
@throttle_classes([LimiteInvitaciones])
def consultar_invitacion(request):
    token = validar(DatosToken, request.data)["token"]
    inv = get_object_or_404(InvitacionGrupo.objects.select_related("grupo__tutor"), token_hash=huella(token))
    invitacion_vigente(inv)
    respuesta = Response({"grupo": inv.grupo.nombre, "profesor": str(inv.grupo.tutor),
                         "nombre_nino": inv.nombre_nino, "email": inv.email, "vence_en": inv.vence_en})
    respuesta["Cache-Control"] = "no-store"
    respuesta["Referrer-Policy"] = "no-referrer"
    return respuesta


@api_view(["POST"])
@throttle_classes([LimiteInvitaciones])
def aceptar_invitacion(request):
    if request.user.is_admin:
        raise PermissionDenied("Acepta la invitación con la cuenta del padre o madre destinatario.")
    datos = validar(DatosAceptacion, request.data)
    inicial = get_object_or_404(InvitacionGrupo, token_hash=huella(datos["token"]))
    with transaction.atomic():
        # Orden de bloqueos común con quitar, cancelar y reenviar; serializa aceptaciones del mismo curso.
        get_object_or_404(GrupoTutor.objects.select_for_update(), pk=inicial.grupo_id)
        inv = get_object_or_404(InvitacionGrupo.objects.select_for_update(), pk=inicial.pk, token_hash=huella(datos["token"]))
        adulto = AdultoResponsable.objects.select_for_update().get(pk=request.user.pk)
        if adulto.email.strip().lower() != inv.email:
            raise PermissionDenied("Inicia sesión con el correo que recibió la invitación.")
        # Repetir una petición exitosa nunca crea un segundo vínculo.
        if inv.estado == "aceptada" and inv.miembro_id:
            return Response({"jugador_id": inv.miembro.jugador_id, "grupo": inv.grupo.nombre, "aceptada": True})
        invitacion_vigente(inv)
        if datos["crear_perfil"]:
            if any(nombre_clave(j.nombre) == inv.nombre_clave for j in adulto.jugadores.all()):
                raise Conflicto("Ya tienes un perfil con ese nombre. Selecciónalo para conservar su progreso.")
            jugador = UsuarioJugador.objects.create(adulto=adulto, nombre=inv.nombre_nino)
        else:
            jugador = get_object_or_404(UsuarioJugador.objects.select_for_update(), pk=datos["jugador_id"], adulto=adulto)
            if nombre_clave(jugador.nombre) != inv.nombre_clave:
                raise ValidationError("El perfil seleccionado no coincide con el niño invitado. Pide al profesor corregir el nombre si corresponde.")
        miembro, _ = MiembroGrupo.objects.get_or_create(grupo=inv.grupo, jugador=jugador, defaults={"nombre_invitado": inv.nombre_nino})
        inv.miembro = miembro
        inv.estado = "aceptada"
        inv.aceptada_en = timezone.now()
        inv.save(update_fields=["miembro", "estado", "aceptada_en"])
    return Response({"jugador_id": jugador.pk, "grupo": inv.grupo.nombre, "aceptada": True})
