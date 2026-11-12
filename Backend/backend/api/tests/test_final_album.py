"""El final de la aventura llega al álbum del niño (HDU-09).

El hueco que cierra esto: `SistemaDeFinales` mostraba el Final A, B o C y le
entregaba la medalla al inventario de Unity, pero **nadie se enteraba en el
servidor**. Las filas de `RecompensaAlbum` con `final_id` existían desde que
`cargar_banco` las carga, y no había forma de que un niño las obtuviera: ninguna
vista creaba `RecompensaObtenida`.

Qué final corresponde lo decide el juego, no el servidor: los umbrales
(`porcentaje_minimo`) son contenido del banco y `cargar_banco` carga de
`finales_narrativos` solo las recompensas. El servidor valida que el id sea uno
de los que trajo el banco, y nada más.

Se usa el banco de verdad a propósito, como `test_cargar_banco`: lo que importa
es que las medallas reales lleguen al álbum real.
"""
from django.core.management import call_command
from io import StringIO

from api.models import RecompensaAlbum, RecompensaObtenida

from .test_guardado_decisiones import BaseAPI


def cargar_banco():
    call_command("cargar_banco", stdout=StringIO())


class RegistrarFinalTests(BaseAPI):

    def setUp(self):
        cargar_banco()
        self.adulto, self.token = self._adulto("album@test.local", "AdultoAlbum")
        self.partida = self._partida(self.adulto, nombre_menor="Perfil 1")
        self.ruta = f"/api/partidas/{self.partida.pk}/final/"

    def test_entrega_la_medalla_del_final_al_album(self):
        d = self.post(self.ruta, {"final_id": "FINAL_A"}, self.token, 200)

        self.assertEqual(d["final_id"], "FINAL_A")
        self.assertEqual(d["recompensas_nuevas"], ["ALB_FINAL_A"])
        self.assertEqual(d["recompensas_ya_obtenidas"], [])
        self.assertEqual(d["album_total"], 1)

        obtenida = RecompensaObtenida.objects.get(partida=self.partida)
        self.assertEqual(obtenida.recompensa.final_id, "FINAL_A")
        self.assertEqual(
            obtenida.recompensa.nombre, "Medalla Dorada de Gran Detective Digital"
        )

    def test_repetir_no_duplica_la_medalla(self):
        """Rejugar la Misión 6, o un reintento de la cola de guardado, no puede
        darle dos veces la misma medalla al mismo niño."""
        self.post(self.ruta, {"final_id": "FINAL_B"}, self.token, 200)
        d = self.post(self.ruta, {"final_id": "FINAL_B"}, self.token, 200)

        self.assertEqual(d["recompensas_nuevas"], [])
        self.assertEqual(d["recompensas_ya_obtenidas"], ["ALB_FINAL_B"])
        self.assertEqual(RecompensaObtenida.objects.filter(partida=self.partida).count(), 1)

    def test_un_final_distinto_si_suma_su_propia_medalla(self):
        """Misma regla que `SistemaDeFinales.EntregarRecompensa` en Unity: el
        álbum no le quita a nadie lo que ya se ganó. Si el niño vuelve a jugar y
        le va mejor, se lleva las dos."""
        self.post(self.ruta, {"final_id": "FINAL_C"}, self.token, 200)
        d = self.post(self.ruta, {"final_id": "FINAL_A"}, self.token, 200)

        self.assertEqual(d["recompensas_nuevas"], ["ALB_FINAL_A"])
        self.assertEqual(d["album_total"], 2)
        self.assertEqual(
            set(RecompensaObtenida.objects
                .filter(partida=self.partida)
                .values_list("recompensa__final_id", flat=True)),
            {"FINAL_A", "FINAL_C"},
        )

    def test_rechaza_un_final_que_el_banco_no_tiene(self):
        d = self.post(self.ruta, {"final_id": "FINAL_Z"}, self.token, 400)
        self.assertIn("FINAL_Z", d["detail"])
        self.assertFalse(RecompensaObtenida.objects.exists())

    def test_rechaza_un_final_vacio(self):
        self.post(self.ruta, {}, self.token, 400)
        self.assertFalse(RecompensaObtenida.objects.exists())

    def test_no_se_puede_escribir_en_el_album_de_otro_nino(self):
        """El álbum es del niño. Un adulto no puede tocar la partida de otro."""
        otro, token_otro = self._adulto("ajeno@test.local", "AdultoAjeno")
        self.post(self.ruta, {"final_id": "FINAL_A"}, token_otro, 404)
        self.assertFalse(RecompensaObtenida.objects.exists())


class LeerAlbumTests(BaseAPI):

    def setUp(self):
        cargar_banco()
        self.adulto, self.token = self._adulto("leeralbum@test.local", "AdultoLeer")
        self.partida = self._partida(self.adulto, nombre_menor="Perfil 1")
        self.ruta = f"/api/partidas/{self.partida.pk}/album/"

    def test_un_album_recien_empezado_viene_vacio_y_no_da_404(self):
        d = self.get(self.ruta, self.token)
        self.assertEqual((d["total"], d["recompensas"]), (0, []))

    def test_dice_de_donde_salio_cada_recompensa(self):
        """`origen` es justo lo que no se podía contestar sin parsear el texto
        libre del banco: si sale de una misión, de una opción o del final."""
        self.post(f"/api/partidas/{self.partida.pk}/final/",
                  {"final_id": "FINAL_A"}, self.token, 200)
        # Una de misión, para que se vea que los dos orígenes conviven.
        de_mision = RecompensaAlbum.objects.filter(mision__isnull=False).first()
        self.assertIsNotNone(de_mision, "el banco ya no trae recompensas de misión")
        RecompensaObtenida.objects.create(partida=self.partida, recompensa=de_mision)

        d = self.get(self.ruta, self.token)

        self.assertEqual(d["total"], 2)
        origenes = {r["recompensa_id"]: r["origen"] for r in d["recompensas"]}
        self.assertEqual(origenes["ALB_FINAL_A"], {"tipo": "final", "id": "FINAL_A"})
        self.assertEqual(origenes[de_mision.recompensa_id]["tipo"], "mision")
        self.assertEqual(
            origenes[de_mision.recompensa_id]["id"], de_mision.mision.mision_id
        )

    def test_no_se_puede_leer_el_album_de_otro_nino(self):
        otro, token_otro = self._adulto("ajeno2@test.local", "AdultoAjeno2")
        self.get(self.ruta, token_otro, 404)
