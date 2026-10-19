"""Tests de la recompensa del Modo Detective (HDU-11, parte C de REQUISITOS_BD).

Mismo criterio que test_cargar_banco: se carga el archivo de verdad
(`banco_preguntas/detective_cases.json`), no uno inventado, porque lo que
interesa es que el contenido que Luis mantiene llegue entero a la base.

El contrato con Unity es al revés de lo habitual: `DetectiveCaseManager` usa los
5 campos SOLO si vienen con algo, y si llegan vacíos cae a su catálogo local
(`CatalogoRecompensasDetective`). O sea que "vacío" es un valor válido y
significa "este caso no entrega nada", no "se perdió el dato".
"""
import json
from io import StringIO
from pathlib import Path

from django.conf import settings
from django.core.management import call_command
from django.test import TestCase
from rest_framework.test import APIClient

from api.models import AdultoResponsable, CasoDetective

RUTA_CASOS = settings.BASE_DIR.parent.parent / "banco_preguntas" / "detective_cases.json"


def cargar(**kwargs):
    salida = StringIO()
    call_command("cargar_detective", stdout=salida, **kwargs)
    return salida.getvalue()


class RecompensaDesdeElArchivoTests(TestCase):
    @classmethod
    def setUpTestData(cls):
        cls.casos = json.loads(RUTA_CASOS.read_text(encoding="utf-8"))["casos"]

    def setUp(self):
        cargar()

    def test_cada_caso_del_archivo_trae_su_recompensa_a_la_base(self):
        """Antes este bloque del JSON no lo leía nadie: el contenido existía y
        no llegaba a la base. Es justo lo que pide la parte C."""
        for c in self.casos:
            esperada = c.get("recompensa") or {}
            if not esperada:
                continue
            obj = CasoDetective.objects.get(caso_id=c["id"])
            self.assertEqual(obj.recompensa_item_id, esperada["item_id"])
            self.assertEqual(obj.recompensa_nombre, esperada["nombre"])
            self.assertEqual(obj.recompensa_accesorio_hdu06, esperada["accesorio_hdu06"])
            self.assertEqual(obj.recompensa_umbral_aciertos, esperada["umbral_aciertos"])
            self.assertEqual(
                obj.recompensa_no_duplica_al_repetir, esperada["no_duplica_al_repetir"]
            )

    def test_ningun_caso_queda_sin_pin(self):
        """Hoy los 3 casos entregan uno. Si mañana entra un caso sin recompensa
        esto avisa, que es la conversación que hay que tener antes de publicarlo."""
        self.assertEqual(CasoDetective.objects.count(), len(self.casos))
        self.assertFalse(
            CasoDetective.objects.filter(recompensa_item_id="").exists(),
            "un caso quedó sin item_id: o el archivo no lo trae o el cargador no lo leyó",
        )

    def test_los_item_id_no_se_repiten_entre_casos(self):
        """CA1 de HDU-11 pide un ítem *único* por caso. Es la misma trampa que
        dejó la medalla dorada duplicada en el álbum, en el otro banco."""
        ids = list(CasoDetective.objects.values_list("recompensa_item_id", flat=True))
        self.assertEqual(len(ids), len(set(ids)), f"hay pines repetidos entre casos: {ids}")

    def test_recargar_no_pierde_ni_duplica_la_recompensa(self):
        antes = {c.caso_id: c.recompensa_item_id for c in CasoDetective.objects.all()}
        cargar()
        self.assertEqual(
            {c.caso_id: c.recompensa_item_id for c in CasoDetective.objects.all()},
            antes,
        )


class CasoSinRecompensaTests(TestCase):
    """El fallback de Unity depende de que un caso sin `recompensa` quede vacío
    en vez de reventar o de inventarse un valor."""

    def _archivo_sin_recompensa(self):
        data = json.loads(RUTA_CASOS.read_text(encoding="utf-8"))
        data["casos"] = data["casos"][:1]
        data["casos"][0].pop("recompensa", None)
        ruta = Path(settings.BASE_DIR) / "detective_sin_recompensa_de_prueba.json"
        ruta.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
        self.addCleanup(ruta.unlink, missing_ok=True)
        return str(ruta)

    def test_un_caso_sin_bloque_recompensa_carga_igual_y_queda_vacio(self):
        cargar(archivo=self._archivo_sin_recompensa())
        caso = CasoDetective.objects.get()
        self.assertEqual(caso.recompensa_item_id, "")
        self.assertEqual(caso.recompensa_nombre, "")
        self.assertEqual(caso.recompensa_accesorio_hdu06, "")
        # Los dos que no son texto conservan el default del modelo, que es lo que
        # Unity asume cuando decide por su cuenta.
        self.assertEqual(caso.recompensa_umbral_aciertos, 0.5)
        self.assertTrue(caso.recompensa_no_duplica_al_repetir)


class ApiExponeLaRecompensaTests(TestCase):
    """Sin esto, el dato llega a la base y se queda ahí: Unity lee la API."""

    CAMPOS = [
        "recompensa_item_id",
        "recompensa_nombre",
        "recompensa_accesorio_hdu06",
        "recompensa_umbral_aciertos",
        "recompensa_no_duplica_al_repetir",
    ]

    def setUp(self):
        cargar()
        # El catálogo del Detective va detrás de sesión, igual que el resto de la
        # API del juego: Unity lo pide con el token del adulto ya autenticado.
        adulto = AdultoResponsable.objects.create_user(
            email="detective@test.local", nombre="Adulto de prueba",
            password="clave-de-prueba-123",
        )
        self.client = APIClient()
        self.client.force_authenticate(adulto)

    def test_el_listado_trae_los_cinco_campos_con_contenido(self):
        resp = self.client.get("/api/casos-detective/")
        self.assertEqual(resp.status_code, 200)
        self.assertTrue(resp.data)
        for caso in resp.data:
            for campo in self.CAMPOS:
                self.assertIn(campo, caso)
            self.assertNotEqual(caso["recompensa_item_id"], "")

    def test_el_detalle_de_un_caso_tambien(self):
        resp = self.client.get("/api/casos-detective/DC_CASO_01/")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.data["recompensa_item_id"], "PIN_VIGIA_SILENCIOSO")
        self.assertEqual(resp.data["recompensa_umbral_aciertos"], 0.5)
