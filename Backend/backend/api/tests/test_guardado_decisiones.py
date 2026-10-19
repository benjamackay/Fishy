"""Verifica que las decisiones del niño terminen realmente escritas en la base.

No mira solo el 201: después de cada POST vuelve a leer la fila desde la BD y
comprueba campo por campo. Son los dos caminos por los que Unity guarda una
decisión:

  - HDU-2/8: la opción elegida en un chat     -> Mensaje.opcion_banco_id
  - HDU-10:  los mensajes marcados como riesgo -> CasoDetectiveProgreso

`opcion_banco_id` y `mensajes_marcados` son los dos campos que después alimentan
el panel del apoderado: si se pierden en el guardado, no se nota hasta que el
apoderado abre la pantalla y la ve vacía.
"""
from django.test import TestCase

from api.models import (
    AdultoResponsable,
    CasoDetective,
    CasoDetectiveProgreso,
    Chat,
    Mensaje,
    MensajeDetective,
    NPC,
    NivelRiesgo,
    OpcionBanco,
    Partida,
    PreguntaBanco,
    UsuarioJugador,
)


class BaseAPI(TestCase):
    """Adulto + menor + partida + token: lo mínimo para poder guardar algo."""

    def _adulto(self, email, nombre):
        adulto = AdultoResponsable.objects.create_user(
            email=email, nombre=nombre, password="clave-de-prueba-123"
        )
        r = self.client.post(
            "/api/auth/login/",
            {"nombre": nombre, "password": "clave-de-prueba-123"},
            content_type="application/json",
        )
        self.assertEqual(r.status_code, 200, r.content)
        return adulto, r.json()["token"]

    def _partida(self, adulto, nombre_menor="Otto"):
        jugador = UsuarioJugador.objects.create(adulto=adulto, nombre=nombre_menor, edad=9)
        nivel, _ = NivelRiesgo.objects.get_or_create(
            nombre="bajo", defaults={"descripcion": "prueba"}
        )
        return Partida.objects.create(usuario_jugador=jugador, nivel_riesgo=nivel)

    def post(self, ruta, body, token, espera):
        r = self.client.post(
            ruta, body, content_type="application/json", HTTP_AUTHORIZATION=f"Token {token}"
        )
        self.assertEqual(r.status_code, espera, r.content)
        return r.json() if r.content else None

    def get(self, ruta, token, espera=200):
        r = self.client.get(ruta, HTTP_AUTHORIZATION=f"Token {token}")
        self.assertEqual(r.status_code, espera, r.content)
        return r.json() if r.content else None


