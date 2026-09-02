"""Tests del cargador contra el banco de verdad (banco_preguntas/banco_preguntas.json).

A propósito no se usa un JSON inventado: lo que interesa es que el contenido real
que Luis mantiene entre completo a la base. Si el banco crece, las cuentas de acá
cambian — y eso es deseable, obliga a mirar qué cambió.
"""
import json
from io import StringIO
from pathlib import Path

from django.conf import settings
from django.core.management import call_command
from django.core.management.base import CommandError
from django.test import TestCase

from api.models import (
    DialogoNPC,
    Mision,
    OpcionBanco,
    PreguntaBanco,
    RecompensaAlbum,
    RecompensaObtenida,
)
from api.tests.test_catalogo_album import crear_partida

RUTA_BANCO = settings.BASE_DIR.parent.parent / "banco_preguntas" / "banco_preguntas.json"


def cargar(**kwargs):
    """Corre el cargador y devuelve lo que imprimió."""
    salida = StringIO()
    call_command("cargar_banco", stdout=salida, **kwargs)
    return salida.getvalue()


class CargaCompletaTests(TestCase):
    """El bloque que antes se perdía en silencio ahora llega entero."""

    @classmethod
    def setUpTestData(cls):
        cls.banco = json.loads(RUTA_BANCO.read_text(encoding="utf-8"))

    def setUp(self):
        cargar()

    def test_carga_las_preguntas_y_opciones(self):
        esperadas = len(self.banco["preguntas"])
        opciones = sum(len(p.get("opciones_respuesta") or []) for p in self.banco["preguntas"])
        self.assertEqual(PreguntaBanco.objects.count(), esperadas)
        self.assertEqual(OpcionBanco.objects.count(), opciones)

    def test_carga_los_dialogos_de_npc_neutros(self):
        """Esto es lo que `data.get("preguntas")` ignoraba sin avisar."""
        self.assertEqual(DialogoNPC.objects.count(), len(self.banco["dialogos_npc_neutros"]))

    def test_las_lineas_del_dialogo_llegan_completas_y_en_orden(self):
        primero = self.banco["dialogos_npc_neutros"][0]
        obj = DialogoNPC.objects.get(dialogo_id=primero["id"])
        self.assertEqual(obj.lineas, primero["lineas"])

    def test_cada_dialogo_queda_enlazado_a_su_mision(self):
        for d in self.banco["dialogos_npc_neutros"]:
            obj = DialogoNPC.objects.get(dialogo_id=d["id"])
            esperada = d.get("mision_desbloquea")
            self.assertEqual(obj.mision.mision_id if obj.mision else None, esperada)

    def test_las_misiones_conservan_el_id_del_banco(self):
        """El MissionManager de Unity guarda el estado por `desafioId`. Si el id
        de la base no es el del banco, el juego no puede reportar qué completó."""
        del_banco = {d["mision_desbloquea"] for d in self.banco["dialogos_npc_neutros"]
                     if d.get("mision_desbloquea")}
        self.assertEqual(set(Mision.objects.values_list("mision_id", flat=True)), del_banco)

    def test_extrae_todas_las_recompensas_de_album(self):
        self.assertEqual(RecompensaAlbum.objects.count(), 12)
        self.assertFalse(
            RecompensaAlbum.objects.filter(nombre="").exists(),
            "una recompensa quedó sin nombre: el parseo del texto libre se rompió",
        )

    def test_cada_recompensa_tiene_exactamente_un_origen(self):
        por_mision = RecompensaAlbum.objects.filter(mision__isnull=False).count()
        por_opcion = RecompensaAlbum.objects.exclude(opcion_banco_id="").count()
        self.assertEqual(por_mision, 6)
        self.assertEqual(por_opcion, 6)
        self.assertEqual(por_mision + por_opcion, RecompensaAlbum.objects.count())


