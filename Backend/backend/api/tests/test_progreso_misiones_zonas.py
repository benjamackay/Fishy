"""HDU-1 CA4/CA5, HDU-3 CA5 y HDU-4 CA5 — el progreso de misiones y zonas queda
guardado en la partida y no en el perfil del menor.

Lo que se cuida acá, además del 201, son las tres decisiones de diseño que si se
pierden no se notan hasta que ya hay datos reales encima:

  - el progreso cuelga de la Partida, así que dos partidas del mismo menor no se
    contaminan (HDU-15, "continuar mi última partida");
  - completar es un camino de ida: el orden en que Unity manda los POST no está
    garantizado y el registro del adulto no puede retroceder;
  - una misión con un `mision_id` que el banco no tiene se guarda igual, porque
    el progreso del niño no depende de que el contenido esté al día.
"""
from django.test import TestCase

from api.models import (
    AdultoResponsable,
    Mision,
    MisionProgreso,
    NivelRiesgo,
    Partida,
    UsuarioJugador,
    ZonaProgreso,
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


class ProgresoDeMisionesTests(BaseAPI):
    """HDU-1 CA4: el desafío queda registrado como disponible. CA5: como completado."""

    def setUp(self):
        self.adulto, self.token = self._adulto("misiones@test.local", "AdultoMisiones")
        self.partida = self._partida(self.adulto)
        self.ruta = f"/api/partidas/{self.partida.pk}/misiones/"
        Mision.objects.create(
            mision_id="MISION_SEC_MOCHILA_HUEMUL", nombre="La mochila de Huemul",
            tipo=Mision.Tipo.SECUNDARIA, zona="desconocidos",
        )

    def test_desbloquear_una_mision_la_deja_disponible(self):
        datos = self.post(
            self.ruta, {"mision_id": "MISION_SEC_MOCHILA_HUEMUL"}, self.token, 201
        )
        self.assertEqual(datos["estado"], "disponible")
        self.assertIsNone(datos["fecha_completada"])

        fila = MisionProgreso.objects.get(partida=self.partida)
        self.assertEqual(fila.mision_id, "MISION_SEC_MOCHILA_HUEMUL")
        self.assertIsNone(fila.fecha_completada)

    def test_completarla_le_pone_fecha_y_cambia_el_estado(self):
        self.post(self.ruta, {"mision_id": "MISION_SEC_MOCHILA_HUEMUL"}, self.token, 201)
        datos = self.post(
            self.ruta,
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "completada"},
            self.token, 200,
        )
        self.assertEqual(datos["estado"], "completada")
        self.assertIsNotNone(datos["fecha_completada"])

        fila = MisionProgreso.objects.get(partida=self.partida)
        self.assertIsNotNone(fila.fecha_completada)

    def test_repetir_el_post_no_duplica_la_fila_ni_mueve_la_fecha(self):
        self.post(
            self.ruta,
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "completada"},
            self.token, 201,
        )
        primera = MisionProgreso.objects.get(partida=self.partida).fecha_completada

        self.post(
            self.ruta,
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "completada"},
            self.token, 200,
        )
        self.assertEqual(MisionProgreso.objects.filter(partida=self.partida).count(), 1)
        self.assertEqual(
            MisionProgreso.objects.get(partida=self.partida).fecha_completada, primera
        )

    def test_una_mision_completada_no_vuelve_a_disponible(self):
        self.post(
            self.ruta,
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "completada"},
            self.token, 201,
        )
        datos = self.post(
            self.ruta,
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "disponible"},
            self.token, 200,
        )
        self.assertEqual(datos["estado"], "completada")

    def test_un_id_que_no_esta_en_el_banco_se_guarda_y_queda_marcado(self):
        # Hoy pasa de verdad: los DesafioData de Unity usan MISION_NPC_01, que el
        # banco no tiene. El progreso del niño no puede depender de eso.
        datos = self.post(self.ruta, {"mision_id": "MISION_NPC_01"}, self.token, 201)
        self.assertFalse(datos["en_catalogo"])
        self.assertTrue(MisionProgreso.objects.filter(mision_id="MISION_NPC_01").exists())

    def test_una_mision_del_banco_llega_con_su_nombre_y_zona(self):
        datos = self.post(
            self.ruta, {"mision_id": "MISION_SEC_MOCHILA_HUEMUL"}, self.token, 201
        )
        self.assertTrue(datos["en_catalogo"])
        self.assertEqual(datos["nombre"], "La mochila de Huemul")
        self.assertEqual(datos["zona"], "desconocidos")

    def test_sin_mision_id_no_guarda_nada(self):
        self.post(self.ruta, {"estado": "completada"}, self.token, 400)
        self.assertEqual(MisionProgreso.objects.count(), 0)

    def test_un_estado_que_no_existe_no_guarda_nada(self):
        self.post(
            self.ruta,
            {"mision_id": "MISION_SEC_MOCHILA_HUEMUL", "estado": "abandonada"},
            self.token, 400,
        )
        self.assertEqual(MisionProgreso.objects.count(), 0)

    def test_lo_guardado_vuelve_por_la_api_de_la_partida(self):
        self.post(self.ruta, {"mision_id": "MISION_SEC_MOCHILA_HUEMUL"}, self.token, 201)
        self.post(
            self.ruta, {"mision_id": "MISION_NPC_01", "estado": "completada"},
            self.token, 201,
        )
        lista = self.get(self.ruta, self.token)
        self.assertEqual(len(lista), 2)
        self.assertEqual(
            {m["mision_id"]: m["estado"] for m in lista},
            {"MISION_SEC_MOCHILA_HUEMUL": "disponible", "MISION_NPC_01": "completada"},
        )

    def test_otro_adulto_no_puede_escribir_en_la_partida_ajena(self):
        _, token_ajeno = self._adulto("otro@test.local", "AdultoAjeno")
        self.post(self.ruta, {"mision_id": "MISION_NPC_01"}, token_ajeno, 404)
        self.assertEqual(MisionProgreso.objects.count(), 0)


