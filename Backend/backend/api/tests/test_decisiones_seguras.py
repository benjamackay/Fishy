"""Verifica el porcentaje de decisiones seguras que elige el final (HDU-09).

Al terminar la Misión 6 el juego pide este porcentaje y muestra el Final A, B o C
según los umbrales del banco. Lo que se protege acá:

  - **Se cuenta cada decisión, no cada NPC.** El final resume toda la aventura,
    sub-decisiones incluidas (a diferencia de la presión social, que va por NPC).
  - **Las dudosas no suman ni restan**, igual que el estado de Otto en el chat.
  - Entran las tres zonas del recorrido y nada más.
  - Sin decisiones contadas el porcentaje es null, no 0 ni 100: inventar un número
    ahí elegiría un final que el niño/a no se ganó.
  - Cada adulto ve solo sus partidas.
"""
from api.models import Chat, NPC, OpcionBanco, PreguntaBanco

from .test_guardado_decisiones import BaseAPI


class DecisionesSegurasTests(BaseAPI):

    def setUp(self):
        self.adulto, self.token = self._adulto("finales@test.local", "AdultoFinales")
        self.partida = self._partida(self.adulto, nombre_menor="Perfil 1")
        self.npc = NPC.objects.create(
            partida=self.partida, nombre="Lobo Marino", area="reto_viral", tipo="enemigo"
        )
        self.chat = Chat.objects.create(
            partida=self.partida, npc=self.npc, categoria_riesgo="reto_viral"
        )
        self.zonas = {
            z: self._pregunta(z) for z in ("desconocidos", "ciberacoso", "reto_viral")
        }
        self.fuera = self._pregunta("otra_zona")

    def _pregunta(self, zona):
        p = PreguntaBanco.objects.create(
            pregunta_id=f"TEST_{zona}_Q01", hdu="HDU-2", zona=zona, categoria=zona,
            npc_id="NPC_X", nivel_riesgo=2, es_mensaje_riesgo=True, mensaje_npc="¿?",
        )
        ops = {}
        for sufijo, tipo, impacto in (("R1", "segura_optima", 2), ("R2", "segura_basica", 1),
                                      ("R3", "dudosa", 0), ("R4", "insegura", -1)):
            ops[tipo] = OpcionBanco.objects.create(
                pregunta=p, opcion_id=f"{p.pregunta_id}_{sufijo}", texto=tipo, tipo=tipo,
                consecuencia_narrativa="Otto reacciona.", impacto_puntuacion=impacto,
            )
        return {"pregunta": p, **ops}

    def _decidir(self, zona, tipo):
        q = self.zonas[zona] if zona in self.zonas else self.fuera
        opcion = q[tipo]
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": opcion.texto, "calidad_respuesta": "buena",
             "pregunta_banco_id": q["pregunta"].pregunta_id,
             "opcion_banco_id": opcion.opcion_id},
            self.token, 201,
        )

    def _resumen(self, token=None, espera=200):
        return self.get(
            f"/api/partidas/{self.partida.pk}/decisiones-seguras/", token or self.token, espera
        )

    def test_sin_decisiones_el_porcentaje_es_null(self):
        d = self._resumen()
        self.assertIsNone(d["porcentaje"])
        self.assertEqual((d["seguras"], d["inseguras"], d["dudosas"]), (0, 0, 0))

    def test_suma_las_tres_zonas_y_cuenta_cada_decision(self):
        self._decidir("desconocidos", "segura_optima")
        self._decidir("desconocidos", "segura_basica")   # mismo NPC: cuenta igual
        self._decidir("ciberacoso", "insegura")
        self._decidir("reto_viral", "segura_optima")
        d = self._resumen()
        self.assertEqual(d["seguras"], 3)
        self.assertEqual(d["inseguras"], 1)
        self.assertEqual(d["porcentaje"], 75.0)
        self.assertEqual(d["por_zona"]["desconocidos"]["seguras"], 2)

    def test_las_dudosas_no_suman_ni_restan(self):
        self._decidir("ciberacoso", "segura_optima")
        self._decidir("ciberacoso", "dudosa")
        self._decidir("ciberacoso", "dudosa")
        d = self._resumen()
        self.assertEqual(d["dudosas"], 2)
        self.assertEqual(d["porcentaje"], 100.0)

    def test_solo_dudosas_deja_el_porcentaje_en_null(self):
        self._decidir("reto_viral", "dudosa")
        self.assertIsNone(self._resumen()["porcentaje"])

    def test_una_zona_fuera_del_recorrido_no_cuenta(self):
        self._decidir("reto_viral", "insegura")
        self._decidir("otra_zona", "segura_optima")
        d = self._resumen()
        self.assertEqual((d["seguras"], d["inseguras"]), (0, 1))
        self.assertEqual(d["porcentaje"], 0.0)

    def test_otro_adulto_no_ve_la_partida(self):
        _, token_ajeno = self._adulto("ajeno@test.local", "AdultoAjeno")
        self._resumen(token=token_ajeno, espera=404)
