"""HDU-15 — Otto vuelve donde estaba, no al spawnPoint de la escena.

`PersonajeJugador` existía vacío desde el principio, con un comentario que decía
"agrega aquí los atributos". Esto es eso.

Lo que se cuida acá:

  - la fila se crea sola: ninguna vista la creaba, así que las partidas que ya
    existen no tienen una y pedir la posición no puede responder 404;
  - "sin posición" y "en el (0,0)" son cosas distintas — el origen es un lugar del
    mapa, así que el null tiene que sobrevivir el viaje de ida y vuelta;
  - es PATCH: mandar solo la escena no puede borrar la posición;
  - `zona_actual` es la región del mapa (`zona_1`, el `zoneId` de Unity), y no la
    temática del banco que guarda `ZonaProgreso.zona` (`desconocidos`). Se llaman
    igual y no son lo mismo.
"""
from django.db import connection
from django.test import TestCase
from django.test.utils import CaptureQueriesContext

from api.models import AdultoResponsable, NivelRiesgo, Partida, PersonajeJugador, UsuarioJugador


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

    def patch(self, ruta, body, token, espera=200):
        r = self.client.patch(
            ruta, body, content_type="application/json", HTTP_AUTHORIZATION=f"Token {token}"
        )
        self.assertEqual(r.status_code, espera, r.content)
        return r.json() if r.content else None

    def get(self, ruta, token, espera=200):
        r = self.client.get(ruta, HTTP_AUTHORIZATION=f"Token {token}")
        self.assertEqual(r.status_code, espera, r.content)
        return r.json() if r.content else None