class GuardarDecisionDeChatTests(BaseAPI):
    """HDU-2/8: la opción que el niño eligió al responderle a un NPC."""

    def setUp(self):
        self.adulto, self.token = self._adulto("chat@test.local", "AdultoChat")
        self.partida = self._partida(self.adulto)
        self.npc = NPC.objects.create(partida=self.partida, nombre="Puma", area="desconocidos")
        self.chat = Chat.objects.create(partida=self.partida, npc=self.npc)

        self.pregunta = PreguntaBanco.objects.create(
            pregunta_id="HDU2_TEST_Q01", hdu="HDU-2", zona="desconocidos",
            categoria="prueba", mensaje_npc="me pasas tu direccion?",
        )
        OpcionBanco.objects.create(
            pregunta=self.pregunta, opcion_id="HDU2_TEST_Q01_R1",
            texto="no comparto mi direccion", tipo="segura_optima",
            consecuencia_narrativa="Otto se protege", impacto_puntuacion=2,
        )

    def test_la_opcion_elegida_queda_guardada_con_su_id_del_banco(self):
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {
                "tipo": "request",
                "respuesta": "no comparto mi direccion",
                "calidad_respuesta": "buena",
                "pregunta_banco_id": "HDU2_TEST_Q01",
                "opcion_banco_id": "HDU2_TEST_Q01_R1",
                "posibles_respuestas": [
                    {"texto": "le doy mi direccion", "orden": 0, "calidad_respuesta": "mala"},
                    {"texto": "no comparto mi direccion", "orden": 1, "calidad_respuesta": "buena"},
                ],
            },
            self.token, 201,
        )

        m = Mensaje.objects.get(chat=self.chat)
        self.assertEqual(m.tipo, "request")
        self.assertEqual(m.respuesta, "no comparto mi direccion")
        self.assertEqual(m.calidad_respuesta, "buena")
        self.assertEqual(m.pregunta_banco_id, "HDU2_TEST_Q01")
        self.assertEqual(m.opcion_banco_id, "HDU2_TEST_Q01_R1")
        self.assertEqual(m.posibles_respuestas.count(), 2)
        self.assertEqual(
            list(m.posibles_respuestas.values_list("texto", flat=True)),
            ["le doy mi direccion", "no comparto mi direccion"],
        )

    def test_lo_guardado_se_puede_volver_a_leer_por_la_api(self):
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": "ok", "opcion_banco_id": "HDU2_TEST_Q01_R1"},
            self.token, 201,
        )
        msgs = self.get(f"/api/chats/{self.chat.pk}/mensajes/", self.token)
        self.assertEqual(len(msgs), 1)
        self.assertEqual(msgs[0]["opcion_banco_id"], "HDU2_TEST_Q01_R1")

    def test_la_decision_guardada_suma_riesgo_en_su_zona(self):
        """Guardar no sirve de nada si el panel del apoderado no lo ve."""
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": "ok", "pregunta_banco_id": "HDU2_TEST_Q01",
             "opcion_banco_id": "HDU2_TEST_Q01_R1"},
            self.token, 201,
        )
        riesgo = self.get(f"/api/partidas/{self.partida.pk}/riesgo-por-zona/", self.token)
        # `total` es la suma de impacto_puntuacion (segura_optima = +2), no el conteo.
        self.assertEqual(riesgo["total"], 2)
        self.assertEqual(riesgo["respuestas"], 1)
        self.assertEqual(riesgo["sin_clasificar"], 0)
        self.assertEqual([z["zona"] for z in riesgo["zonas"]], ["desconocidos"])

    def test_un_chat_cerrado_no_acepta_mas_decisiones(self):
        self.post(f"/api/chats/{self.chat.pk}/finalizar/", {"respuesta": "fin"}, self.token, 200)
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": "tarde"},
            self.token, 400,
        )

    def test_otro_adulto_no_puede_escribir_en_el_chat_ajeno(self):
        _, token_b = self._adulto("otro@test.local", "AdultoAjeno")
        self.post(
            f"/api/chats/{self.chat.pk}/mensajes/registrar/",
            {"tipo": "chain", "respuesta": "intruso"},
            token_b, 404,
        )
        self.assertEqual(Mensaje.objects.count(), 0)