class RecargaTests(TestCase):
    """Recargar el banco es una operación rutinaria: no puede destruir nada."""

    def test_es_idempotente(self):
        cargar()
        conteos = (PreguntaBanco.objects.count(), OpcionBanco.objects.count(),
                   DialogoNPC.objects.count(), Mision.objects.count(),
                   RecompensaAlbum.objects.count())
        cargar()
        self.assertEqual(
            (PreguntaBanco.objects.count(), OpcionBanco.objects.count(),
             DialogoNPC.objects.count(), Mision.objects.count(),
             RecompensaAlbum.objects.count()),
            conteos,
        )

    def test_el_album_ya_obtenido_sobrevive_a_una_recarga(self):
        """El test que justifica todo el diseño de RecompensaAlbum.

        Si esto falla, cada vez que Luis recargue el banco los niños pierden su
        álbum, y nadie se entera hasta que un apoderado reclame.
        """
        cargar()
        partida = crear_partida()
        premio = RecompensaAlbum.objects.get(recompensa_id="ALB_MISION_SEC_MOCHILA_HUEMUL")
        obtenida = RecompensaObtenida.objects.create(partida=partida, recompensa=premio)

        cargar()

        self.assertTrue(
            RecompensaObtenida.objects.filter(pk=obtenida.pk).exists(),
            "recargar el banco borró el álbum del niño",
        )
        self.assertEqual(
            RecompensaAlbum.objects.get(recompensa_id="ALB_MISION_SEC_MOCHILA_HUEMUL").pk,
            premio.pk,
            "la recompensa se recreó en vez de actualizarse: perdió su PK",
        )

    def test_las_opciones_se_resincronizan_sin_duplicarse(self):
        """Las opciones se borran y recrean; `opcion_id` es único, así que un
        duplicado reventaría. Cubre el flujo viejo, que sigue vigente."""
        cargar()
        antes = OpcionBanco.objects.count()
        cargar()
        self.assertEqual(OpcionBanco.objects.count(), antes)


class FallaRuidosaTests(TestCase):
    """El parseo de recompensas es un puente sobre texto libre. Cuando el texto
    cambie, tiene que reventar — no guardar el álbum a medias."""

    def _banco_con(self, pista):
        banco = json.loads(RUTA_BANCO.read_text(encoding="utf-8"))
        banco["dialogos_npc_neutros"][1]["pista_mision"] = pista
        ruta = Path(settings.BASE_DIR) / "banco_roto_de_prueba.json"
        ruta.write_text(json.dumps(banco, ensure_ascii=False), encoding="utf-8")
        self.addCleanup(ruta.unlink, missing_ok=True)
        return str(ruta)

    def test_revienta_si_el_texto_menciona_album_sin_el_formato(self):
        ruta = self._banco_con("RECOMPENSA DE ÁLBUM: Sticker sin comillas.")
        with self.assertRaises(CommandError) as ctx:
            cargar(archivo=ruta)
        self.assertIn("HDU1_SEC_HUEMUL_MOCHILA", str(ctx.exception))

    def test_al_reventar_no_deja_nada_a_medias(self):
        """La carga es atómica: o entra todo o no entra nada."""
        ruta = self._banco_con("RECOMPENSA DE ÁLBUM: Sticker sin comillas.")
        with self.assertRaises(CommandError):
            cargar(archivo=ruta)
        self.assertEqual(RecompensaAlbum.objects.count(), 0)
        self.assertEqual(PreguntaBanco.objects.count(), 0)

    def test_un_texto_sin_album_no_es_un_error(self):
        """Los diálogos sin recompensa (el guía Huemul) son normales."""
        ruta = self._banco_con("Sigue las huellas hasta el río.")
        cargar(archivo=ruta)
        self.assertEqual(RecompensaAlbum.objects.count(), 11)

    def test_avisa_de_bloques_del_json_que_no_sabe_cargar(self):
        """El modo de falla original: ignorar contenido en silencio."""
        banco = json.loads(RUTA_BANCO.read_text(encoding="utf-8"))
        banco["minijuegos_nuevos"] = [{"id": "X"}]
        ruta = Path(settings.BASE_DIR) / "banco_bloque_nuevo.json"
        ruta.write_text(json.dumps(banco, ensure_ascii=False), encoding="utf-8")
        self.addCleanup(ruta.unlink, missing_ok=True)

        salida = cargar(archivo=str(ruta))
        self.assertIn("minijuegos_nuevos", salida)
        self.assertIn("NO carga", salida)
