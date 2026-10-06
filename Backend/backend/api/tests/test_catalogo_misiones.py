"""Tests del catálogo de misiones y del avance por objetivo (parte B).

El catálogo se carga del archivo de verdad (`Assets/Resources/misiones.json`),
igual que test_cargar_banco con el banco: es la única copia de ese contenido, y
si el cargador deja algo fuera lo que interesa es enterarse.
"""
import json
from io import StringIO

from django.conf import settings
from django.core.management import call_command
from django.core.management.base import CommandError
from django.test import TestCase
from rest_framework.test import APIClient

from api.models import (
    AdultoResponsable, Mision, ObjetivoMision, ObjetivoProgreso,
    RecompensaAlbum, RecompensaObtenida,
)
from api.tests.test_catalogo_album import crear_partida

RUTA_MISIONES = settings.BASE_DIR.parent.parent / "Fishy!" / "Assets" / "Resources" / "misiones.json"


def cargar(**kwargs):
    salida = StringIO()
    call_command("cargar_banco", stdout=salida, **kwargs)
    return salida.getvalue()


class CatalogoDesdeElArchivoTests(TestCase):
    @classmethod
    def setUpTestData(cls):
        cls.archivo = json.loads(RUTA_MISIONES.read_text(encoding="utf-8"))["misiones"]

    def setUp(self):
        cargar()

    def test_llegan_todas_las_misiones_con_su_orden_y_zona_objetivo(self):
        """El banco solo trae id y nombre: esto es lo que se perdía."""
        for m in self.archivo:
            obj = Mision.objects.get(mision_id=m["mision_id"])
            self.assertEqual(obj.orden, m["orden"])
            self.assertEqual(obj.zona_objetivo, m.get("zona_objetivo", ""))
            self.assertEqual(obj.zona, m.get("zona", ""))

    def test_el_titulo_del_archivo_gana_cuando_el_banco_no_trae_nombre(self):
        """`MISION_EXPLORACION_01` no tiene nombre en el banco. Si el título del
        archivo no llegara, el cartel saldría con el id crudo."""
        obj = Mision.objects.get(mision_id="MISION_EXPLORACION_01")
        self.assertEqual(obj.nombre, "Conoce el Bosque")

    def test_los_objetivos_llegan_con_los_campos_de_su_tipo(self):
        esperados = sum(len(m.get("objetivos") or []) for m in self.archivo)
        self.assertEqual(ObjetivoMision.objects.count(), esperados)

        # Una de cada tipo que hoy existe en el archivo.
        chat = ObjetivoMision.objects.get(mision__mision_id="MISION_NPC_03", orden=1)
        self.assertEqual(chat.tipo, "chatear_telefono")
        self.assertEqual(chat.escenario_ids, "M1_CHAT01")

        caso = ObjetivoMision.objects.get(mision__mision_id="MISION_NPC_03", orden=3)
        self.assertEqual(caso.tipo, "completar_caso_detective")
        self.assertEqual(caso.caso_id, "DC_CASO_01")

        npc = ObjetivoMision.objects.get(mision__mision_id="MISION_EXPLORACION_01", orden=1)
        self.assertEqual(npc.tipo, "hablar_npc")
        self.assertEqual(npc.dialogo_id, "HDU1_NPC_HUEMUL")

    def test_recargar_no_duplica_los_objetivos(self):
        antes = ObjetivoMision.objects.count()
        cargar()
        self.assertEqual(ObjetivoMision.objects.count(), antes)

    def test_un_tipo_de_objetivo_desconocido_revienta(self):
        """Un tipo que Unity no sabe interpretar deja el objetivo muerto en el
        panel sin que nadie se entere. Mejor no cargar."""
        data = json.loads(RUTA_MISIONES.read_text(encoding="utf-8"))
        data["misiones"] = [m for m in data["misiones"] if m.get("objetivos")][:1]
        data["misiones"][0]["objetivos"][0]["tipo"] = "bailar_cueca"
        ruta = settings.BASE_DIR / "misiones_rotas_de_prueba.json"
        ruta.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
        self.addCleanup(ruta.unlink, missing_ok=True)

        with self.assertRaises(CommandError) as ctx:
            cargar(archivo_misiones=str(ruta))
        self.assertIn("bailar_cueca", str(ctx.exception))


