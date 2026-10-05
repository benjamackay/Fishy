from django.test import TestCase
from rest_framework.test import APIClient

from api.models import AdultoResponsable, Chat, GrupoTutor, Mensaje, MiembroGrupo, NPC, OpcionBanco, Partida, PreguntaBanco, UsuarioJugador
from api.reportes_web import calcular
from api.subcategorias_reportes import clasificar


class SubcategoriasReportesTests(TestCase):
    def setUp(self):
        adulto = AdultoResponsable.objects.create(nombre="Familia", email="subcategorias@example.com")
        self.jugadores = [UsuarioJugador.objects.create(adulto=adulto, nombre=f"Privado{i}") for i in range(3)]
        self.chats = []
        for jugador in self.jugadores:
            partida = Partida.objects.create(usuario_jugador=jugador)
            npc = NPC.objects.create(partida=partida, nombre="NPC", area="zona", tipo="neutral")
            self.chats.append(Chat.objects.create(partida=partida, npc=npc))

    def registrar(self, indice, categoria, etiquetas, tipo="segura_optima", zona="desconocidos"):
        clave = str(Mensaje.objects.count())
        pregunta = PreguntaBanco.objects.create(pregunta_id="P" + clave, zona=zona, categoria=categoria,
                                                etiquetas_ml=etiquetas, mensaje_npc="ConversacionPrivada")
        opcion = OpcionBanco.objects.create(pregunta=pregunta, opcion_id="O" + clave, texto="RespuestaPrivada",
                                            tipo=tipo, consecuencia_narrativa="Privado")
        Mensaje.objects.create(chat=self.chats[indice], tipo="chain", respuesta="TextoPrivado", opcion_banco_id=opcion.opcion_id)

    def resultados(self, minimo=1):
        return calcular([j.pk for j in self.jugadores], minimo)[0]

    def test_categoria_y_etiquetas_se_cuentan_una_sola_vez_por_subcategoria(self):
        self.registrar(0, "grooming_datos_personales", ["solicitud_foto", "solicitud_ubicacion", "fin_npc"])
        self.registrar(0, "grooming_datos_personales", [], "insegura")
        tema = self.resultados()[0]
        datos = {s["id"]: s for s in tema["subcategorias"]}
        self.assertEqual(datos["datos_personales"]["metricas"], {"decisiones_seguras": 1, "decisiones_evaluadas": 2})
        self.assertEqual(tema["metricas"]["decisiones_evaluadas"], 2)
        self.assertIsNone(datos["secretos"]["metricas"])
        self.assertEqual(datos["secretos"]["motivo"], "sin_resultados")
        self.assertNotIn("Privado", str(tema))

    def test_minimo_por_subcategoria_cuenta_personas_distintas_no_decisiones(self):
        for indice in range(3):
            self.registrar(indice, "grooming_secreto", ["solicitud_secreto"])
        for _ in range(4):
            self.registrar(0, "grooming_datos_personales", ["solicitud_foto"])
        tema = self.resultados(3)[0]
        datos = {s["id"]: s for s in tema["subcategorias"]}
        self.assertIsNotNone(tema["metricas"])
        self.assertEqual(datos["secretos"]["metricas"]["decisiones_evaluadas"], 3)
        self.assertIsNone(datos["datos_personales"]["metricas"])
        self.assertEqual(datos["datos_personales"]["motivo"], "muestra_insuficiente")
        self.assertNotIn("participantes", datos["datos_personales"])

    def test_etiquetas_tecnicas_neutras_y_desconocidas_no_inventan_habilidades(self):
        self.assertEqual(clasificar("desconocidos", "neutral", ["solicitud_foto", "exito"]), ())
        self.assertEqual(clasificar("desconocidos", "sin_clasificar", ["fin_npc", "fallo", "futura"]), ())
        self.registrar(0, "sin_clasificar", ["fin_npc"])
        self.assertTrue(all(s["metricas"] is None for s in self.resultados()[0]["subcategorias"]))
        self.assertEqual(self.resultados()[0]["metricas"]["decisiones_evaluadas"], 1)

    def test_reto_viral_y_multietiqueta_conservan_el_total_de_la_tematica(self):
        self.registrar(0, "reto_viral_presion", ["presion_social", "incentivo_falso", "reto_peligroso"], "insegura", "reto_viral")
        tema = self.resultados()[2]
        self.assertEqual(tema["tematica"], "retos_virales")
        self.assertEqual(tema["metricas"]["decisiones_evaluadas"], 1)
        evaluadas = [s["metricas"]["decisiones_evaluadas"] for s in tema["subcategorias"] if s["metricas"]]
        self.assertEqual(evaluadas, [1, 1, 1])

    def test_no_incluye_jugadores_fuera_del_reporte(self):
        self.registrar(0, "grooming_secreto", [])
        self.registrar(1, "grooming_datos_personales", [])
        tema = calcular([self.jugadores[0].pk], 1)[0][0]
        datos = {s["id"]: s for s in tema["subcategorias"]}
        self.assertIsNone(datos["datos_personales"]["metricas"])
        self.assertEqual(datos["secretos"]["metricas"]["decisiones_evaluadas"], 1)

    def test_endpoint_grupal_entrega_desglose_y_conserva_autorizacion(self):
        profesor = AdultoResponsable.objects.create(nombre="Profesor", email="profesor@example.com", rol="profesor")
        otro = AdultoResponsable.objects.create(nombre="Otro profesor", email="otro@example.com", rol="profesor")
        grupo = GrupoTutor.objects.create(tutor=profesor, nombre="Curso")
        for indice, jugador in enumerate(self.jugadores):
            MiembroGrupo.objects.create(grupo=grupo, jugador=jugador, nombre_invitado=jugador.nombre)
            self.registrar(indice, "grooming_secreto", [])
        client = APIClient()
        ruta = f"/api/grupos/{grupo.pk}/reporte/"
        self.assertEqual(client.get(ruta).status_code, 401)
        client.force_authenticate(otro)
        self.assertEqual(client.get(ruta).status_code, 404)
        client.force_authenticate(profesor)
        respuesta = client.get(ruta)
        self.assertEqual(respuesta.status_code, 200)
        self.assertEqual(respuesta["Cache-Control"], "no-store")
        secreto = next(s for s in respuesta.data["tematicas"][0]["subcategorias"] if s["id"] == "secretos")
        self.assertEqual(secreto["metricas"], {"decisiones_seguras": 3, "decisiones_evaluadas": 3})
        self.assertNotIn("Privado", str(respuesta.data))
