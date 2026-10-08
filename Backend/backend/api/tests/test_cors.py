from django.test import SimpleTestCase


class CorsPortalTests(SimpleTestCase):
    def preflight(self, origin, path="/api/auth/registro/"):
        return self.client.options(
            path,
            HTTP_ORIGIN=origin,
            HTTP_ACCESS_CONTROL_REQUEST_METHOD="POST",
            HTTP_ACCESS_CONTROL_REQUEST_HEADERS="content-type,authorization",
        )

    def test_portal_permite_registro_y_token(self):
        for origin in ("https://fishygame.cl", "https://www.fishygame.cl"):
            with self.subTest(origin=origin):
                response = self.preflight(origin)
                self.assertEqual(response.status_code, 200)
                self.assertEqual(response["Access-Control-Allow-Origin"], origin)
                self.assertIn("POST", response["Access-Control-Allow-Methods"])
                headers = response["Access-Control-Allow-Headers"].lower()
                self.assertIn("content-type", headers)
                self.assertIn("authorization", headers)
                self.assertNotIn("Access-Control-Allow-Credentials", response)

    def test_no_autoriza_origen_ajeno(self):
        response = self.preflight("https://fishygame.cl.example.com")
        self.assertNotIn("Access-Control-Allow-Origin", response)

    def test_no_abre_admin(self):
        response = self.preflight("https://fishygame.cl", "/admin/login/")
        self.assertNotIn("Access-Control-Allow-Origin", response)
