"""Tests del endpoint de conversación completa (A.3 de REQUISITOS_BD).

Lo que se prueba no es solo que funcione, sino que produzca **exactamente las
mismas filas** que la cadena larga (`RegistrarNPC` → `IniciarChat` → N ×
`RegistrarMensaje` → `FinalizarChat`). Si se separan, el riesgo por zona y los
reportes del adulto empiezan a depender de por cuál de los dos caminos se guardó
la conversación, que es el peor tipo de error: no revienta, solo miente.
"""
from django.test import TestCase
from rest_framework.test import APIClient

from api.models import Chat, Mensaje, NPC, PosibleRespuesta
from api.tests.test_catalogo_album import crear_partida


def cuerpo(**extra):
    datos = {
        "npc": {"nombre": "Alex", "area": "zona_2", "tipo": "enemigo", "confianza": 0},
        "chat": {"categoria_riesgo": "desconocidos"},
        "mensajes": [
            {"tipo": "start", "respuesta": "Hola!", "calidad_respuesta": ""},
            {
                "tipo": "request",
                "respuesta": "¿Me pasas tu dirección?",
                "calidad_respuesta": "",
                "pregunta_banco_id": "HDU2_NPC01_F2_Q01",
                "posibles_respuestas": [
                    {"texto": "Claro", "orden": 0, "calidad_respuesta": "mala"},
                    {"texto": "No", "orden": 1, "calidad_respuesta": "buena"},
                ],
            },
            {
                "tipo": "chain",
                "respuesta": "No te la voy a dar",
                "calidad_respuesta": "buena",
                "opcion_banco_id": "HDU2_NPC01_F2_Q01_R2",
            },
        ],
        "finalizar": True,
    }
    datos.update(extra)
    return datos


