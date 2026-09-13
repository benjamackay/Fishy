from unittest.mock import patch

from django.test import TestCase, override_settings
from rest_framework.test import APIClient

from api.models import AdultoResponsable, Chat, Mensaje, NPC, OpcionBanco, Partida, PreguntaBanco, UsuarioJugador


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class PortalSinGruposTests(TestCase):
    def setUp(self):
        self.client = APIClient()
        self.padre = AdultoResponsable.objects.create_user(nombre="Familia", email="familia@example.com", password="prueba")
        self.profesor = AdultoResponsable.objects.create_user(nombre="Profesor", email="profe@example.com", password="prueba", is_admin=True)
        self.nina = UsuarioJugador.objects.create(adulto=self.padre, nombre="Martina")
        self.ajeno = UsuarioJugador.objects.create(adulto=self.profesor, nombre="Perfil ajeno de prueba")

    def test_grupos_retirados_informan_indisponibilidad_sin_consultar_tablas(self):
        self.client.force_authenticate(self.profesor)
        grupo = "11111111-1111-4111-8111-111111111111"
        rutas = ["/api/grupos/", f"/api/grupos/{grupo}/", f"/api/grupos/{grupo}/reporte/", f"/api/grupos/{grupo}/seguimiento/"]
        for ruta in rutas:
            with self.subTest(ruta=ruta), self.assertNumQueries(0):
                respuesta = self.client.get(ruta)
                self.assertEqual(respuesta.status_code, 503)
                self.assertIn("no está habilitada", respuesta.data["detail"])
                self.assertEqual(respuesta["Cache-Control"], "no-store")

    def test_invitaciones_retiradas_no_envian_correos_ni_crean_perfiles(self):
        cantidad = UsuarioJugador.objects.count()
        with patch("django.core.mail.EmailMultiAlternatives.send") as enviar:
            for ruta in ["/api/invitaciones/consultar/", "/api/invitaciones/aceptar/", "/api/grupos/11111111-1111-4111-8111-111111111111/invitaciones/"]:
                with self.subTest(ruta=ruta), self.assertNumQueries(0):
                    self.assertEqual(self.client.post(ruta, {"token": "prueba", "crear_perfil": True}, format="json").status_code, 503)
            enviar.assert_not_called()
        self.assertEqual(UsuarioJugador.objects.count(), cantidad)

    def test_reportes_individuales_conservan_rol_y_propiedad(self):
        ruta = f"/api/jugadores/{self.nina.pk}/reporte/"
        self.assertEqual(self.client.get(ruta).status_code, 401)
        self.client.force_authenticate(self.profesor)
        self.assertTrue(self.client.get("/api/auth/perfil/").data["is_admin"])
        self.assertEqual(self.client.get(ruta).status_code, 403)
        self.client.force_authenticate(self.padre)
        self.assertFalse(self.client.get("/api/auth/perfil/").data["is_admin"])
        self.assertEqual(self.client.get(f"/api/jugadores/{self.ajeno.pk}/reporte/").status_code, 404)
        reporte = self.client.get(ruta)
        self.assertEqual(reporte.status_code, 200)
        self.assertTrue(all(t["metricas"] is None for t in reporte.data["tematicas"]))

    def test_reporte_individual_lee_decisiones_y_no_expone_transcripciones(self):
        pregunta = PreguntaBanco.objects.create(pregunta_id="P1", zona="desconocidos", mensaje_npc="Texto privado")
        opcion = OpcionBanco.objects.create(pregunta=pregunta, opcion_id="O1", texto="Respuesta privada", tipo="segura_optima", consecuencia_narrativa="Privado")
        partida = Partida.objects.create(usuario_jugador=self.nina)
        npc = NPC.objects.create(partida=partida, nombre="NPC", area="zona", tipo="neutral")
        chat = Chat.objects.create(partida=partida, npc=npc)
        Mensaje.objects.create(chat=chat, tipo="chain", respuesta="ConversacionPrivada", opcion_banco_id=opcion.opcion_id)
        self.client.force_authenticate(self.padre)
        reporte = self.client.get(f"/api/jugadores/{self.nina.pk}/reporte/")
        self.assertEqual(reporte.status_code, 200)
        self.assertEqual(reporte.data["tematicas"][0]["metricas"]["decisiones_seguras"], 1)
        for texto in ["Texto privado", "Respuesta privada", "ConversacionPrivada"]:
            self.assertNotIn(texto, str(reporte.data))
