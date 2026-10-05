"""Tests del vencimiento del token (api/autenticacion.py).

Un solo token por cuenta, compartido por el juego y el portal: vence a los
FISHY_TOKEN_DIAS días y el login lo renueva solo al pasar la mitad de su vida.
"""
from datetime import timedelta

from django.test import TestCase, override_settings
from django.utils import timezone
from rest_framework.authtoken.models import Token
from rest_framework.test import APIClient

from api.models import AdultoResponsable


@override_settings(FISHY_TOKEN_DIAS=30)
class TokenVencimientoTests(TestCase):
    def setUp(self):
        self.adulto = AdultoResponsable.objects.create_user("familia", "familia@example.com", "clave-larga")
        self.client = APIClient()

    def login(self):
        resp = self.client.post("/api/auth/login/", {"nombre": "familia", "password": "clave-larga"}, format="json")
        self.assertEqual(resp.status_code, 200)
        return resp.data["token"]

    def envejecer(self, dias):
        Token.objects.filter(user=self.adulto).update(created=timezone.now() - timedelta(days=dias))

    def perfil(self, token):
        return self.client.get("/api/auth/perfil/", HTTP_AUTHORIZATION=f"Token {token}")

    def test_un_token_vigente_entra(self):
        self.assertEqual(self.perfil(self.login()).status_code, 200)

    def test_un_token_vencido_da_401_y_dice_por_que(self):
        token = self.login()
        self.envejecer(31)
        resp = self.perfil(token)
        self.assertEqual(resp.status_code, 401)
        self.assertIn("venció", str(resp.data["detail"]))

    def test_entrar_otra_vez_no_le_corta_la_sesion_al_otro_cliente(self):
        """El juego y el portal comparten el token: si el login creara uno nuevo,
        que el apoderado abriera el portal dejaría al juego sin guardar."""
        del_juego = self.login()
        self.envejecer(10)
        self.assertEqual(self.login(), del_juego)
        self.assertEqual(self.perfil(del_juego).status_code, 200)

    def test_pasada_la_mitad_el_login_entrega_uno_nuevo(self):
        """Así un login nunca entrega un token a punto de vencer."""
        viejo = self.login()
        self.envejecer(16)
        nuevo = self.login()
        self.assertNotEqual(nuevo, viejo)
        self.assertEqual(self.perfil(viejo).status_code, 401)
        self.assertEqual(self.perfil(nuevo).status_code, 200)

    def test_despues_de_vencer_basta_con_volver_a_entrar(self):
        self.login()
        self.envejecer(45)
        self.assertEqual(self.perfil(self.login()).status_code, 200)

    def test_el_token_del_registro_tambien_vence(self):
        resp = self.client.post("/api/auth/registro/",
                                {"nombre": "nueva", "email": "nueva@example.com", "password": "clave-larga"},
                                format="json")
        token = resp.data["token"]
        self.assertEqual(self.perfil(token).status_code, 200)
        Token.objects.filter(key=token).update(created=timezone.now() - timedelta(days=31))
        self.assertEqual(self.perfil(token).status_code, 401)

    @override_settings(FISHY_TOKEN_DIAS=7)
    def test_la_duracion_sale_de_la_configuracion(self):
        token = self.login()
        self.envejecer(8)
        self.assertEqual(self.perfil(token).status_code, 401)