class RecargaNoDestruyeTests(TestCase):
    """B.5: recargar el banco es rutina y no puede costarle el álbum a un niño."""

    def test_limpiar_ya_no_se_lleva_el_album_ganado(self):
        cargar()
        partida = crear_partida()
        premio = RecompensaAlbum.objects.get(recompensa_id="ALB_MISION_SEC_MOCHILA_HUEMUL")
        obtenida = RecompensaObtenida.objects.create(partida=partida, recompensa=premio)

        cargar(limpiar=True)

        self.assertTrue(
            RecompensaObtenida.objects.filter(pk=obtenida.pk).exists(),
            "--limpiar borró el álbum que el niño ya se había ganado",
        )

    def test_borrar_misiones_sigue_disponible_cuando_se_pide_a_proposito(self):
        """El borrado no desaparece, pasa a ser explícito. Se nota en que las 3
        misiones que solo existen en el archivo no vuelven: las del banco sí,
        porque las recrean sus diálogos en la misma corrida."""
        cargar()
        solo_del_archivo = "MISION_PANTANO_CRIATURAS"
        self.assertTrue(Mision.objects.filter(mision_id=solo_del_archivo).exists())

        salida = cargar(borrar_misiones=True, sin_misiones=True)

        self.assertIn("recompensas", salida)
        self.assertFalse(Mision.objects.filter(mision_id=solo_del_archivo).exists())
        self.assertEqual(RecompensaObtenida.objects.count(), 0)


class ApiCatalogoTests(TestCase):
    def setUp(self):
        cargar()
        self.client = APIClient()

    def test_el_listado_no_pide_sesion(self):
        """Unity lo pide al arrancar, antes de que nadie haya entrado (B.2)."""
        resp = self.client.get("/api/misiones/")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(len(resp.data), Mision.objects.count())

    def test_la_raiz_es_un_arreglo_y_no_un_objeto(self):
        """`ApiManager.ObtenerCatalogoMisiones` espera `Send<List<MisionRegistro>>`:
        con `{version, misiones}` Unity no parsea nada."""
        self.assertIsInstance(self.client.get("/api/misiones/").data, list)

    def test_una_mision_sin_objetivos_manda_la_lista_vacia_igual(self):
        mision = next(m for m in self.client.get("/api/misiones/").data
                      if m["mision_id"] == "MISION_SEC_MOCHILA_HUEMUL")
        self.assertEqual(mision["objetivos"], [])

    def test_los_campos_que_no_aplican_van_vacios_y_no_ausentes(self):
        """JsonUtility no distingue ausente de vacío: el contrato es mandarlos
        siempre (B.2)."""
        detalle = self.client.get("/api/misiones/MISION_NPC_03/").data
        objetivo = detalle["objetivos"][0]
        for campo in ("item_id", "dialogo_id", "escenario_ids", "zona_id", "caso_id",
                      "descripcion", "cantidad", "orden", "tipo"):
            self.assertIn(campo, objetivo)
        self.assertEqual(objetivo["dialogo_id"], "")

    def test_el_detalle_trae_titulo_y_nombre_por_compatibilidad(self):
        detalle = self.client.get("/api/misiones/MISION_EXPLORACION_01/").data
        self.assertEqual(detalle["titulo"], detalle["nombre"])
        self.assertEqual(detalle["titulo"], "Conoce el Bosque")

    def test_un_id_que_no_existe_da_404(self):
        self.assertEqual(self.client.get("/api/misiones/NO_EXISTE/").status_code, 404)


