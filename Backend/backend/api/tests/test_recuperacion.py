import re
from datetime import timedelta
from unittest.mock import patch

from django.core import mail
from django.core.cache import cache
from django.test import TestCase, override_settings
from django.utils import timezone
from rest_framework.authtoken.models import Token
from rest_framework.test import APIClient

from api.models import AdultoResponsable, CodigoRecuperacion

BASE = "/api/auth/recuperar/"
CLAVE_NUEVA = "otto-chungungo-2026"


@override_settings(EMAIL_BACKEND="django.core.mail.backends.locmem.EmailBackend", FISHY_EMAIL_ENABLED=True,
                   DEFAULT_FROM_EMAIL="Fishy <no-reply@example.com>",
                   PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class RecuperacionTests(TestCase):
    def setUp(self):
        cache.clear()
        self.padre = AdultoResponsable.objects.create_user("padre", "Familia@example.com", "clave-vieja")
        self.client = APIClient()

    def solicitar(self, email="familia@example.com"):
        return self.client.post(BASE + "solicitar/", {"email": email}, format="json")

    def codigo_enviado(self):
        return re.search(r"\b(\d{6})\b", mail.outbox[-1].body)[1]

    def verificar(self, codigo, email="familia@example.com"):
        return self.client.post(BASE + "verificar/", {"email": email, "codigo": codigo}, format="json")

    def permiso(self):
        self.solicitar()
        return self.permiso_de_un_codigo_ya_enviado()

    def permiso_de_un_codigo_ya_enviado(self):
        respuesta = self.verificar(self.codigo_enviado())
        self.assertEqual(respuesta.status_code, 200, respuesta.data)
        return respuesta.data["permiso"]

    def cambiar(self, permiso, password=CLAVE_NUEVA):
        return self.client.post(BASE + "cambiar/", {"permiso": permiso, "password": password}, format="json")

    def otro_codigo(self, codigo):
        return f"{(int(codigo) + 1) % 10**6:06d}"

    def test_camino_completo_cambia_la_clave_y_cierra_las_sesiones(self):
        Token.objects.create(user=self.padre)
        self.assertEqual(self.solicitar("FAMILIA@example.com").status_code, 202)
        correo = mail.outbox[0]
        self.assertEqual(correo.to, ["Familia@example.com"])
        self.assertEqual(len(correo.alternatives), 1)
        respuesta = self.cambiar(self.permiso_de_un_codigo_ya_enviado())
        self.assertEqual(respuesta.status_code, 200, respuesta.data)
        self.padre.refresh_from_db()
        self.assertTrue(self.padre.check_password(CLAVE_NUEVA))
        self.assertFalse(Token.objects.filter(user=self.padre).exists())
        self.assertFalse(CodigoRecuperacion.objects.exists())
        login = self.client.post("/api/auth/login/", {"nombre": "padre", "password": CLAVE_NUEVA}, format="json")
        self.assertEqual(login.status_code, 200)

    def test_correo_desconocido_responde_igual_y_no_envia_nada(self):
        conocido = self.solicitar()
        cache.clear()
        desconocido = self.solicitar("nadie@example.com")
        self.assertEqual((desconocido.status_code, desconocido.data), (conocido.status_code, conocido.data))
        self.assertEqual(len(mail.outbox), 1)
        self.assertEqual(self.verificar("123456", "nadie@example.com").status_code, 400)

    def test_el_codigo_no_se_guarda_en_claro(self):
        self.solicitar()
        codigo = self.codigo_enviado()
        fila = CodigoRecuperacion.objects.get()
        self.assertNotIn(codigo, fila.codigo_hash)
        self.assertEqual(len(fila.codigo_hash), 64)

    def test_quinto_error_anula_el_codigo_aunque_despues_acierte(self):
        self.solicitar()
        codigo = self.codigo_enviado()
        for _ in range(5):
            self.assertEqual(self.verificar(self.otro_codigo(codigo)).status_code, 400)
        self.assertEqual(CodigoRecuperacion.objects.get().intentos, 5)
        self.assertEqual(self.verificar(codigo).status_code, 400)

    def test_codigo_vencido_no_sirve(self):
        self.solicitar()
        codigo = self.codigo_enviado()
        CodigoRecuperacion.objects.update(vence_en=timezone.now() - timedelta(seconds=1))
        self.assertEqual(self.verificar(codigo).status_code, 400)

    def test_el_codigo_sirve_una_sola_vez(self):
        self.solicitar()
        codigo = self.codigo_enviado()
        self.assertEqual(self.verificar(codigo).status_code, 200)
        self.assertEqual(self.verificar(codigo).status_code, 400)

    def test_pedir_otro_codigo_anula_el_anterior(self):
        self.solicitar()
        viejo = self.codigo_enviado()
        CodigoRecuperacion.objects.update(fecha_creacion=timezone.now() - timedelta(minutes=2))
        self.solicitar()
        nuevo = self.codigo_enviado()
        self.assertEqual(len(mail.outbox), 2)
        self.assertEqual(CodigoRecuperacion.objects.count(), 1)
        if viejo != nuevo:
            self.assertEqual(self.verificar(viejo).status_code, 400)
        self.assertEqual(self.verificar(nuevo).status_code, 200)

    def test_dentro_del_minuto_no_manda_otro_codigo(self):
        self.solicitar()
        self.assertEqual(self.solicitar().status_code, 202)
        self.assertEqual(len(mail.outbox), 1)

    def test_tope_de_envios_por_correo(self):
        for _ in range(5):
            self.assertEqual(self.solicitar().status_code, 202)
        self.assertEqual(self.solicitar().status_code, 429)

    def test_el_permiso_es_de_un_solo_uso(self):
        permiso = self.permiso()
        self.assertEqual(self.cambiar(permiso).status_code, 200)
        self.assertEqual(self.cambiar(permiso, "otra-clave-segura-99").status_code, 400)
        self.padre.refresh_from_db()
        self.assertTrue(self.padre.check_password(CLAVE_NUEVA))

    def test_permiso_vencido_no_sirve(self):
        permiso = self.permiso()
        CodigoRecuperacion.objects.update(permiso_vence_en=timezone.now() - timedelta(seconds=1))
        self.assertEqual(self.cambiar(permiso).status_code, 400)
        self.padre.refresh_from_db()
        self.assertTrue(self.padre.check_password("clave-vieja"))

    def test_clave_debil_se_rechaza_y_el_permiso_sigue_sirviendo(self):
        permiso = self.permiso()
        respuesta = self.cambiar(permiso, "1234")
        self.assertEqual(respuesta.status_code, 400)
        self.assertIn("password", respuesta.data)
        self.assertEqual(self.cambiar(permiso).status_code, 200)

    def test_sin_correo_configurado_responde_503(self):
        with self.settings(FISHY_EMAIL_ENABLED=False):
            self.assertEqual(self.solicitar().status_code, 503)
        self.assertFalse(CodigoRecuperacion.objects.exists())

    def test_si_el_proveedor_falla_no_queda_un_codigo_huerfano(self):
        with patch("api.recuperacion.EmailMultiAlternatives.send", side_effect=OSError("caído")):
            self.assertEqual(self.solicitar().status_code, 503)
        self.assertFalse(CodigoRecuperacion.objects.exists())
        self.assertEqual(self.solicitar().status_code, 202)

    def test_un_token_vencido_en_la_cabecera_no_bloquea_la_recuperacion(self):
        self.client.credentials(HTTP_AUTHORIZATION="Token no-existe")
        self.assertEqual(self.solicitar().status_code, 202)
