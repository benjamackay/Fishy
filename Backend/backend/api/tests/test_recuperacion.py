import re
import uuid
from datetime import timedelta
from unittest.mock import patch

from django.core import mail
from django.core.cache import cache
from django.test import TestCase, override_settings
from django.utils import timezone
from rest_framework.authtoken.models import Token
from rest_framework.test import APIClient

from api.models import AdultoResponsable, CodigoRecuperacion

BASE = "/api/auth/recuperacion/"
CLAVE_NUEVA = "otto-chungungo-2026"


@override_settings(EMAIL_BACKEND="django.core.mail.backends.locmem.EmailBackend", FISHY_EMAIL_ENABLED=True,
                   DEFAULT_FROM_EMAIL="Fishy <no-reply@example.com>",
                   PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
@patch("api.recuperacion.despachar", lambda tarea: tarea())  # el correo sale en línea, no en otro hilo
class RecuperacionTests(TestCase):
    def setUp(self):
        cache.clear()
        self.padre = AdultoResponsable.objects.create_user("padre", "Familia@example.com", "clave-vieja")
        self.client = APIClient()

    def solicitar(self, email="familia@example.com"):
        with self.captureOnCommitCallbacks(execute=True):
            return self.client.post(BASE + "solicitar/", {"email": email}, format="json")

    def solicitud(self, email="familia@example.com"):
        respuesta = self.solicitar(email)
        self.assertEqual(respuesta.status_code, 202, respuesta.data)
        return respuesta.data["solicitud_id"]

    def codigo_enviado(self):
        return re.search(r"\b(\d{6})\b", mail.outbox[-1].body)[1]

    def verificar(self, solicitud_id, codigo):
        return self.client.post(BASE + "verificar/", {"solicitud_id": solicitud_id, "codigo": codigo}, format="json")

    def reset_token(self):
        respuesta = self.verificar(self.solicitud(), self.codigo_enviado())
        self.assertEqual(respuesta.status_code, 200, respuesta.data)
        return respuesta.data["reset_token"]

    def restablecer(self, token, password=CLAVE_NUEVA, confirmacion=None):
        return self.client.post(BASE + "restablecer/", {
            "reset_token": token, "password_nueva": password,
            "password_confirmacion": password if confirmacion is None else confirmacion}, format="json")

    def otro_codigo(self, codigo):
        return f"{(int(codigo) + 1) % 10**6:06d}"

    def nuevo_minuto(self):
        cache.clear()  # pasa la espera entre solicitudes (y los topes)

    def test_camino_completo_cambia_la_clave_y_cierra_las_sesiones(self):
        Token.objects.create(user=self.padre)
        respuesta = self.solicitar("FAMILIA@example.com")
        self.assertEqual(respuesta.status_code, 202)
        self.assertEqual(respuesta.data["expira_en_segundos"], 600)
        self.assertEqual(respuesta.data["reenvio_en_segundos"], 60)
        self.assertEqual(mail.outbox[0].to, ["Familia@example.com"])
        self.assertEqual(len(mail.outbox[0].alternatives), 1)
        verificado = self.verificar(respuesta.data["solicitud_id"], self.codigo_enviado())
        self.assertEqual(verificado.status_code, 200, verificado.data)
        self.assertEqual(verificado.data["expira_en_segundos"], 300)
        final = self.restablecer(verificado.data["reset_token"])
        self.assertEqual(final.status_code, 200, final.data)
        self.assertTrue(final.data["requiere_login"])
        self.assertNotIn("token", final.data)
        self.padre.refresh_from_db()
        self.assertTrue(self.padre.check_password(CLAVE_NUEVA))
        self.assertFalse(Token.objects.filter(user=self.padre).exists())
        self.assertFalse(CodigoRecuperacion.objects.exists())
        login = self.client.post("/api/auth/login/", {"nombre": "padre", "password": CLAVE_NUEVA}, format="json")
        self.assertEqual(login.status_code, 200)

    def test_correo_desconocido_responde_igual_con_otra_solicitud_y_no_envia_nada(self):
        conocido = self.solicitar()
        desconocido = self.solicitar("nadie@example.com")
        self.assertEqual(desconocido.status_code, conocido.status_code)
        self.assertEqual(set(desconocido.data), set(conocido.data))
        self.assertEqual(desconocido.data["detail"], conocido.data["detail"])
        uuid.UUID(desconocido.data["solicitud_id"])
        self.assertEqual(len(mail.outbox), 1)
        self.assertEqual(self.verificar(desconocido.data["solicitud_id"], "123456").status_code, 400)

    def test_la_espera_entre_solicitudes_es_igual_con_y_sin_cuenta(self):
        for email in ("familia@example.com", "nadie@example.com"):
            self.solicitar(email)
            otra = self.solicitar(email)
            self.assertEqual(otra.status_code, 429)
            self.assertEqual(otra.data["code"], "reenvio_muy_pronto")
            self.assertEqual(otra["Retry-After"], "60")
        self.assertEqual(len(mail.outbox), 1)

    def test_el_codigo_no_se_guarda_en_claro(self):
        self.solicitud()
        codigo = self.codigo_enviado()
        fila = CodigoRecuperacion.objects.get()
        self.assertNotIn(codigo, fila.codigo_hash)
        self.assertEqual(len(fila.codigo_hash), 64)

    def test_acepta_codigos_que_empiezan_con_cero(self):
        with patch("api.recuperacion.secrets.randbelow", return_value=12345):
            solicitud = self.solicitud()
        self.assertEqual(self.codigo_enviado(), "012345")
        self.assertEqual(self.verificar(solicitud, "012345").status_code, 200)

    def test_quinto_error_anula_el_codigo_aunque_despues_acierte(self):
        solicitud = self.solicitud()
        codigo = self.codigo_enviado()
        for _ in range(5):
            respuesta = self.verificar(solicitud, self.otro_codigo(codigo))
            self.assertEqual((respuesta.status_code, respuesta.data["code"]), (400, "codigo_invalido"))
        self.assertEqual(CodigoRecuperacion.objects.get().intentos, 5)
        self.assertEqual(self.verificar(solicitud, codigo).status_code, 400)

    def test_codigo_vencido_no_sirve(self):
        solicitud = self.solicitud()
        CodigoRecuperacion.objects.update(vence_en=timezone.now() - timedelta(seconds=1))
        self.assertEqual(self.verificar(solicitud, self.codigo_enviado()).status_code, 400)

    def test_el_codigo_sirve_una_sola_vez(self):
        solicitud = self.solicitud()
        codigo = self.codigo_enviado()
        self.assertEqual(self.verificar(solicitud, codigo).status_code, 200)
        self.assertEqual(self.verificar(solicitud, codigo).status_code, 400)

    def test_pedir_otro_codigo_anula_el_anterior(self):
        vieja = self.solicitud()
        codigo_viejo = self.codigo_enviado()
        self.nuevo_minuto()
        nueva = self.solicitud()
        self.assertEqual(len(mail.outbox), 2)
        self.assertEqual(CodigoRecuperacion.objects.count(), 1)
        self.assertEqual(self.verificar(vieja, codigo_viejo).status_code, 400)
        self.assertEqual(self.verificar(nueva, self.codigo_enviado()).status_code, 200)

    @override_settings(FISHY_RECUPERACION_REENVIO_SEGUNDOS=0)  # sin espera: solo cuenta el tope
    def test_tope_de_envios_por_correo(self):
        for _ in range(5):
            self.assertEqual(self.solicitar().status_code, 202)
        respuesta = self.solicitar()
        self.assertEqual(respuesta.status_code, 429)
        self.assertIn("Retry-After", respuesta)

    def test_el_reset_token_es_de_un_solo_uso(self):
        token = self.reset_token()
        self.assertEqual(self.restablecer(token).status_code, 200)
        segunda = self.restablecer(token, "otra-clave-segura-99")
        self.assertEqual((segunda.status_code, segunda.data["code"]), (400, "reset_token_invalido"))
        self.padre.refresh_from_db()
        self.assertTrue(self.padre.check_password(CLAVE_NUEVA))

    def test_reset_token_vencido_no_sirve(self):
        token = self.reset_token()
        CodigoRecuperacion.objects.update(permiso_vence_en=timezone.now() - timedelta(seconds=1))
        self.assertEqual(self.restablecer(token).status_code, 400)
        self.padre.refresh_from_db()
        self.assertTrue(self.padre.check_password("clave-vieja"))

    def test_el_reset_token_no_sirve_como_sesion(self):
        token = self.reset_token()
        self.client.credentials(HTTP_AUTHORIZATION="Token " + token)
        self.assertEqual(self.client.get("/api/auth/perfil/").status_code, 401)

    def test_clave_debil_o_distinta_se_rechaza_y_el_token_sigue_sirviendo(self):
        token = self.reset_token()
        debil = self.restablecer(token, "1234")
        self.assertEqual(debil.status_code, 400)
        self.assertIn("password_nueva", debil.data["errors"])
        distinta = self.restablecer(token, CLAVE_NUEVA, "otra-cosa-distinta")
        self.assertEqual(distinta.status_code, 400)
        self.assertIn("password_confirmacion", distinta.data["errors"])
        self.assertEqual(self.restablecer(token).status_code, 200)

    def test_datos_mal_formados_responden_400_con_el_campo(self):
        respuesta = self.client.post(BASE + "solicitar/", {"email": "no-es-correo"}, format="json")
        self.assertEqual((respuesta.status_code, respuesta.data["code"]), (400, "datos_invalidos"))
        self.assertIn("email", respuesta.data["errors"])
        self.assertEqual(self.verificar("no-es-uuid", "123456").status_code, 400)

    def test_sin_correo_configurado_responde_503(self):
        with self.settings(FISHY_EMAIL_ENABLED=False):
            self.assertEqual(self.solicitar().status_code, 503)
        self.assertFalse(CodigoRecuperacion.objects.exists())

    def test_si_el_proveedor_falla_responde_igual_y_no_queda_un_codigo_vivo(self):
        with patch("api.recuperacion.EmailMultiAlternatives.send", side_effect=OSError("caído")):
            respuesta = self.solicitar()
        self.assertEqual(respuesta.status_code, 202)
        self.assertFalse(CodigoRecuperacion.objects.exists())

    def test_un_token_vencido_en_la_cabecera_no_bloquea_la_recuperacion(self):
        self.client.credentials(HTTP_AUTHORIZATION="Token no-existe")
        self.assertEqual(self.solicitar().status_code, 202)
