"""HDU-1 CA2 — los objetos que el niño/a ya recogió no vuelven a aparecer en el suelo.

El CA dice: *"cuando vuelve a acercarse a su posición original, el objeto ya no se
muestra disponible para interactuar nuevamente"*. `WorldItem.recogido` lo cumplía
**dentro** de una sesión, pero es un bool privado del GameObject: al recargar la
escena los objetos volvían al mapa.

Lo que se cuida acá:

  - esto **solo crece**, al revés que el inventario, así que el POST por objeto es
    idempotente y la fila no se borra nunca;
  - `objeto_id` es el id del objeto de la escena, **no** el `itemId` del ítem: dos
    objetos del mapa pueden dar el mismo ítem, y un consumible sale de la mochila
    sin que el del suelo deba reaparecer;
  - cuelga de la partida, así que dos partidas del mismo menor no se contaminan.
"""
from django.test import TestCase

from api.models import AdultoResponsable, NivelRiesgo, ObjetoRecogido, Partida, UsuarioJugador


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


class ObjetosRecogidosTests(BaseAPI):
    def setUp(self):
        self.adulto, self.token = self._adulto("objetos@test.local", "AdultoObjetos")
        self.partida = self._partida(self.adulto)
        self.ruta = f"/api/partidas/{self.partida.pk}/objetos-recogidos/"

    # ── Lo básico ────────────────────────────────────────────────────────────

    def test_un_mapa_nuevo_no_tiene_nada_recogido(self):
        self.assertEqual(self.get(self.ruta, self.token), [])

    def test_recoger_marca_el_objeto(self):
        datos = self.post(self.ruta, {"objeto_id": "SAMPLESCENE_CONCHA_01"}, self.token, 201)
        self.assertEqual(datos["objeto_id"], "SAMPLESCENE_CONCHA_01")

        lista = self.get(self.ruta, self.token)
        self.assertEqual([o["objeto_id"] for o in lista], ["SAMPLESCENE_CONCHA_01"])

    def test_varios_objetos(self):
        for oid in ("A_01", "A_02", "B_01"):
            self.post(self.ruta, {"objeto_id": oid}, self.token, 201)

        lista = self.get(self.ruta, self.token)
        self.assertEqual(sorted(o["objeto_id"] for o in lista), ["A_01", "A_02", "B_01"])

    # ── Solo crece: es la diferencia con el inventario ───────────────────────

    def test_repetir_el_post_es_idempotente(self):
        """Recoger es un camino de ida. Repetir no duplica la fila ni mueve la fecha,
        y devuelve 200 en vez de 201 — mismo criterio que misiones."""
        primero = self.post(self.ruta, {"objeto_id": "SAMPLESCENE_ROCA_01"}, self.token, 201)
        segundo = self.post(self.ruta, {"objeto_id": "SAMPLESCENE_ROCA_01"}, self.token, 200)

        self.assertEqual(primero["id"], segundo["id"])
        self.assertEqual(primero["fecha"], segundo["fecha"])
        self.assertEqual(ObjetoRecogido.objects.filter(partida=self.partida).count(), 1)

    def test_un_objeto_no_se_puede_des_recoger(self):
        """No hay endpoint para borrar, y es a propósito: si existiera, una llamada
        perdida o repetida podría devolver un objeto al suelo que el niño/a ya tiene."""
        self.post(self.ruta, {"objeto_id": "SAMPLESCENE_SURF_01"}, self.token, 201)

        r = self.client.delete(self.ruta, HTTP_AUTHORIZATION=f"Token {self.token}")
        self.assertEqual(r.status_code, 405, r.content)
        self.assertEqual(len(self.get(self.ruta, self.token)), 1)

    # ── Aislamiento ──────────────────────────────────────────────────────────

    def test_dos_partidas_del_mismo_menor_no_se_mezclan(self):
        otra = Partida.objects.create(
            usuario_jugador=self.partida.usuario_jugador,
            nivel_riesgo=self.partida.nivel_riesgo,
        )
        self.post(self.ruta, {"objeto_id": "SAMPLESCENE_CONCHA_01"}, self.token, 201)

        self.assertEqual(self.get(f"/api/partidas/{otra.pk}/objetos-recogidos/", self.token), [])

    def test_el_mismo_objeto_en_dos_partidas_son_dos_filas(self):
        """La restricción de unicidad es (partida, objeto_id), no objeto_id solo: que
        un niño/a haya recogido la concha no puede quitársela a otro."""
        otra = Partida.objects.create(
            usuario_jugador=self.partida.usuario_jugador,
            nivel_riesgo=self.partida.nivel_riesgo,
        )
        self.post(self.ruta, {"objeto_id": "SAMPLESCENE_CONCHA_01"}, self.token, 201)
        self.post(f"/api/partidas/{otra.pk}/objetos-recogidos/",
                  {"objeto_id": "SAMPLESCENE_CONCHA_01"}, self.token, 201)

        self.assertEqual(ObjetoRecogido.objects.filter(objeto_id="SAMPLESCENE_CONCHA_01").count(), 2)

    def test_otro_adulto_no_entra(self):
        _, otro_token = self._adulto("ajeno-o@test.local", "AdultoAjenoO")
        self.get(self.ruta, otro_token, espera=404)
        self.post(self.ruta, {"objeto_id": "X_01"}, otro_token, 404)

    def test_sin_token_no_se_entra(self):
        self.assertEqual(self.client.get(self.ruta).status_code, 401)

    def test_borrar_la_partida_se_lleva_los_objetos(self):
        self.post(self.ruta, {"objeto_id": "SAMPLESCENE_CONCHA_01"}, self.token, 201)
        self.partida.delete()
        self.assertEqual(ObjetoRecogido.objects.count(), 0)

    # ── Entradas malas ───────────────────────────────────────────────────────

    def test_sin_objeto_id_es_400(self):
        self.post(self.ruta, {}, self.token, 400)

    def test_objeto_id_vacio_es_400(self):
        self.post(self.ruta, {"objeto_id": "   "}, self.token, 400)

    def test_un_objeto_id_demasiado_largo_es_400(self):
        """Postgres corta en varchar(80) y responderia 500; SQLite lo dejaria pasar.
        Mismo caso que el inventario, y por eso se valida en la vista."""
        self.post(self.ruta, {"objeto_id": "X" * 100}, self.token, 400)

    # ── Sin catálogo ─────────────────────────────────────────────────────────

    def test_un_objeto_desconocido_se_guarda_igual(self):
        """El catálogo de objetos de la escena vive en Unity. El progreso del niño/a
        no puede depender de que las dos partes estén al día."""
        self.post(self.ruta, {"objeto_id": "OBJETO_QUE_NADIE_DEFINIO"}, self.token, 201)
        self.assertEqual(len(self.get(self.ruta, self.token)), 1)