class GuardarDecisionDetectiveTests(BaseAPI):
    """HDU-10: los mensajes que el niño marco como senal de riesgo."""

    def setUp(self):
        self.adulto, self.token = self._adulto("detective@test.local", "AdultoDetective")
        self.partida = self._partida(self.adulto)
        self.caso = CasoDetective.objects.create(
            caso_id="caso_test_01", titulo="Caso de prueba", zona="playa",
            etiquetas_ml=["grooming"],
            permiso_player_text="me muestras el chat?",
            permiso_npc_nombre="Alex", permiso_npc_response="ya, mira",
        )
        for i, riesgo in enumerate([True, False, True], start=1):
            MensajeDetective.objects.create(
                caso=self.caso, mensaje_id=f"caso_test_01_m{i}", npc_sender="Alex",
                texto=f"mensaje {i}", es_senal_riesgo=riesgo, orden=i,
            )

    def _cuerpo(self, marcados, aciertos):
        return {
            "partida_id": self.partida.pk,
            "mensajes_marcados": marcados,
            # Estos tres los recalcula el servidor; se mandan igual porque es lo que
            # sigue enviando Unity y hay que comprobar que no rompe (ver CorreccionEnElServidorTests).
            "aciertos": aciertos,
            "total_riesgo": 2,
            "porcentaje": aciertos / 2,
        }

    def test_el_primer_intento_crea_la_fila_con_lo_marcado(self):
        self.post(
            f"/api/casos-detective/{self.caso.caso_id}/progreso/",
            self._cuerpo(["caso_test_01_m1"], 1),
            self.token, 201,
        )
        p = CasoDetectiveProgreso.objects.get(partida=self.partida, caso=self.caso)
        self.assertEqual(p.mensajes_marcados, ["caso_test_01_m1"])
        self.assertEqual(p.aciertos, 1)
        self.assertEqual(p.total_riesgo, 2)
        self.assertEqual(p.porcentaje, 0.5)
        self.assertEqual(p.intentos, 1)
        self.assertIsNotNone(p.fecha_termino)

    def test_reintentar_no_duplica_la_fila_y_sobrescribe_el_resultado(self):
        self.post(
            f"/api/casos-detective/{self.caso.caso_id}/progreso/",
            self._cuerpo(["caso_test_01_m1"], 1),
            self.token, 201,
        )
        self.post(
            f"/api/casos-detective/{self.caso.caso_id}/progreso/",
            self._cuerpo(["caso_test_01_m1", "caso_test_01_m3"], 2),
            self.token, 200,
        )
        self.assertEqual(CasoDetectiveProgreso.objects.count(), 1)
        p = CasoDetectiveProgreso.objects.get()
        self.assertEqual(p.mensajes_marcados, ["caso_test_01_m1", "caso_test_01_m3"])
        self.assertEqual(p.aciertos, 2)
        self.assertEqual(p.porcentaje, 1.0)
        self.assertEqual(p.intentos, 2)

    def test_lo_guardado_vuelve_por_la_api_de_la_partida(self):
        self.post(
            f"/api/casos-detective/{self.caso.caso_id}/progreso/",
            self._cuerpo(["caso_test_01_m1", "caso_test_01_m3"], 2),
            self.token, 201,
        )
        progresos = self.get(f"/api/partidas/{self.partida.pk}/casos-detective/", self.token)
        self.assertEqual(len(progresos), 1)
        self.assertEqual(
            progresos[0]["mensajes_marcados"], ["caso_test_01_m1", "caso_test_01_m3"]
        )
        self.assertEqual(progresos[0]["aciertos"], 2)

    def test_un_caso_que_no_existe_no_guarda_nada(self):
        self.post(
            "/api/casos-detective/caso_inventado/progreso/",
            self._cuerpo(["x"], 0),
            self.token, 404,
        )
        self.assertEqual(CasoDetectiveProgreso.objects.count(), 0)

    def test_otro_adulto_no_puede_guardar_en_la_partida_ajena(self):
        _, token_b = self._adulto("ajeno@test.local", "AdultoAjenoDet")
        self.post(
            f"/api/casos-detective/{self.caso.caso_id}/progreso/",
            self._cuerpo(["caso_test_01_m1"], 1),
            token_b, 404,
        )
        self.assertEqual(CasoDetectiveProgreso.objects.count(), 0)



