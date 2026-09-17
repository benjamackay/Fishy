from django.test import TestCase, override_settings
from django.utils import timezone
from rest_framework.test import APIClient

from api.models import AdultoResponsable, Chat, Mensaje, NPC, OpcionBanco, Partida, PreguntaBanco, UsuarioJugador, ZonaProgreso


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class ReporteNinoTests(TestCase):
    def setUp(self):
        self.client = APIClient()
        self.padre = AdultoResponsable.objects.create_user(nombre="Familia", email="familia@example.com", password="prueba")
        self.profesor = AdultoResponsable.objects.create_user(nombre="Profesor", email="profe@example.com", password="prueba", rol=AdultoResponsable.ROL_PROFESOR)
        self.nina = UsuarioJugador.objects.create(adulto=self.padre, nombre="Martina")
        self.ajeno = UsuarioJugador.objects.create(adulto=self.profesor, nombre="Perfil ajeno de prueba")

    def test_reportes_individuales_conservan_rol_y_propiedad(self):
        ruta = f"/api/jugadores/{self.nina.pk}/reporte/"
        self.assertEqual(self.client.get(ruta).status_code, 401)
        self.client.force_authenticate(self.profesor)
        self.assertEqual(self.client.get("/api/auth/perfil/").data["rol"], "profesor")
        self.assertEqual(self.client.get(ruta).status_code, 403)
        self.client.force_authenticate(self.padre)
        self.assertEqual(self.client.get("/api/auth/perfil/").data["rol"], "padre")
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

    def test_retos_virales_lee_el_slug_reto_viral_del_banco(self):
        # El banco y Unity usan `reto_viral`; antes el reporte filtraba `retos_virales` y nunca veía nada.
        partida = Partida.objects.create(usuario_jugador=self.nina)
        npc = NPC.objects.create(partida=partida, nombre="NPC", area="zona", tipo="neutral")
        chat = Chat.objects.create(partida=partida, npc=npc)
        for i, (zona, tipo) in enumerate([("reto_viral", "segura_basica"), ("reto_viral", "insegura"), ("retos_virales", "segura_optima")]):
            pregunta = PreguntaBanco.objects.create(pregunta_id=f"R{i}", zona=zona, mensaje_npc="x")
            OpcionBanco.objects.create(pregunta=pregunta, opcion_id=f"RO{i}", texto="x", tipo=tipo, consecuencia_narrativa="x")
            Mensaje.objects.create(chat=chat, tipo="chain", respuesta="x", opcion_banco_id=f"RO{i}")
        ZonaProgreso.objects.create(partida=partida, zona="reto_viral", fecha_completada=timezone.now())
        self.client.force_authenticate(self.padre)
        tematicas = {t["tematica"]: t for t in self.client.get(f"/api/jugadores/{self.nina.pk}/reporte/").data["tematicas"]}
        self.assertNotIn("reto_viral", tematicas)
        self.assertEqual(tematicas["retos_virales"]["metricas"], {"decisiones_seguras": 2, "decisiones_evaluadas": 3, "completada": True})
