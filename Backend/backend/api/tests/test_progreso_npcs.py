"""HDU-3 CA5 / HDU-4 CA5 — una temática se puede completar en varias sesiones.

`BosqueDesconocidosManager` decide si la temática está lista preguntándole a cada
NPC si `Finished`, y ese `Finished` vivía solo en memoria del objeto de la escena.
Con 2 de 3 NPCs hechos, cerrar el juego los devolvía a los tres a cero: había que
hacer la temática entera de una sentada o la zona siguiente no se abría nunca.

Lo que se cuida acá:

  - `exito` importa tanto como haber terminado: decide si el NPC se retira del mapa
    y si cuenta como "a salvo" o "captura". Por eso no basta con deducir de los
    chats que la conversación ocurrió;
  - a diferencia de los objetos recogidos, aquí el POST repetido **sí actualiza**
    `exito`: un NPC con `allowReplay` puede repetirse y vale el último resultado;
  - cuelga de la partida, así que dos partidas del mismo menor no se contaminan.
"""
from django.test import TestCase

from api.models import AdultoResponsable, NivelRiesgo, NpcProgreso, Partida, UsuarioJugador


class BaseAPI(TestCase):
    def _adulto(self, email, nombre):
        AdultoResponsable.objects.create_user(
            email=email, nombre=nombre, password="clave-de-prueba-123"
        )
        r = self.client.post(
            "/api/auth/login/",
            {"nombre": nombre, "password": "clave-de-prueba-123"},
            content_type="application/json",
        )
        self.assertEqual(r.status_code, 200, r.content)
        return AdultoResponsable.objects.get(nombre=nombre), r.json()["token"]

    def _partida(self, adulto, nombre_menor="Otto"):
        jugador = UsuarioJugador.objects.create(adulto=adulto, nombre=nombre_menor, edad=9)
        nivel, _ = NivelRiesgo.objects.get_or_create(nombre="bajo", defaults={"descripcion": "prueba"})
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


class ProgresoNpcsTests(BaseAPI):
    def setUp(self):
        self.adulto, self.token = self._adulto("npcs@test.local", "AdultoNpcs")
        self.partida = self._partida(self.adulto)
        self.ruta = f"/api/partidas/{self.partida.pk}/progreso-npcs/"

    # ── Lo básico ────────────────────────────────────────────────────────────

    def test_una_partida_nueva_no_tiene_npcs_hechos(self):
        self.assertEqual(self.get(self.ruta, self.token), [])

    def test_terminar_con_un_npc_lo_marca(self):
        datos = self.post(self.ruta, {"npc_id": "BOSQUE_01", "exito": True}, self.token, 201)
        self.assertEqual(datos["npc_id"], "BOSQUE_01")
        self.assertTrue(datos["exito"])

    def test_la_tematica_a_medias_sobrevive(self):
        """El caso que motivó todo: 2 de 3 NPCs y el niño/a cierra el juego."""
        self.post(self.ruta, {"npc_id": "BOSQUE_01", "exito": True}, self.token, 201)
        self.post(self.ruta, {"npc_id": "BOSQUE_02", "exito": False}, self.token, 201)

        hechos = self.get(self.ruta, self.token)
        self.assertEqual(sorted(n["npc_id"] for n in hechos), ["BOSQUE_01", "BOSQUE_02"])

    def test_el_exito_de_cada_uno_se_conserva(self):
        """Sin `exito` no se puede reconstruir el mapa: el NPC exitoso se retira."""
        self.post(self.ruta, {"npc_id": "BOSQUE_01", "exito": True}, self.token, 201)
        self.post(self.ruta, {"npc_id": "BOSQUE_02", "exito": False}, self.token, 201)

        por_id = {n["npc_id"]: n["exito"] for n in self.get(self.ruta, self.token)}
        self.assertTrue(por_id["BOSQUE_01"])
        self.assertFalse(por_id["BOSQUE_02"])

    def test_exito_por_omision_es_false(self):
        datos = self.post(self.ruta, {"npc_id": "BOSQUE_03"}, self.token, 201)
        self.assertFalse(datos["exito"])

    # ── Repetir SÍ actualiza, al revés que los objetos recogidos ─────────────

    def test_repetir_no_duplica_pero_actualiza_el_exito(self):
        """`allowReplay` permite rehacer la interacción, y vale el último resultado.
        Es la diferencia con ObjetoRecogido, donde no hay nada que actualizar."""
        primero = self.post(self.ruta, {"npc_id": "BOSQUE_01", "exito": False}, self.token, 201)
        segundo = self.post(self.ruta, {"npc_id": "BOSQUE_01", "exito": True}, self.token, 200)

        self.assertEqual(primero["id"], segundo["id"])
        self.assertTrue(segundo["exito"])
        self.assertEqual(NpcProgreso.objects.filter(partida=self.partida).count(), 1)

    # ── Aislamiento ──────────────────────────────────────────────────────────

    def test_dos_partidas_del_mismo_menor_no_se_mezclan(self):
        otra = Partida.objects.create(
            usuario_jugador=self.partida.usuario_jugador,
            nivel_riesgo=self.partida.nivel_riesgo,
        )
        self.post(self.ruta, {"npc_id": "BOSQUE_01", "exito": True}, self.token, 201)

        self.assertEqual(self.get(f"/api/partidas/{otra.pk}/progreso-npcs/", self.token), [])

    def test_otro_adulto_no_entra(self):
        _, otro_token = self._adulto("ajeno-n@test.local", "AdultoAjenoN")
        self.get(self.ruta, otro_token, espera=404)
        self.post(self.ruta, {"npc_id": "X"}, otro_token, 404)

    def test_sin_token_no_se_entra(self):
        self.assertEqual(self.client.get(self.ruta).status_code, 401)

    def test_borrar_la_partida_se_lleva_el_progreso(self):
        self.post(self.ruta, {"npc_id": "BOSQUE_01", "exito": True}, self.token, 201)
        self.partida.delete()
        self.assertEqual(NpcProgreso.objects.count(), 0)

    # ── Entradas malas ───────────────────────────────────────────────────────

    def test_sin_npc_id_es_400(self):
        self.post(self.ruta, {"exito": True}, self.token, 400)

    def test_npc_id_demasiado_largo_es_400(self):
        """Postgres corta en varchar(80) y responderia 500; SQLite lo dejaria pasar."""
        self.post(self.ruta, {"npc_id": "X" * 100}, self.token, 400)

    def test_exito_que_no_es_booleano_es_400(self):
        self.post(self.ruta, {"npc_id": "BOSQUE_01", "exito": "quizas"}, self.token, 400)