class ProgresoDeZonasTests(BaseAPI):
    """HDU-3 CA5 y HDU-4 CA5: la temática queda marcada como completada y la
    siguiente queda habilitada."""

    def setUp(self):
        self.adulto, self.token = self._adulto("zonas@test.local", "AdultoZonas")
        self.partida = self._partida(self.adulto)
        self.ruta = f"/api/partidas/{self.partida.pk}/zonas/"

    def test_abrir_una_zona_la_deja_desbloqueada_sin_completar(self):
        datos = self.post(self.ruta, {"zona": "ciberacoso"}, self.token, 201)
        self.assertTrue(datos["desbloqueada"])
        self.assertFalse(datos["completada"])
        self.assertIsNone(datos["fecha_completada"])

    def test_completar_la_zona_le_pone_fecha(self):
        self.post(self.ruta, {"zona": "ciberacoso"}, self.token, 201)
        datos = self.post(
            self.ruta, {"zona": "ciberacoso", "completada": True}, self.token, 200
        )
        self.assertTrue(datos["completada"])
        self.assertIsNotNone(datos["fecha_completada"])

    def test_completar_dos_veces_no_duplica_ni_mueve_la_fecha(self):
        self.post(self.ruta, {"zona": "ciberacoso", "completada": True}, self.token, 201)
        primera = ZonaProgreso.objects.get(partida=self.partida).fecha_completada

        self.post(self.ruta, {"zona": "ciberacoso", "completada": True}, self.token, 200)
        self.assertEqual(ZonaProgreso.objects.filter(partida=self.partida).count(), 1)
        self.assertEqual(
            ZonaProgreso.objects.get(partida=self.partida).fecha_completada, primera
        )

    def test_una_zona_completada_no_se_reabre(self):
        self.post(self.ruta, {"zona": "ciberacoso", "completada": True}, self.token, 201)
        datos = self.post(
            self.ruta, {"zona": "ciberacoso", "completada": False}, self.token, 200
        )
        self.assertTrue(datos["completada"])

    def test_las_tres_zonas_del_banco_conviven_sin_migrar_nada(self):
        for zona in ("desconocidos", "ciberacoso", "reto_viral"):
            self.post(self.ruta, {"zona": zona}, self.token, 201)
        lista = self.get(self.ruta, self.token)
        self.assertEqual(
            {z["zona"] for z in lista}, {"desconocidos", "ciberacoso", "reto_viral"}
        )

    def test_una_zona_nueva_no_necesita_estar_en_ninguna_lista(self):
        # Agregar una temática es contenido, no código: el endpoint no valida
        # contra una lista fija a propósito.
        datos = self.post(self.ruta, {"zona": "privacidad"}, self.token, 201)
        self.assertEqual(datos["zona"], "privacidad")

    def test_sin_zona_no_guarda_nada(self):
        self.post(self.ruta, {"completada": True}, self.token, 400)
        self.assertEqual(ZonaProgreso.objects.count(), 0)

    def test_dos_partidas_del_mismo_menor_no_comparten_zonas(self):
        # La razón por la que esto no vive en UsuarioJugador: la partida de mañana
        # en la feria tiene que empezar con el mapa cerrado.
        self.post(self.ruta, {"zona": "ciberacoso", "completada": True}, self.token, 201)

        jugador = self.partida.usuario_jugador
        segunda = Partida.objects.create(
            usuario_jugador=jugador, nivel_riesgo=self.partida.nivel_riesgo
        )
        lista = self.get(f"/api/partidas/{segunda.pk}/zonas/", self.token)
        self.assertEqual(lista, [])

    def test_otro_adulto_no_puede_escribir_en_la_partida_ajena(self):
        _, token_ajeno = self._adulto("otra@test.local", "AdultaAjena")
        self.post(self.ruta, {"zona": "ciberacoso"}, token_ajeno, 404)
        self.assertEqual(ZonaProgreso.objects.count(), 0)
