"""Peticiones del portal de cPanel con el middleware real, sin acceder a la BD."""
from django.conf import settings
from django.test import SimpleTestCase, override_settings


class CorsPortalTests(SimpleTestCase):
    def preflight(self, origen, ruta="/api/auth/login/"):
        return self.client.options(
            ruta,
            HTTP_ORIGIN=origen,
            HTTP_ACCESS_CONTROL_REQUEST_METHOD="POST",
            HTTP_ACCESS_CONTROL_REQUEST_HEADERS="authorization,content-type",
        )

    def test_dominios_oficiales_permiten_login_con_token_y_json(self):
        for origen in ("https://fishygame.cl", "https://www.fishygame.cl"):
            with self.subTest(origen=origen):
                respuesta = self.preflight(origen)
                self.assertEqual(respuesta.status_code, 200)
                self.assertEqual(respuesta.headers["Access-Control-Allow-Origin"], origen)
                self.assertIn("POST", respuesta.headers["Access-Control-Allow-Methods"])
                cabeceras = respuesta.headers["Access-Control-Allow-Headers"].lower()
                self.assertIn("authorization", cabeceras)
                self.assertIn("content-type", cabeceras)
                self.assertNotIn("Access-Control-Allow-Credentials", respuesta.headers)

    def test_otros_origenes_no_se_autorizan(self):
        for origen in (
            "https://otro.cl",
            "http://fishygame.cl",
            "https://fishygame.cl.otro.cl",
            "null",
        ):
            with self.subTest(origen=origen):
                respuesta = self.preflight(origen)
                self.assertNotIn("Access-Control-Allow-Origin", respuesta.headers)

    def test_salud_incluye_cors_en_la_respuesta_real(self):
        respuesta = self.client.get("/api/health/", HTTP_ORIGIN="https://fishygame.cl")
        self.assertEqual(respuesta.status_code, 200)
        self.assertEqual(respuesta.json()["status"], "ok")
        self.assertEqual(respuesta.headers["Access-Control-Allow-Origin"], "https://fishygame.cl")

    def test_cors_no_omite_la_autenticacion(self):
        respuesta = self.client.get("/api/auth/perfil/", HTTP_ORIGIN="https://fishygame.cl")
        self.assertEqual(respuesta.status_code, 401)
        self.assertEqual(respuesta.headers["Access-Control-Allow-Origin"], "https://fishygame.cl")

    def test_cors_no_se_aplica_al_admin(self):
        respuesta = self.client.get("/admin/login/", HTTP_ORIGIN="https://fishygame.cl")
        self.assertNotIn("Access-Control-Allow-Origin", respuesta.headers)

    @override_settings(CORS_ALLOWED_ORIGINS=[*settings.CORS_ALLOWED_ORIGINS, "https://portal.otro.cl"])
    def test_se_pueden_agregar_origenes_exactos(self):
        respuesta = self.preflight("https://portal.otro.cl")
        self.assertEqual(respuesta.headers["Access-Control-Allow-Origin"], "https://portal.otro.cl")
