"""HDU-15 — la mochila de Otto sobrevive a cerrar el juego.

Hasta ahora el inventario vivía solo en memoria (`InventoryManager` es un
singleton con un `List<Item>` y ni siquiera PlayerPrefs), así que cerrar el juego
lo borraba entero.

Lo que se cuida acá, más allá del 200, son las decisiones de diseño que si se
pierden no se notan hasta que ya hay datos reales encima:

  - el inventario cuelga de la Partida, no del perfil del menor, así que dos
    partidas del mismo niño/a no se contaminan;
  - el PUT **reemplaza**: lo que no viene, no está. Es lo que permite que un
    objeto consumido desaparezca, cosa que un POST por fila no sabe expresar;
  - un PUT inválido no deja la mochila a medias: o se escribe entera o no se
    escribe nada;
  - no hay catálogo de items en el backend, así que cualquier `item_id` se
    acepta. El progreso del niño/a no depende de que Unity y la base estén al día.
"""
from django.test import TestCase

from api.models import (
    AdultoResponsable,
    ItemInventario,
    NivelRiesgo,
    Partida,
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

    def put(self, ruta, body, token, espera=200):
        r = self.client.put(
            ruta, body, content_type="application/json", HTTP_AUTHORIZATION=f"Token {token}"
        )
        self.assertEqual(r.status_code, espera, r.content)
        return r.json() if r.content else None

    def get(self, ruta, token, espera=200):
        r = self.client.get(ruta, HTTP_AUTHORIZATION=f"Token {token}")
        self.assertEqual(r.status_code, espera, r.content)
        return r.json() if r.content else None


class InventarioTests(BaseAPI):
    def setUp(self):
        self.adulto, self.token = self._adulto("inventario@test.local", "AdultoInventario")
        self.partida = self._partida(self.adulto)
        self.ruta = f"/api/partidas/{self.partida.pk}/inventario/"

    # ── Lo básico ────────────────────────────────────────────────────────────

    def test_una_mochila_nueva_esta_vacia(self):
        self.assertEqual(self.get(self.ruta, self.token), [])

    def test_guardar_y_recuperar(self):
        self.put(
            self.ruta,
            {"items": [
                {"item_id": "ITEM_BRUJULA", "cantidad": 1},
                {"item_id": "ITEM_FLOR_01", "cantidad": 3},
            ]},
            self.token,
        )

        datos = self.get(self.ruta, self.token)
        self.assertEqual(
            {d["item_id"]: d["cantidad"] for d in datos},
            {"ITEM_BRUJULA": 1, "ITEM_FLOR_01": 3},
        )

    def test_cantidad_por_omision_es_uno(self):
        self.put(self.ruta, {"items": [{"item_id": "ITEM_ROCA"}]}, self.token)
        self.assertEqual(self.get(self.ruta, self.token)[0]["cantidad"], 1)

    # ── El PUT reemplaza: es el punto del diseño ─────────────────────────────

    def test_lo_que_no_viene_se_borra(self):
        """Un objeto consumido tiene que desaparecer. Es la razón de que sea PUT
        y no POST por fila: `ItemType.Consumable` saca objetos de la mochila."""
        self.put(
            self.ruta,
            {"items": [
                {"item_id": "ITEM_BRUJULA", "cantidad": 1},
                {"item_id": "ITEM_FLOR_01", "cantidad": 2},
            ]},
            self.token,
        )
        self.put(self.ruta, {"items": [{"item_id": "ITEM_BRUJULA", "cantidad": 1}]}, self.token)

        datos = self.get(self.ruta, self.token)
        self.assertEqual([d["item_id"] for d in datos], ["ITEM_BRUJULA"])

    def test_una_lista_vacia_vacia_la_mochila(self):
        self.put(self.ruta, {"items": [{"item_id": "ITEM_SURF", "cantidad": 1}]}, self.token)
        self.put(self.ruta, {"items": []}, self.token)
        self.assertEqual(self.get(self.ruta, self.token), [])

    def test_repetir_el_mismo_put_no_cambia_nada(self):
        cuerpo = {"items": [{"item_id": "ITEM_SILBATO", "cantidad": 2}]}
        primero = self.put(self.ruta, cuerpo, self.token)
        segundo = self.put(self.ruta, cuerpo, self.token)

        self.assertEqual(
            [(d["item_id"], d["cantidad"]) for d in primero],
            [(d["item_id"], d["cantidad"]) for d in segundo],
        )
        self.assertEqual(ItemInventario.objects.filter(partida=self.partida).count(), 1)

    def test_cantidad_cero_es_no_tenerlo(self):
        self.put(self.ruta, {"items": [{"item_id": "ITEM_MOCHILA", "cantidad": 0}]}, self.token)
        self.assertEqual(self.get(self.ruta, self.token), [])

    def test_el_mismo_item_repetido_en_un_put_se_suma(self):
        """Que Unity mande dos filas del mismo objeto sería un bug suyo, pero
        perder unidades en silencio es peor que quedarse con las dos."""
        self.put(
            self.ruta,
            {"items": [
                {"item_id": "ITEM_FLOR_02", "cantidad": 2},
                {"item_id": "ITEM_FLOR_02", "cantidad": 3},
            ]},
            self.token,
        )
        self.assertEqual(self.get(self.ruta, self.token)[0]["cantidad"], 5)

    # ── Aislamiento ──────────────────────────────────────────────────────────

    def test_dos_partidas_del_mismo_menor_no_se_mezclan(self):
        """HDU-15: un mismo menor puede tener varias partidas."""
        otra = Partida.objects.create(
            usuario_jugador=self.partida.usuario_jugador,
            nivel_riesgo=self.partida.nivel_riesgo,
        )
        self.put(self.ruta, {"items": [{"item_id": "ITEM_BRUJULA", "cantidad": 1}]}, self.token)

        self.assertEqual(self.get(f"/api/partidas/{otra.pk}/inventario/", self.token), [])

    def test_no_se_puede_ver_la_mochila_de_otro_adulto(self):
        otro_adulto, otro_token = self._adulto("ajeno@test.local", "AdultoAjeno")
        self.get(self.ruta, otro_token, espera=404)
        self.put(self.ruta, {"items": []}, otro_token, espera=404)

    def test_sin_token_no_se_entra(self):
        r = self.client.get(self.ruta)
        self.assertEqual(r.status_code, 401, r.content)

    # ── Entradas malas ───────────────────────────────────────────────────────

    def test_sin_items_es_400(self):
        self.put(self.ruta, {}, self.token, espera=400)

    def test_items_que_no_es_lista_es_400(self):
        self.put(self.ruta, {"items": "ITEM_ROCA"}, self.token, espera=400)

    def test_un_item_sin_id_es_400(self):
        self.put(self.ruta, {"items": [{"cantidad": 2}]}, self.token, espera=400)

    def test_una_cantidad_que_no_es_numero_es_400(self):
        self.put(
            self.ruta,
            {"items": [{"item_id": "ITEM_ROCA", "cantidad": "muchas"}]},
            self.token,
            espera=400,
        )

    def test_un_put_invalido_no_deja_la_mochila_a_medias(self):
        """O se escribe entera o no se escribe nada: un estado intermedio sería
        una mochila que el niño/a nunca tuvo."""
        self.put(self.ruta, {"items": [{"item_id": "ITEM_BRUJULA", "cantidad": 1}]}, self.token)

        self.put(
            self.ruta,
            {"items": [
                {"item_id": "ITEM_FLOR_03", "cantidad": 1},
                {"cantidad": 9},                     # sin item_id: revienta el PUT entero
            ]},
            self.token,
            espera=400,
        )

        datos = self.get(self.ruta, self.token)
        self.assertEqual([d["item_id"] for d in datos], ["ITEM_BRUJULA"])

    # ── Sin catálogo ─────────────────────────────────────────────────────────

    def test_un_item_desconocido_se_guarda_igual(self):
        """No hay catálogo de items en el backend y no debería haberlo: los
        objetos se crean en Unity. El progreso del niño/a no puede depender de
        que las dos partes estén al día."""
        self.put(
            self.ruta,
            {"items": [{"item_id": "ITEM_QUE_NADIE_DEFINIO", "cantidad": 1}]},
            self.token,
        )
        self.assertEqual(self.get(self.ruta, self.token)[0]["item_id"], "ITEM_QUE_NADIE_DEFINIO")

    def test_borrar_la_partida_se_lleva_su_inventario(self):
        self.put(self.ruta, {"items": [{"item_id": "ITEM_ROCA", "cantidad": 1}]}, self.token)
        self.partida.delete()
        self.assertEqual(ItemInventario.objects.count(), 0)
