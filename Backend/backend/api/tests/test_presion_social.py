"""Verifica el nivel de presión social de Retos Virales (HDU-04 CA4).

El criterio pide que, tras **al menos dos retos virales rechazados de forma
consecutiva**, el siguiente NPC de la zona apriete más. El nivel no se guarda en
ninguna columna: se deriva del historial de decisiones que ya existe, igual que
`riesgo_por_zona`.

Lo que se protege acá, que es donde está la sutileza:

  - **Un reto es un NPC, no un mensaje.** Contestarle dos veces al mismo NPC es un
    solo reto. Si se contara por mensaje, un niño indeciso que después rechaza
    sumaría dos rechazos y la presión subiría antes de tiempo.
  - **De cada NPC manda su ÚLTIMA decisión.** Dudar y después rechazar es rechazar.
  - **"Consecutivos" es literal:** aceptar un reto, o quedarse en la duda, corta la
    racha y devuelve el nivel a cero.
  - Las decisiones de otras zonas no cuentan, aunque sean seguras.
  - El nivel es gradual y tiene tope.
"""
from api.models import Chat, NPC, OpcionBanco, PreguntaBanco

from .test_guardado_decisiones import BaseAPI


class PresionSocialTests(BaseAPI):
    """HDU-04 CA4: la presión sube sola cuando el niño lleva dos rechazos seguidos."""

    def setUp(self):
        self.adulto, self.token = self._adulto("presion@test.local", "AdultoPresion")
        self.partida = self._partida(self.adulto, nombre_menor="Perfil 1")
        self.npc = NPC.objects.create(
            partida=self.partida, nombre="Pinguino", area="reto_viral", tipo="enemigo"
        )
        self.chat = Chat.objects.create(
            partida=self.partida, npc=self.npc, categoria_riesgo="reto_viral"
        )
        # Un NPC del banco por cada reto de la zona, más uno de otra zona para
        # comprobar que no se cuela.
        self.retos = {
            npc_id: self._pregunta_con_opciones(npc_id, "reto_viral")
            for npc_id in ("NPC_05", "NPC_06", "NPC_09")
        }
        self.ajeno = self._pregunta_con_opciones("NPC_03", "ciberacoso")

    # ── Helpers ───────────────────────────────────────────────────────────────
    def _pregunta_con_opciones(self, npc_id, zona):
        """Réplica reducida de una apertura del banco: rechazar / dudar / aceptar."""
        pregunta = PreguntaBanco.objects.create(
            pregunta_id=f"TEST_{zona}_{npc_id}_Q01", hdu="HDU-4", zona=zona,
            categoria=zona, npc_id=npc_id, nivel_riesgo=3, es_mensaje_riesgo=True,
            mensaje_npc="si no lo haces no eres parte del grupo",
        )
        return {
            "pregunta": pregunta,
            "rechaza": OpcionBanco.objects.create(
                pregunta=pregunta, opcion_id=f"{pregunta.pregunta_id}_R1",
                texto="No lo voy a hacer", tipo="segura_optima",
                consecuencia_narrativa="Otto respira aliviado.", impacto_puntuacion=2,
            ),
            "duda": OpcionBanco.objects.create(
                pregunta=pregunta, opcion_id=f"{pregunta.pregunta_id}_R2",
                texto="No sé, lo pienso", tipo="dudosa",
                consecuencia_narrativa="Otto se queda callado.", impacto_puntuacion=0,
            ),
            "acepta": OpcionBanco.objects.create(
                pregunta=pregunta, opcion_id=f"{pregunta.pregunta_id}_R3",
                texto="Ya, lo hago", tipo="insegura",
                consecuencia_narrativa="Otto se preocupa.", impacto_puntuacion=-1,
            ),
        }

    def _decidir(self, reto, cual):
        opcion = reto[cual]
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": opcion.texto, "calidad_respuesta": "buena",
             "pregunta_banco_id": reto["pregunta"].pregunta_id,
             "opcion_banco_id": opcion.opcion_id},
            self.token, 201,
        )

    def _nivel(self, token=None, espera=200):
        return self.get(
            f"/api/partidas/{self.partida.pk}/presion-social/", token or self.token, espera
        )

    # ── Tests ─────────────────────────────────────────────────────────────────
    def test_sin_decisiones_la_presion_es_cero(self):
        d = self._nivel()
        self.assertEqual(d["SocialPressureLevel"], 0)
        self.assertEqual(d["rechazos_consecutivos"], 0)
        self.assertEqual(d["retos"], [])

    def test_un_solo_rechazo_no_alcanza_el_umbral(self):
        self._decidir(self.retos["NPC_05"], "rechaza")
        d = self._nivel()
        self.assertEqual(d["rechazos_consecutivos"], 1)
        self.assertEqual(d["SocialPressureLevel"], 0)

    def test_dos_rechazos_seguidos_suben_un_nivel(self):
        """Es el caso exacto del criterio de aceptación."""
        self._decidir(self.retos["NPC_05"], "rechaza")
        self._decidir(self.retos["NPC_06"], "rechaza")
        d = self._nivel()
        self.assertEqual(d["rechazos_consecutivos"], 2)
        self.assertEqual(d["SocialPressureLevel"], 1)

    def test_tres_rechazos_suben_otro_nivel(self):
        for npc in ("NPC_05", "NPC_06", "NPC_09"):
            self._decidir(self.retos[npc], "rechaza")
        d = self._nivel()
        self.assertEqual(d["SocialPressureLevel"], 2)

    def test_el_nivel_no_pasa_del_tope(self):
        for npc in ("NPC_05", "NPC_06", "NPC_09"):
            self._decidir(self.retos[npc], "rechaza")
        d = self._nivel()
        self.assertEqual(d["SocialPressureLevel"], d["nivel_maximo"])

    def test_aceptar_un_reto_corta_la_racha(self):
        self._decidir(self.retos["NPC_05"], "rechaza")
        self._decidir(self.retos["NPC_06"], "rechaza")
        self._decidir(self.retos["NPC_09"], "acepta")
        d = self._nivel()
        self.assertEqual(d["rechazos_consecutivos"], 0)
        self.assertEqual(d["SocialPressureLevel"], 0)

    def test_quedarse_en_la_duda_tambien_corta_la_racha(self):
        self._decidir(self.retos["NPC_05"], "rechaza")
        self._decidir(self.retos["NPC_06"], "rechaza")
        self._decidir(self.retos["NPC_09"], "duda")
        d = self._nivel()
        self.assertEqual(d["rechazos_consecutivos"], 0)
        self.assertEqual(d["SocialPressureLevel"], 0)

    def test_dudar_y_despues_rechazar_al_mismo_npc_es_UN_solo_rechazo(self):
        """La trampa de contar por mensaje en vez de por NPC.

        Dos mensajes, un solo reto: la presión no debe subir todavía.
        """
        self._decidir(self.retos["NPC_05"], "duda")
        self._decidir(self.retos["NPC_05"], "rechaza")
        d = self._nivel()
        self.assertEqual(len(d["retos"]), 1)
        self.assertEqual(d["retos"][0]["resultado"], "rechazado")
        self.assertEqual(d["rechazos_consecutivos"], 1)
        self.assertEqual(d["SocialPressureLevel"], 0)

    def test_manda_la_ultima_decision_del_npc(self):
        """Rechazar y después ceder al mismo NPC cuenta como reto aceptado."""
        self._decidir(self.retos["NPC_05"], "rechaza")
        self._decidir(self.retos["NPC_06"], "rechaza")
        self._decidir(self.retos["NPC_06"], "acepta")
        d = self._nivel()
        self.assertEqual(d["retos"][-1]["resultado"], "aceptado")
        self.assertEqual(d["rechazos_consecutivos"], 0)

    def test_las_decisiones_de_otra_zona_no_cuentan(self):
        self._decidir(self.retos["NPC_05"], "rechaza")
        self._decidir(self.ajeno, "rechaza")          # ciberacoso
        self._decidir(self.retos["NPC_06"], "rechaza")
        d = self._nivel()
        self.assertEqual([r["npc_id"] for r in d["retos"]], ["NPC_05", "NPC_06"])
        self.assertEqual(d["SocialPressureLevel"], 1)

    def test_una_opcion_que_no_existe_en_el_banco_se_ignora(self):
        self._decidir(self.retos["NPC_05"], "rechaza")
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": "?", "calidad_respuesta": "buena",
             "pregunta_banco_id": "NO_EXISTE_Q01", "opcion_banco_id": "NO_EXISTE_Q01_R1"},
            self.token, 201,
        )
        self._decidir(self.retos["NPC_06"], "rechaza")
        d = self._nivel()
        self.assertEqual(d["SocialPressureLevel"], 1)

    def test_un_adulto_no_ve_la_presion_de_la_partida_de_otro(self):
        _, token_ajeno = self._adulto("otro@test.local", "AdultoOtro")
        self._nivel(token=token_ajeno, espera=404)
