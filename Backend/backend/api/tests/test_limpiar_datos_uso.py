"""Tests de `limpiar_datos_uso`: borra cuentas y lo que cuelga de ellas, nunca el catálogo."""
from io import StringIO

from django.core.management import call_command
from django.core.management.base import CommandError
from django.test import TestCase
from rest_framework.authtoken.models import Token

from api.models import (
    AdultoResponsable,
    GrupoTutor,
    MiembroGrupo,
    NivelRiesgo,
    Partida,
    UsuarioJugador,
)


class LimpiarDatosUsoTests(TestCase):
    def setUp(self):
        crear = AdultoResponsable.objects.create_user
        self.django_admin = crear("dani", "dani@example.com", "x", is_admin=True)
        self.portal_admin = crear("qa_admin", "qa_admin@example.com", "x", rol=AdultoResponsable.ROL_ADMIN)
        self.padre = crear("olaola", "olaola@example.com", "x")
        self.profe = crear("profe_prueba", "profe@example.com", "x", rol=AdultoResponsable.ROL_PROFESOR)

        self.nino_admin = UsuarioJugador.objects.create(adulto=self.django_admin, nombre="Perfil admin")
        Partida.objects.create(usuario_jugador=self.nino_admin)
        nino = UsuarioJugador.objects.create(adulto=self.padre, nombre="Perfil prueba")
        Partida.objects.create(usuario_jugador=nino)
        grupo = GrupoTutor.objects.create(tutor=self.profe, nombre="Curso de prueba")
        MiembroGrupo.objects.create(grupo=grupo, jugador=nino)
        Token.objects.create(user=self.padre)
        # Catálogo: no cuelga de ninguna cuenta y no debe moverse.
        self.riesgo = NivelRiesgo.objects.create(nombre="bajo")

    def correr(self, *args):
        salida = StringIO()
        call_command("limpiar_datos_uso", *args, stdout=salida)
        return salida.getvalue()

    def test_sin_confirmar_no_borra_nada(self):
        salida = self.correr()
        self.assertIn("No se borró nada", salida)
        self.assertEqual(AdultoResponsable.objects.count(), 4)
        self.assertEqual(Partida.objects.count(), 2)

    def test_borra_lo_no_admin_con_su_cascada_y_conserva_admins(self):
        self.correr("--confirmar")
        self.assertEqual(set(AdultoResponsable.objects.values_list("nombre", flat=True)), {"dani", "qa_admin"})
        self.assertEqual(list(Partida.objects.values_list("usuario_jugador", flat=True)), [self.nino_admin.pk])
        self.assertFalse(GrupoTutor.objects.exists())
        self.assertFalse(MiembroGrupo.objects.exists())
        self.assertFalse(Token.objects.exists())
        self.assertTrue(NivelRiesgo.objects.filter(pk=self.riesgo.pk).exists())

    def test_conservar_agrega_cuentas_por_nombre(self):
        self.correr("--conservar", "Profe_Prueba", "--confirmar")
        self.assertTrue(GrupoTutor.objects.filter(tutor=self.profe).exists())
        self.assertFalse(AdultoResponsable.objects.filter(pk=self.padre.pk).exists())

    def test_nombre_mal_escrito_no_borra_nada(self):
        with self.assertRaisesMessage(CommandError, "profe_prueva"):
            self.correr("--conservar", "profe_prueva", "--confirmar")
        self.assertEqual(AdultoResponsable.objects.count(), 4)