class CorreccionEnElServidorTests(BaseAPI):
    """HDU-10 CA4 y CA5: el resultado lo corrige el servidor contra las señales del
    propio caso, no lo copia del cliente.

    El cliente sigue mandando sus números (Unity los necesita para pintar la
    pantalla al tiro), pero lo que queda guardado es lo que da el caso. Importa
    porque de acá sale el reporte del adulto responsable en HDU-13: si el juego
    tiene un bug o alguien manda la petición a mano, los números quedarían sin
    forma de auditarse."""

    def setUp(self):
        self.adulto, self.token = self._adulto("correccion@test.local", "AdultoCorreccion")
        self.partida = self._partida(self.adulto)
        self.caso = CasoDetective.objects.create(
            caso_id="caso_corr_01", titulo="Caso con ambiguo", zona="ciberacoso",
            permiso_player_text="me muestras?", permiso_npc_nombre="Pudu",
            permiso_npc_response="ya",
        )
        # 2 señales de riesgo reales, 1 ambigua y 1 inocente.
        for i, (riesgo, ambiguo) in enumerate(
            [(True, False), (False, False), (True, True), (True, False)], start=1
        ):
            MensajeDetective.objects.create(
                caso=self.caso, mensaje_id=f"caso_corr_01_m{i}", npc_sender="Puma",
                texto=f"mensaje {i}", es_senal_riesgo=riesgo, es_ambiguo=ambiguo, orden=i,
            )
        self.ruta = f"/api/casos-detective/{self.caso.caso_id}/progreso/"

    def _marcar(self, marcados, extra=None, espera=201):
        cuerpo = {"partida_id": self.partida.pk, "mensajes_marcados": marcados}
        if extra:
            cuerpo.update(extra)
        return self.post(self.ruta, cuerpo, self.token, espera)

    def test_no_hace_falta_mandar_los_numeros(self):
        # El cuerpo mínimo es la partida y lo que el niño/a marcó.
        datos = self._marcar(["caso_corr_01_m1"])
        self.assertEqual(datos["aciertos"], 1)
        self.assertEqual(datos["total_riesgo"], 2)
        self.assertEqual(datos["porcentaje"], 0.5)

    def test_el_servidor_ignora_los_aciertos_que_manda_el_cliente(self):
        datos = self._marcar(
            ["caso_corr_01_m1"],
            {"aciertos": 99, "total_riesgo": 99, "porcentaje": 1.0},
        )
        self.assertEqual(datos["aciertos"], 1)
        self.assertEqual(datos["total_riesgo"], 2)
        self.assertEqual(datos["porcentaje"], 0.5)

        fila = CasoDetectiveProgreso.objects.get()
        self.assertEqual(fila.aciertos, 1)
        self.assertEqual(fila.porcentaje, 0.5)

    def test_un_ambiguo_marcado_no_suma_ni_resta(self):
        # CA5: marcar el ambiguo no puede mejorar ni empeorar el resultado.
        solo_riesgo = self._marcar(["caso_corr_01_m1"])
        CasoDetectiveProgreso.objects.all().delete()
        con_ambiguo = self._marcar(["caso_corr_01_m1", "caso_corr_01_m3"])

        self.assertEqual(con_ambiguo["aciertos"], solo_riesgo["aciertos"])
        self.assertEqual(con_ambiguo["porcentaje"], solo_riesgo["porcentaje"])

    def test_el_ambiguo_no_entra_en_el_total(self):
        # m3 tiene es_senal_riesgo=True pero es ambiguo: no cuenta para el total.
        datos = self._marcar([])
        self.assertEqual(datos["total_riesgo"], 2)

    def test_marcar_un_mensaje_inocente_no_suma(self):
        datos = self._marcar(["caso_corr_01_m2"])
        self.assertEqual(datos["aciertos"], 0)
        self.assertEqual(datos["porcentaje"], 0.0)

    def test_un_id_que_no_esta_en_el_caso_no_suma(self):
        datos = self._marcar(["caso_corr_01_m1", "mensaje_de_otro_caso"])
        self.assertEqual(datos["aciertos"], 1)

    def test_marcarlas_todas_da_el_cien_por_ciento(self):
        datos = self._marcar(["caso_corr_01_m1", "caso_corr_01_m4"])
        self.assertEqual(datos["aciertos"], 2)
        self.assertEqual(datos["porcentaje"], 1.0)

    def test_reintentar_recalcula_con_las_marcas_nuevas(self):
        self._marcar(["caso_corr_01_m1"])
        datos = self._marcar(
            ["caso_corr_01_m1", "caso_corr_01_m4"],
            {"aciertos": 0},   # el cliente miente, el servidor corrige igual
            espera=200,
        )
        self.assertEqual(datos["aciertos"], 2)
        self.assertEqual(datos["intentos"], 2)

    def test_un_caso_sin_senales_de_riesgo_no_revienta(self):
        # Dividir por cero daria un 500, y un 0% castigaria al jugador por un caso
        # que no tenia nada que encontrar.
        caso = CasoDetective.objects.create(
            caso_id="caso_corr_02", titulo="Sin trampa", zona="ciberacoso",
            permiso_player_text="?", permiso_npc_nombre="Coipo", permiso_npc_response="ya",
        )
        MensajeDetective.objects.create(
            caso=caso, mensaje_id="caso_corr_02_m1", npc_sender="Coipo",
            texto="hola", es_senal_riesgo=False, orden=1,
        )
        datos = self.post(
            f"/api/casos-detective/{caso.caso_id}/progreso/",
            {"partida_id": self.partida.pk, "mensajes_marcados": []},
            self.token, 201,
        )
        self.assertEqual(datos["total_riesgo"], 0)
        self.assertEqual(datos["porcentaje"], 1.0)
