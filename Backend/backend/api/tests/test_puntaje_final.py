"""Verifica el puntaje que elige el final (HDU-09).

Al terminar la Misión 6 el juego pide este puntaje y muestra el Final A, B o C según
los umbrales del banco (A desde 70 %, B desde 40 %, C debajo). La regla:

  - Se suman los puntos de **todas** las opciones elegidas en los chats de las tres
    zonas (+2, +1, 0, -1), las respuestas de seguimiento incluidas.
  - Un total negativo cuenta como 0.
  - El máximo es lo que suma quien elige la mejor ruta en cada chat, calculado desde
    el banco: con seguimiento, la mejor ruta puede sumar más que la mejor opción
    inicial. Las variantes `_BASE` de un escenario cuentan una sola vez.
  - El porcentaje tiene tope en 100 (repetir un chat no da más de 100).
"""
from django.core.management import call_command
from django.test import TestCase
from io import StringIO

from api.models import Chat, NPC, OpcionBanco, PreguntaBanco
from api.views import puntos_maximos_del_recorrido

from .test_guardado_decisiones import BaseAPI


def opcion(pregunta, sufijo, impacto, siguiente=""):
    return OpcionBanco.objects.create(
        pregunta=pregunta, opcion_id=f"{pregunta.pregunta_id}_{sufijo}", texto=sufijo,
        tipo="segura_optima" if impacto > 0 else ("dudosa" if impacto == 0 else "insegura"),
        consecuencia_narrativa="Otto reacciona.", impacto_puntuacion=impacto,
        siguiente_pregunta=siguiente,
    )


def pregunta(pid, escenario, zona="desconocidos", fin=False):
    return PreguntaBanco.objects.create(
        pregunta_id=pid, hdu="HDU-2", zona=zona, categoria=zona, npc_id="NPC_X",
        escenario_id=escenario, nivel_riesgo=2, es_mensaje_riesgo=not fin,
        es_fin_de_npc=fin, mensaje_npc="¿?",
    )


class PuntajeFinalTests(BaseAPI):

    def setUp(self):
        self.adulto, self.token = self._adulto("finales@test.local", "AdultoFinales")
        self.partida = self._partida(self.adulto, nombre_menor="Perfil 1")
        npc = NPC.objects.create(partida=self.partida, nombre="Puma", area="desconocidos", tipo="enemigo")
        self.chat = Chat.objects.create(partida=self.partida, npc=npc, categoria_riesgo="desconocidos")

        # Chat con seguimiento, como M1: la óptima (+2) lleva a otra decisión (+1).
        q = pregunta("T_Q01", "CHAT_A")
        seg = pregunta("T_Q01_OPTIMA", "CHAT_A")
        pregunta("T_FIN", "CHAT_A", fin=True)
        self.op = {
            "optima": opcion(q, "R1", 2, "T_Q01_OPTIMA"),
            "dudosa": opcion(q, "R3", 0, "T_FIN"),
            "insegura": opcion(q, "R4", -1, "T_FIN"),
            "seg_buena": opcion(seg, "R1", 1, "T_FIN"),
            "seg_mala": opcion(seg, "R2", -1, "T_FIN"),
        }
        # Dos variantes del mismo chat final: se juega una u otra.
        v = pregunta("T_M6", "FINAL_X", zona="reto_viral")
        vb = pregunta("T_M6B", "FINAL_X_BASE", zona="reto_viral")
        self.op["m6"] = opcion(v, "R1", 2)
        opcion(vb, "R1", 2)
        # Una zona fuera del recorrido no suma ni al máximo ni a los puntos.
        fuera = pregunta("T_OTRA", "CHAT_OTRO", zona="otra_zona")
        self.op["fuera"] = opcion(fuera, "R1", 2)

    def _elegir(self, clave):
        o = self.op[clave]
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": o.texto, "calidad_respuesta": "buena",
             "pregunta_banco_id": o.pregunta.pregunta_id, "opcion_banco_id": o.opcion_id},
            self.token, 201,
        )

    def _puntaje(self, token=None, espera=200):
        return self.get(f"/api/partidas/{self.partida.pk}/puntaje-final/", token or self.token, espera)

    def test_el_maximo_sigue_la_mejor_ruta_y_cuenta_una_vez_cada_variante(self):
        total, por_escenario = puntos_maximos_del_recorrido()
        self.assertEqual(por_escenario["CHAT_A"], 3)          # +2 y después +1
        self.assertEqual(por_escenario["FINAL_X"], 2)
        self.assertEqual(por_escenario["FINAL_X_BASE"], 2)
        self.assertNotIn("CHAT_OTRO", por_escenario)
        self.assertEqual(total, 5)                            # 3 + 2, no 3 + 2 + 2

    def test_sin_decisiones_da_cero(self):
        d = self._puntaje()
        self.assertEqual((d["puntos"], d["puntos_maximos"], d["porcentaje"]), (0, 5, 0.0))

    def test_suma_las_respuestas_de_seguimiento(self):
        self._elegir("optima")
        self._elegir("seg_buena")
        self._elegir("m6")
        d = self._puntaje()
        self.assertEqual(d["puntos"], 5)
        self.assertEqual(d["porcentaje"], 100.0)
        self.assertEqual(d["por_zona"]["desconocidos"], 3)

    def test_un_total_negativo_cuenta_como_cero(self):
        self._elegir("insegura")
        self._elegir("insegura")
        d = self._puntaje()
        self.assertEqual(d["puntos_brutos"], -2)
        self.assertEqual(d["puntos"], 0)
        self.assertEqual(d["porcentaje"], 0.0)

    def test_repetir_un_chat_no_pasa_de_100(self):
        for _ in range(3):
            self._elegir("optima")
            self._elegir("seg_buena")
        self.assertEqual(self._puntaje()["porcentaje"], 100.0)

    def test_una_zona_fuera_del_recorrido_no_suma(self):
        self._elegir("fuera")
        self._elegir("dudosa")
        d = self._puntaje()
        self.assertEqual(d["puntos"], 0)
        self.assertEqual(d["decisiones"], 1)

    def test_otro_adulto_no_ve_la_partida(self):
        _, token_ajeno = self._adulto("ajeno@test.local", "AdultoAjeno")
        self._puntaje(token=token_ajeno, espera=404)


class MaximoDelBancoRealTests(TestCase):
    """Con el banco de verdad, los 8 chats con decisión suman 18. Si el banco cambia,
    este número cambia: es la señal para mirar si los finales siguen calzando."""

    def test_el_banco_real_da_18(self):
        call_command("cargar_banco", stdout=StringIO())
        total, por_escenario = puntos_maximos_del_recorrido()
        self.assertEqual(por_escenario["M1_CHAT01"], 3)
        self.assertEqual(por_escenario["M4_FASE01"], 3)
        self.assertEqual(total, 18)