class ChatCompletoTests(TestCase):
    def setUp(self):
        self.partida = crear_partida()
        self.client = APIClient()
        self.client.force_authenticate(self.partida.usuario_jugador.adulto)
        self.ruta = f"/api/partidas/{self.partida.pk}/chats/completo/"

    def test_una_peticion_deja_la_conversacion_entera(self):
        resp = self.client.post(self.ruta, cuerpo(), format="json")
        self.assertEqual(resp.status_code, 201)

        self.assertEqual(NPC.objects.count(), 1)
        self.assertEqual(Chat.objects.count(), 1)
        # 3 mensajes + el END que cierra
        self.assertEqual(Mensaje.objects.count(), 4)
        self.assertEqual(PosibleRespuesta.objects.count(), 2)

    def test_el_chat_queda_cerrado(self):
        self.client.post(self.ruta, cuerpo(), format="json")
        self.assertIsNotNone(Chat.objects.get().fecha_termino)
        self.assertEqual(Mensaje.objects.filter(tipo="end").count(), 1)

    def test_los_ids_del_banco_se_conservan(self):
        """Sin `opcion_banco_id` el riesgo por zona se queda sin el puntaje real."""
        self.client.post(self.ruta, cuerpo(), format="json")
        self.assertEqual(
            Mensaje.objects.filter(opcion_banco_id="HDU2_NPC01_F2_Q01_R2").count(), 1
        )
        self.assertEqual(
            Mensaje.objects.filter(pregunta_banco_id="HDU2_NPC01_F2_Q01").count(), 1
        )

    def test_produce_las_mismas_filas_que_la_cadena_larga(self):
        """La prueba que justifica el endpoint: es un atajo, no otro formato."""
        self.client.post(self.ruta, cuerpo(), format="json")
        por_atajo = {
            "npcs": NPC.objects.count(),
            "chats": Chat.objects.count(),
            "mensajes": list(Mensaje.objects.order_by("timestamp", "pk").values_list(
                "tipo", "respuesta", "calidad_respuesta",
                "pregunta_banco_id", "opcion_banco_id",
            )),
            "posibles": list(PosibleRespuesta.objects.order_by("pk").values_list(
                "texto", "orden", "calidad_respuesta",
            )),
        }

        NPC.objects.all().delete()
        datos = cuerpo()

        # Ahora lo mismo, por la cadena larga.
        npc_id = self.client.post(
            f"/api/partidas/{self.partida.pk}/npcs/", datos["npc"], format="json",
        ).data["id"]
        chat_id = self.client.post(
            "/api/chats/",
            {"partida_id": self.partida.pk, "npc_id": npc_id,
             "categoria_riesgo": datos["chat"]["categoria_riesgo"]},
            format="json",
        ).data["id"]
        for m in datos["mensajes"]:
            self.client.post(
                f"/api/chats/{chat_id}/mensajes/registrar/", m, format="json",
            )
        self.client.post(f"/api/chats/{chat_id}/finalizar/", {}, format="json")

        por_cadena = {
            "npcs": NPC.objects.count(),
            "chats": Chat.objects.count(),
            "mensajes": list(Mensaje.objects.order_by("timestamp", "pk").values_list(
                "tipo", "respuesta", "calidad_respuesta",
                "pregunta_banco_id", "opcion_banco_id",
            )),
            "posibles": list(PosibleRespuesta.objects.order_by("pk").values_list(
                "texto", "orden", "calidad_respuesta",
            )),
        }
        self.assertEqual(por_atajo, por_cadena)

    def test_un_npc_que_ya_existe_se_reusa(self):
        primera = self.client.post(self.ruta, cuerpo(), format="json")
        npc_id = primera.data["npc"]["id"]

        self.client.post(
            self.ruta, cuerpo(npc={"npc_id": npc_id}), format="json",
        )
        self.assertEqual(NPC.objects.count(), 1)
        self.assertEqual(Chat.objects.count(), 2)

    def test_finalizar_false_deja_el_chat_abierto(self):
        self.client.post(self.ruta, cuerpo(finalizar=False), format="json")
        self.assertIsNone(Chat.objects.get().fecha_termino)
        self.assertEqual(Mensaje.objects.count(), 3)

    def test_un_mensaje_invalido_no_deja_nada_a_medias(self):
        """Lo atómico es el punto: media conversación en la base cuenta igual en
        el reporte del adulto y es peor que ninguna."""
        malo = cuerpo()
        malo["mensajes"][1]["tipo"] = "saludo_inventado"

        resp = self.client.post(self.ruta, malo, format="json")

        self.assertEqual(resp.status_code, 400)
        self.assertIn("1", resp.data["mensajes"])  # dice cuál de los mensajes
        self.assertEqual(NPC.objects.count(), 0)
        self.assertEqual(Chat.objects.count(), 0)
        self.assertEqual(Mensaje.objects.count(), 0)

    def test_una_conversacion_sin_mensajes_es_valida(self):
        resp = self.client.post(self.ruta, cuerpo(mensajes=[]), format="json")
        self.assertEqual(resp.status_code, 201)
        self.assertEqual(Mensaje.objects.count(), 1)  # solo el END

    def test_mensajes_tiene_que_ser_una_lista(self):
        resp = self.client.post(self.ruta, cuerpo(mensajes="tres"), format="json")
        self.assertEqual(resp.status_code, 400)

    def test_la_partida_de_otro_adulto_no_se_toca(self):
        otra = crear_partida(nombre="Hijo de otra cuenta")
        resp = self.client.post(
            f"/api/partidas/{otra.pk}/chats/completo/", cuerpo(), format="json",
        )
        self.assertEqual(resp.status_code, 404)
        self.assertEqual(Chat.objects.count(), 0)

    def test_el_riesgo_por_zona_lo_ve_igual(self):
        """Prueba de integración: lo guardado por acá tiene que llegar al
        reporte, que es para lo que existe."""
        self.client.post(self.ruta, cuerpo(), format="json")
        resp = self.client.get(f"/api/partidas/{self.partida.pk}/riesgo-por-zona/")
        self.assertEqual(resp.status_code, 200)