class PersonajeTests(BaseAPI):
    def setUp(self):
        self.adulto, self.token = self._adulto("personaje@test.local", "AdultoPersonaje")
        self.partida = self._partida(self.adulto)
        self.ruta = f"/api/partidas/{self.partida.pk}/personaje/"

    # ── La fila se crea sola ─────────────────────────────────────────────────

    def test_una_partida_sin_personaje_no_da_404(self):
        """Ninguna vista creaba PersonajeJugador, así que las 9 partidas que ya
        existían en Supabase no tienen fila. Pedirla tiene que crearla."""
        self.assertFalse(PersonajeJugador.objects.filter(partida=self.partida).exists())

        datos = self.get(self.ruta, self.token)

        self.assertTrue(PersonajeJugador.objects.filter(partida=self.partida).exists())
        self.assertFalse(datos["tiene_posicion"])
        self.assertIsNone(datos["pos_x"])

    def test_pedirla_dos_veces_no_crea_dos(self):
        self.get(self.ruta, self.token)
        self.get(self.ruta, self.token)
        self.assertEqual(PersonajeJugador.objects.filter(partida=self.partida).count(), 1)

    # ── Guardar y recuperar ──────────────────────────────────────────────────

    def test_zona_actual_de_dev_se_crea_y_se_conserva_al_actualizar_posicion(self):
        self.assertEqual(self.get(self.ruta, self.token)["zona_actual"], "")
        self.patch(self.ruta, {"zona_actual": "zona_2"}, self.token)
        self.patch(self.ruta, {"pos_x": 12.5, "pos_y": -3.25}, self.token)
        self.assertEqual(self.get(self.ruta, self.token)["zona_actual"], "zona_2")
        self.assertEqual(PersonajeJugador.objects.get(partida=self.partida).zona_actual, "zona_2")

    def test_guardar_y_recuperar_la_posicion(self):
        self.patch(self.ruta, {"escena": "SampleScene", "pos_x": 12.5, "pos_y": -3.25}, self.token)

        datos = self.get(self.ruta, self.token)
        self.assertEqual(datos["escena"], "SampleScene")
        self.assertAlmostEqual(datos["pos_x"], 12.5)
        self.assertAlmostEqual(datos["pos_y"], -3.25)
        self.assertTrue(datos["tiene_posicion"])

    def test_el_origen_no_es_lo_mismo_que_sin_posicion(self):
        """(0,0) es un lugar del mapa. Si el cliente tratara el 0 como "no hay",
        a un niño que guarda ahí lo mandaría de vuelta al spawnPoint."""
        self.patch(self.ruta, {"pos_x": 0.0, "pos_y": 0.0}, self.token)

        datos = self.get(self.ruta, self.token)
        self.assertTrue(datos["tiene_posicion"])
        self.assertEqual(datos["pos_x"], 0.0)

    def test_es_patch_mandar_solo_la_escena_no_borra_la_posicion(self):
        self.patch(self.ruta, {"escena": "SampleScene", "pos_x": 4.0, "pos_y": 5.0}, self.token)
        self.patch(self.ruta, {"escena": "OtraEscena"}, self.token)

        datos = self.get(self.ruta, self.token)
        self.assertEqual(datos["escena"], "OtraEscena")
        self.assertAlmostEqual(datos["pos_x"], 4.0)

    def test_se_puede_sobrescribir(self):
        self.patch(self.ruta, {"pos_x": 1.0, "pos_y": 1.0}, self.token)
        self.patch(self.ruta, {"pos_x": 99.0, "pos_y": -99.0}, self.token)

        datos = self.get(self.ruta, self.token)
        self.assertAlmostEqual(datos["pos_x"], 99.0)
        self.assertAlmostEqual(datos["pos_y"], -99.0)

    # ── Zona del mapa ────────────────────────────────────────────────────────

    def test_sin_guardar_la_zona_viene_vacia_y_no_null(self):
        """La cadena vacía es "nunca se guardó". Acá no hace falta el null que sí
        necesitan pos_x/pos_y, porque no hay una zona que se llame ""."""
        datos = self.get(self.ruta, self.token)
        self.assertEqual(datos["zona_actual"], "")

    def test_guardar_y_recuperar_la_zona(self):
        self.patch(self.ruta, {"zona_actual": "zona_2"}, self.token)

        datos = self.get(self.ruta, self.token)
        self.assertEqual(datos["zona_actual"], "zona_2")

    def test_mandar_solo_la_zona_no_borra_la_posicion(self):
        """Unity manda zona y posición juntas, pero el PATCH tiene que aguantar
        que venga una sola: es una actualización, no una orden de dejar el resto
        en blanco."""
        self.patch(self.ruta, {"escena": "SampleScene", "pos_x": 12.5, "pos_y": -3.25}, self.token)
        self.patch(self.ruta, {"zona_actual": "zona_2"}, self.token)

        datos = self.get(self.ruta, self.token)
        self.assertEqual(datos["zona_actual"], "zona_2")
        self.assertAlmostEqual(datos["pos_x"], 12.5)
        self.assertAlmostEqual(datos["pos_y"], -3.25)

    def test_una_zona_mas_larga_que_la_columna_da_400_y_no_500(self):
        """La suite corre en SQLite, que no valida largos de varchar: sin este
        corte en el serializer, un id largo pasaría acá y reventaría en Supabase."""
        self.patch(self.ruta, {"zona_actual": "z" * 51}, self.token, espera=400)

    # ── Aislamiento ──────────────────────────────────────────────────────────

    def test_dos_partidas_del_mismo_menor_no_se_mezclan(self):
        otra = Partida.objects.create(
            usuario_jugador=self.partida.usuario_jugador,
            nivel_riesgo=self.partida.nivel_riesgo,
        )
        self.patch(self.ruta, {"pos_x": 7.0, "pos_y": 7.0}, self.token)

        datos = self.get(f"/api/partidas/{otra.pk}/personaje/", self.token)
        self.assertFalse(datos["tiene_posicion"])

    def test_otro_adulto_no_ve_donde_esta_el_nino(self):
        _, otro_token = self._adulto("ajeno-p@test.local", "AdultoAjenoP")
        self.get(self.ruta, otro_token, espera=404)
        self.patch(self.ruta, {"pos_x": 1.0}, otro_token, espera=404)

    def test_sin_token_no_se_entra(self):
        self.assertEqual(self.client.get(self.ruta).status_code, 401)

    def test_borrar_la_partida_se_lleva_el_personaje(self):
        self.get(self.ruta, self.token)
        self.partida.delete()
        self.assertEqual(PersonajeJugador.objects.count(), 0)

    # ── Entradas malas ───────────────────────────────────────────────────────

    def test_una_posicion_que_no_es_numero_es_400(self):
        self.patch(self.ruta, {"pos_x": "por alla"}, self.token, espera=400)

    def test_una_escena_demasiado_larga_es_400(self):
        """Postgres corta en varchar(80). Como pasa por serializer, DRF valida el
        largo solo — a diferencia del inventario, que escribe con el ORM directo."""
        self.patch(self.ruta, {"escena": "X" * 100}, self.token, espera=400)


