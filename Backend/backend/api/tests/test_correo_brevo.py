"""Integración con el backend real de Anymail y transporte HTTPS simulado."""
import json
import os
from pathlib import Path
import subprocess
import sys
from unittest.mock import patch

from django.core.cache import cache
from django.test import SimpleTestCase, TestCase, override_settings
import requests
from rest_framework.test import APIClient

from api.models import AdultoResponsable, GrupoTutor, InvitacionGrupo


class SeleccionCorreoTests(SimpleTestCase):
    def configuracion(self, **valores):
        entorno = os.environ.copy()
        for clave in ("BREVO_API_KEY", "EMAIL_BACKEND"):
            entorno.pop(clave, None)
        entorno.update(valores)
        resultado = subprocess.run(
            [sys.executable, "-c", "import json; from juego_backend import settings as s; print(json.dumps({'backend': s.EMAIL_BACKEND, 'timeout': getattr(s, 'ANYMAIL', {}).get('REQUESTS_TIMEOUT')}))"],
            cwd=Path(__file__).resolve().parents[2], env=entorno,
            capture_output=True, text=True, check=True, timeout=20,
        )
        return json.loads(resultado.stdout)

    def test_sin_clave_conserva_smtp(self):
        self.assertEqual(self.configuracion()["backend"], "django.core.mail.backends.smtp.EmailBackend")

    def test_con_clave_elige_brevo_con_timeout_limitado(self):
        config = self.configuracion(BREVO_API_KEY="clave-ficticia-para-tests")
        self.assertEqual(config["backend"], "anymail.backends.brevo.EmailBackend")
        self.assertEqual(config["timeout"], 10)

    def test_backend_explicito_tiene_prioridad(self):
        self.assertEqual(self.configuracion(BREVO_API_KEY="ficticia", EMAIL_BACKEND="django.core.mail.backends.console.EmailBackend")["backend"], "django.core.mail.backends.console.EmailBackend")


@override_settings(
    EMAIL_BACKEND="anymail.backends.brevo.EmailBackend",
    ANYMAIL={"BREVO_API_KEY": "clave-ficticia-para-tests", "REQUESTS_TIMEOUT": 10},
    FISHY_EMAIL_ENABLED=True, FISHY_WEB_URL="https://fishy.example.com",
    DEFAULT_FROM_EMAIL="Fishy <no-reply@example.com>",
)
class InvitacionBrevoTests(TestCase):
    def setUp(self):
        cache.clear()
        profesor = AdultoResponsable.objects.create_user("profesor_brevo", "profe@example.com", "clave", rol="profesor")
        self.grupo = GrupoTutor.objects.create(tutor=profesor, nombre="Grupo de prueba")
        self.client = APIClient()
        self.client.force_authenticate(profesor)

    def respuesta(self, status, cuerpo):
        respuesta = requests.Response()
        respuesta.status_code = status
        respuesta._content = json.dumps(cuerpo).encode()
        respuesta.headers["Content-Type"] = "application/json"
        return respuesta

    def invitar(self):
        return self.client.post(f"/api/grupos/{self.grupo.pk}/invitaciones/", {"email": "familia@example.com", "nombre_nino": "Perfil ficticio"}, format="json")

    def test_invita_por_https_con_html_y_texto_y_guarda_estado(self):
        with patch("requests.Session.request", return_value=self.respuesta(201, {"messageId": "<prueba@example.com>"})) as transporte:
            respuesta = self.invitar()
        self.assertEqual(respuesta.status_code, 201)
        argumentos = transporte.call_args.kwargs
        self.assertEqual(argumentos["url"], "https://api.brevo.com/v3/smtp/email")
        self.assertEqual(argumentos["timeout"], 10)
        self.assertEqual(argumentos["headers"]["api-key"], "clave-ficticia-para-tests")
        datos = json.loads(argumentos["data"])
        self.assertEqual(datos["to"][0]["email"], "familia@example.com")
        self.assertIn("https://fishy.example.com/invitacion#", datos["textContent"])
        self.assertIn("Perfil ficticio", datos["htmlContent"])
        self.assertEqual(InvitacionGrupo.objects.get().estado_envio, "enviado")

    def test_rechazo_de_brevo_no_afirma_envio(self):
        with patch("requests.Session.request", return_value=self.respuesta(401, {"code": "unauthorized", "message": "Key not found"})):
            respuesta = self.invitar()
        self.assertEqual(respuesta.status_code, 503)
        self.assertEqual(InvitacionGrupo.objects.get().estado_envio, "fallido")
        self.assertNotIn("clave-ficticia", str(respuesta.data))

    def test_timeout_se_informa_y_conserva_invitacion_para_reenvio(self):
        with patch("requests.Session.request", side_effect=requests.Timeout("timeout simulado")):
            respuesta = self.invitar()
        self.assertEqual(respuesta.status_code, 503)
        self.assertEqual(InvitacionGrupo.objects.get().estado_envio, "fallido")