class AvancePorObjetivoTests(TestCase):
    """B.3. Mismo contrato que las misiones y las zonas: camino de ida (A.1) y
    no-op seguro ante el mismo valor repetido (A.2)."""

    def setUp(self):
        cargar()
        self.partida = crear_partida()
        self.adulto = self.partida.usuario_jugador.adulto
        self.client = APIClient()
        self.client.force_authenticate(self.adulto)
        self.ruta = f"/api/partidas/{self.partida.pk}/objetivos/"

    def test_marcar_un_objetivo_lo_guarda(self):
        resp = self.client.post(
            self.ruta, {"mision_id": "MISION_NPC_03", "orden": 1, "cumplido": True},
            format="json",
        )
        self.assertEqual(resp.status_code, 200)
        self.assertTrue(resp.data["cumplido"])
        self.assertEqual(ObjetivoProgreso.objects.count(), 1)

    def test_repetir_el_mismo_aviso_no_duplica_ni_falla(self):
        """La cola de Unity reintenta hasta 3 veces y puede reenviar un cambio
        cuya respuesta nunca llegó."""
        cuerpo = {"mision_id": "MISION_NPC_03", "orden": 1, "cumplido": True}
        for _ in range(3):
            self.assertEqual(
                self.client.post(self.ruta, cuerpo, format="json").status_code, 200
            )
        self.assertEqual(ObjetivoProgreso.objects.count(), 1)

    def test_un_objetivo_cumplido_no_vuelve_atras(self):
        """Los avisos no llegan en orden garantizado: uno viejo no puede
        deshacer uno nuevo."""
        self.client.post(
            self.ruta, {"mision_id": "MISION_NPC_03", "orden": 1, "cumplido": True},
            format="json",
        )
        resp = self.client.post(
            self.ruta, {"mision_id": "MISION_NPC_03", "orden": 1, "cumplido": False},
            format="json",
        )
        self.assertTrue(resp.data["cumplido"])
        self.assertTrue(ObjetivoProgreso.objects.get().cumplido)

    def test_un_objetivo_fuera_del_catalogo_se_guarda_igual(self):
        """El avance del niño no depende de que el catálogo esté al día."""
        resp = self.client.post(
            self.ruta, {"mision_id": "MISION_QUE_NO_EXISTE", "orden": 9, "cumplido": True},
            format="json",
        )
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(ObjetivoProgreso.objects.count(), 1)

    def test_recargar_el_catalogo_no_borra_el_avance(self):
        """La razón por la que ObjetivoProgreso no tiene FK: el cargador borra y
        recrea los objetivos en cada corrida."""
        self.client.post(
            self.ruta, {"mision_id": "MISION_NPC_03", "orden": 1, "cumplido": True},
            format="json",
        )
        cargar()
        self.assertEqual(ObjetivoProgreso.objects.filter(cumplido=True).count(), 1)

    def test_el_get_devuelve_lo_guardado(self):
        self.client.post(
            self.ruta, {"mision_id": "MISION_NPC_03", "orden": 2, "cumplido": True},
            format="json",
        )
        datos = self.client.get(self.ruta).data
        self.assertEqual(len(datos), 1)
        self.assertEqual(datos[0]["mision_id"], "MISION_NPC_03")
        self.assertEqual(datos[0]["orden"], 2)

    def test_falta_el_orden_es_400(self):
        resp = self.client.post(
            self.ruta, {"mision_id": "MISION_NPC_03", "cumplido": True}, format="json",
        )
        self.assertEqual(resp.status_code, 400)

    def test_la_partida_de_otro_adulto_no_se_toca(self):
        otra = crear_partida(nombre="Hija de otra cuenta")
        resp = self.client.post(
            f"/api/partidas/{otra.pk}/objetivos/",
            {"mision_id": "MISION_NPC_03", "orden": 1, "cumplido": True}, format="json",
        )
        self.assertEqual(resp.status_code, 404)
        self.assertEqual(ObjetivoProgreso.objects.count(), 0)