class ZonaActualEnLaListaDePartidasTests(BaseAPI):
    """`GET /api/jugadores/<id>/partidas/` trae la zona de cada partida.

    Antes había que pedir `/partidas/<id>/personaje/` una por una para pintar la
    lista de "Ingresar", que es justo lo que este campo evita.
    """

    def setUp(self):
        self.adulto, self.token = self._adulto("lista-zona@test.local", "AdultoListaZona")
        self.partida = self._partida(self.adulto)
        self.jugador = self.partida.usuario_jugador
        self.ruta = f"/api/jugadores/{self.jugador.pk}/partidas/"

    def test_una_partida_sin_fila_de_personaje_trae_la_zona_vacia(self):
        """`POST /partidas/` no crea PersonajeJugador, así que una partida recién
        creada no tiene fila. El campo tiene que salir igual, y vacío — no null:
        es lo mismo que responde `/personaje/` cuando la zona no se ha guardado."""
        self.assertFalse(PersonajeJugador.objects.filter(partida=self.partida).exists())

        datos = self.get(self.ruta, self.token)

        self.assertEqual(datos[0]["zona_actual"], "")
        # Leerla no debe crear la fila: de eso se encarga /personaje/.
        self.assertFalse(PersonajeJugador.objects.filter(partida=self.partida).exists())

    def test_la_zona_guardada_viaja_en_la_lista(self):
        self.patch(
            f"/api/partidas/{self.partida.pk}/personaje/", {"zona_actual": "zona_2"}, self.token
        )
        self.assertEqual(self.get(self.ruta, self.token)[0]["zona_actual"], "zona_2")

    def test_cada_partida_trae_la_suya(self):
        otra = Partida.objects.create(
            usuario_jugador=self.jugador, nivel_riesgo=self.partida.nivel_riesgo
        )
        PersonajeJugador.objects.create(partida=self.partida, zona_actual="zona_1")
        PersonajeJugador.objects.create(partida=otra, zona_actual="zona_3")

        por_id = {p["id"]: p["zona_actual"] for p in self.get(self.ruta, self.token)}

        self.assertEqual(por_id[self.partida.pk], "zona_1")
        self.assertEqual(por_id[otra.pk], "zona_3")

    def test_no_dispara_una_consulta_por_partida(self):
        """Sin el select_related("personaje") de la vista, el campo costaría una
        consulta por partida. Se compara 1 partida contra 4: el total no se mueve."""
        with CaptureQueriesContext(connection) as una:
            self.get(self.ruta, self.token)

        for _ in range(3):
            PersonajeJugador.objects.create(
                partida=Partida.objects.create(
                    usuario_jugador=self.jugador, nivel_riesgo=self.partida.nivel_riesgo
                ),
                zona_actual="zona_2",
            )

        with CaptureQueriesContext(connection) as cuatro:
            self.assertEqual(len(self.get(self.ruta, self.token)), 4)

        self.assertEqual(len(cuatro), len(una), "la lista de partidas tiene un N+1")

    def test_el_patch_de_la_partida_sigue_trayendo_el_campo(self):
        """`PartidaSerializer` lo comparten la lista, el POST y este PATCH, así que
        el campo nuevo tiene que venir en los tres y con el mismo vacío, sin fila
        de personaje. Con `source=` + `default=""` acá salía null."""
        datos = self.patch(f"/api/partidas/{self.partida.pk}/", {"progreso": 5}, self.token)

        self.assertIn("zona_actual", datos)
        self.assertEqual(datos["zona_actual"], "")
