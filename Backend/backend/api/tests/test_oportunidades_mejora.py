"""Verifica el endpoint de oportunidades de mejora.

Una "oportunidad de mejora" no es un campo guardado: es un `Mensaje` cuyo
`opcion_banco_id` resuelve a una `OpcionBanco` de tipo `insegura`. Estos tests
fijan esa definición, porque es justamente lo que hace que la lista no pueda
quedar desincronizada del banco.

Lo que se protege acá:
  - que una decisión insegura aparezca, con la alternativa que se le escapó
  - que una decisión segura NO aparezca (ni siquiera la `segura_basica`)
  - que un adulto no vea las oportunidades de la partida de otro
"""
from api.models import Chat, NPC, OpcionBanco, PreguntaBanco

from .test_guardado_decisiones import BaseAPI


class OportunidadesMejoraTests(BaseAPI):
    """HDU-3 CA3 y equivalentes: la decisión insegura queda registrada como tal."""

    def setUp(self):
        self.adulto, self.token = self._adulto("mejora@test.local", "AdultoMejora")
        self.partida = self._partida(self.adulto, nombre_menor="Perfil 1")
        self.npc = NPC.objects.create(
            partida=self.partida, nombre="Flamenco", area="ciberacoso", tipo="enemigo"
        )
        self.chat = Chat.objects.create(
            partida=self.partida, npc=self.npc, categoria_riesgo="ciberacoso"
        )

        # Réplica reducida de HDU3_NPC03_Q01: reportar / reclamar / burlarse.
        self.pregunta = PreguntaBanco.objects.create(
            pregunta_id="HDU3_TEST_Q01", hdu="HDU-3", zona="ciberacoso",
            categoria="ciberacoso", nivel_riesgo=2, es_mensaje_riesgo=True,
            mensaje_npc="te sacamos del grupo, todos votaron",
        )
        self.optima = OpcionBanco.objects.create(
            pregunta=self.pregunta, opcion_id="HDU3_TEST_Q01_R1",
            texto="Reportar y bloquear este mensaje", tipo="segura_optima",
            consecuencia_narrativa="Lo reportaste de inmediato.", impacto_puntuacion=2,
        )
        self.basica = OpcionBanco.objects.create(
            pregunta=self.pregunta, opcion_id="HDU3_TEST_Q01_R2",
            texto="eso esta muy mal de tu parte", tipo="segura_basica",
            consecuencia_narrativa="Intentaste razonar.", impacto_puntuacion=1,
        )
        self.insegura = OpcionBanco.objects.create(
            pregunta=self.pregunta, opcion_id="HDU3_TEST_Q01_R3",
            texto="ja, quedense solos", tipo="insegura",
            consecuencia_narrativa="Respondes con enojo.", impacto_puntuacion=-1,
        )

    # ── Helpers ───────────────────────────────────────────────────────────────
    def _elegir(self, opcion, calidad="mala"):
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": opcion.texto, "calidad_respuesta": calidad,
             "pregunta_banco_id": self.pregunta.pregunta_id,
             "opcion_banco_id": opcion.opcion_id},
            self.token, 201,
        )

    def _oportunidades(self, query="", token=None, espera=200):
        return self.get(
            f"/api/partidas/{self.partida.pk}/oportunidades-mejora/{query}",
            token or self.token, espera,
        )

    # ── Tests ─────────────────────────────────────────────────────────────────
    def test_la_burla_queda_registrada_como_oportunidad_de_mejora(self):
        self._elegir(self.insegura)

        d = self._oportunidades()
        self.assertEqual(d["total"], 1)
        self.assertEqual(d["jugador"], "Perfil 1")

        o = d["oportunidades"][0]
        self.assertEqual(o["zona"], "ciberacoso")
        self.assertEqual(o["npc"], "Flamenco")
        self.assertEqual(o["pregunta_banco_id"], "HDU3_TEST_Q01")
        self.assertEqual(o["mensaje_npc"], "te sacamos del grupo, todos votaron")
        self.assertEqual(o["eligio"]["opcion_banco_id"], "HDU3_TEST_Q01_R3")
        self.assertEqual(o["eligio"]["impacto_puntuacion"], -1)
        self.assertEqual(o["eligio"]["consecuencia"], "Respondes con enojo.")

    def test_incluye_la_mejor_alternativa_que_tenia_disponible(self):
        """Sin la alternativa, el reporte dice qué falló pero no qué enseñar."""
        self._elegir(self.insegura)

        o = self._oportunidades()["oportunidades"][0]
        self.assertEqual(o["mejor_opcion"]["opcion_banco_id"], "HDU3_TEST_Q01_R1")
        self.assertEqual(o["mejor_opcion"]["texto"], "Reportar y bloquear este mensaje")
        self.assertEqual(o["mejor_opcion"]["impacto_puntuacion"], 2)
        # Distancia contra la mejor opción: +2 - (-1) = 3.
        self.assertEqual(o["puntos_perdidos"], 3)

    def test_una_respuesta_segura_no_es_una_oportunidad_de_mejora(self):
        self._elegir(self.optima, calidad="buena")
        self.assertEqual(self._oportunidades()["total"], 0)

    def test_la_segura_basica_tampoco_cuenta(self):
        """Es correcta aunque mejorable: mezclarla vaciaría de sentido la lista."""
        self._elegir(self.basica, calidad="buena")
        self.assertEqual(self._oportunidades()["total"], 0)

    def test_agrupa_por_zona_y_cuenta_el_total(self):
        otra = PreguntaBanco.objects.create(
            pregunta_id="HDU4_TEST_Q01", hdu="HDU-4", zona="reto_viral",
            categoria="reto_viral", mensaje_npc="saltas o subo el video",
        )
        insegura_otra = OpcionBanco.objects.create(
            pregunta=otra, opcion_id="HDU4_TEST_Q01_R2", texto="tirate rapido",
            tipo="insegura", consecuencia_narrativa="Otto se preocupa.",
            impacto_puntuacion=-1,
        )
        self._elegir(self.insegura)
        self._elegir(insegura_otra)

        d = self._oportunidades()
        self.assertEqual(d["total"], 2)
        self.assertEqual(
            d["por_zona"],
            [{"zona": "ciberacoso", "oportunidades": 1},
             {"zona": "reto_viral", "oportunidades": 1}],
        )

    def test_se_puede_filtrar_por_zona(self):
        otra = PreguntaBanco.objects.create(
            pregunta_id="HDU4_TEST_Q02", hdu="HDU-4", zona="reto_viral",
            categoria="reto_viral", mensaje_npc="saltas o subo el video",
        )
        insegura_otra = OpcionBanco.objects.create(
            pregunta=otra, opcion_id="HDU4_TEST_Q02_R2", texto="tirate rapido",
            tipo="insegura", consecuencia_narrativa="Otto se preocupa.",
            impacto_puntuacion=-1,
        )
        self._elegir(self.insegura)
        self._elegir(insegura_otra)

        d = self._oportunidades("?zona=ciberacoso")
        self.assertEqual(d["total"], 1)
        self.assertEqual(d["oportunidades"][0]["zona"], "ciberacoso")

    def test_una_opcion_que_no_existe_en_el_banco_se_ignora(self):
        """Contenido viejo o con typo no debe reventar el reporte del apoderado."""
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": "algo", "opcion_banco_id": "NO_EXISTE_R9"},
            self.token, 201,
        )
        self._elegir(self.insegura)

        d = self._oportunidades()
        self.assertEqual(d["total"], 1)
        self.assertEqual(d["oportunidades"][0]["eligio"]["opcion_banco_id"],
                         "HDU3_TEST_Q01_R3")

    def test_los_mensajes_sin_opcion_del_banco_no_aparecen(self):
        """El módulo de diálogo viejo no reporta la opción elegida."""
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "start", "respuesta": "hola"},
            self.token, 201,
        )
        self.assertEqual(self._oportunidades()["total"], 0)

    def test_otro_adulto_no_ve_las_oportunidades_de_la_partida_ajena(self):
        self._elegir(self.insegura)
        _, token_b = self._adulto("ajeno@test.local", "AdultoAjeno")
        self._oportunidades(token=token_b, espera=404)
