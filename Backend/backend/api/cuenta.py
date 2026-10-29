"""Edición de la cuenta autenticada desde el portal."""
from django.contrib.auth.password_validation import validate_password
from django.core.exceptions import ValidationError
from django.db import IntegrityError, transaction
from django.utils import timezone
from rest_framework import serializers
from rest_framework.authtoken.models import Token
from rest_framework.decorators import api_view
from rest_framework.response import Response

from .models import AdultoResponsable
from .serializers import AdultoResponsableSerializer
from .recuperacion import DatosRestablecer, error, leer, sin_cache


class DatosPerfil(serializers.ModelSerializer):
    class Meta:
        model = AdultoResponsable
        fields = ['nombre', 'apellido', 'email', 'fecha_nacimiento']

    def validate(self, attrs):
        if set(self.initial_data) - set(self.fields):
            raise serializers.ValidationError('Solo puedes editar los datos personales del perfil.')
        return attrs

    def validate_email(self, value):
        value = value.strip().lower()
        if AdultoResponsable.objects.exclude(pk=self.instance.pk).filter(email__iexact=value).exists():
            raise serializers.ValidationError('Ya existe una cuenta con este correo.')
        return value

    def validate_fecha_nacimiento(self, value):
        if value and value > timezone.localdate():
            raise serializers.ValidationError('La fecha de nacimiento no puede ser futura.')
        return value


@api_view(['GET', 'PATCH'])
def perfil(request):
    if request.method == 'PATCH':
        try:
            with transaction.atomic():
                adulto = AdultoResponsable.objects.select_for_update().get(pk=request.user.pk)
                datos = DatosPerfil(adulto, data=request.data, partial=True)
                datos.is_valid(raise_exception=True)
                extra = {}
                if 'fecha_nacimiento' in datos.validated_data:
                    nacimiento = datos.validated_data['fecha_nacimiento']
                    hoy = timezone.localdate()
                    extra['edad'] = (hoy.year - nacimiento.year - ((hoy.month, hoy.day) < (nacimiento.month, nacimiento.day))) if nacimiento else None
                adulto = datos.save(**extra)
        except IntegrityError:
            return error('datos_duplicados', 'El nombre de usuario o correo ya está registrado.')
    else:
        adulto = request.user
    return sin_cache(Response(AdultoResponsableSerializer(adulto).data))


class DatosClave(DatosRestablecer):
    reset_token = None
    password_actual = serializers.CharField(max_length=128, trim_whitespace=False)


@api_view(['POST'])
def cambiar_contrasena(request):
    datos, fallo = leer(DatosClave, request.data)
    if fallo:
        return fallo
    with transaction.atomic():
        adulto = AdultoResponsable.objects.select_for_update().get(pk=request.user.pk)
        if not adulto.check_password(datos['password_actual']):
            return error('password_actual_incorrecta', 'La contraseña actual no es correcta.')
        if datos['password_nueva'] != datos['password_confirmacion']:
            return error('password_invalida', 'Las contraseñas no coinciden.')
        if datos['password_nueva'] == datos['password_actual']:
            return error('password_invalida', 'Elige una contraseña diferente a la actual.')
        try:
            validate_password(datos['password_nueva'], adulto)
        except ValidationError as exc:
            return error('password_invalida', ' '.join(exc.messages))
        adulto.set_password(datos['password_nueva'])
        adulto.save(update_fields=['password'])
        adulto.codigos_recuperacion.all().delete()
        Token.objects.filter(user=adulto).delete()
    return sin_cache(Response({'detail': 'Contraseña actualizada. Inicia sesión nuevamente.', 'requiere_login': True}))
