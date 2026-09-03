"""Tests de las tablas nuevas: Mision, DialogoNPC, RecompensaAlbum y el álbum obtenido.

Lo que se prueba acá son las reglas que la base tiene que hacer cumplir sola,
sin depender de que el código que escribe se acuerde de respetarlas.
"""
from django.db import IntegrityError, transaction
from django.test import TestCase

from api.models import (
    AdultoResponsable,
    Mision,
    NivelRiesgo,
    OpcionBanco,
    Partida,
    PreguntaBanco,
    RecompensaAlbum,
    RecompensaObtenida,
    UsuarioJugador,
)


def crear_partida(nombre="Otto de prueba"):
    adulto = AdultoResponsable.objects.create_user(
        email=f"{nombre.replace(' ', '-').lower()}@test.local",
        nombre=nombre,
        password="clave-de-prueba-123",
    )
    jugador = UsuarioJugador.objects.create(adulto=adulto, nombre=nombre, edad=9)
    nivel, _ = NivelRiesgo.objects.get_or_create(
        nombre="bajo", defaults={"descripcion": "prueba"}
    )
    return Partida.objects.create(usuario_jugador=jugador, nivel_riesgo=nivel)


class RecompensaOrigenUnicoTests(TestCase):
    """Cada recompensa viene de una misión o de una opción, nunca de las dos.

    Es un CheckConstraint y no una validación en Python porque el cargador no es
    el único que escribe: el admin, un shell o un script futuro también pueden, y
    una recompensa con dos orígenes (o ninguno) haría que el álbum del apoderado
    contara mal sin que nada fallara.
    """

    def setUp(self):
        self.mision = Mision.objects.create(
            mision_id="MISION_TEST_01", nombre="Misión de prueba", zona="desconocidos"
        )

    def test_acepta_origen_en_mision(self):
        RecompensaAlbum.objects.create(
            recompensa_id="ALB_MISION_TEST_01", nombre="Sticker de prueba", mision=self.mision
        )
        self.assertEqual(RecompensaAlbum.objects.count(), 1)

    def test_acepta_origen_en_opcion(self):
        RecompensaAlbum.objects.create(
            recompensa_id="ALB_OPCION_TEST", nombre="Estampa de prueba",
            opcion_banco_id="HDU2_NPC01_Q01_R1",
        )
        self.assertEqual(RecompensaAlbum.objects.count(), 1)

    def test_rechaza_los_dos_origenes_a_la_vez(self):
        with self.assertRaises(IntegrityError):
            with transaction.atomic():
                RecompensaAlbum.objects.create(
                    recompensa_id="ALB_DOBLE", nombre="Imposible",
                    mision=self.mision, opcion_banco_id="HDU2_NPC01_Q01_R1",
                )

    def test_rechaza_recompensa_sin_origen(self):
        with self.assertRaises(IntegrityError):
            with transaction.atomic():
                RecompensaAlbum.objects.create(recompensa_id="ALB_HUERFANA", nombre="Sin origen")


class RecompensaObtenidaTests(TestCase):
    def setUp(self):
        self.partida = crear_partida()
        mision = Mision.objects.create(mision_id="MISION_TEST_02", zona="ciberacoso")
        self.premio = RecompensaAlbum.objects.create(
            recompensa_id="ALB_MISION_TEST_02", nombre="Cromo de prueba", mision=mision
        )

    def test_registra_lo_que_desbloqueo_el_nino(self):
        RecompensaObtenida.objects.create(partida=self.partida, recompensa=self.premio)
        album = self.partida.recompensas_album.all()
        self.assertEqual([r.recompensa.nombre for r in album], ["Cromo de prueba"])

    def test_no_se_puede_obtener_dos_veces_la_misma(self):
        """Sin esto, rejugar una misión inflaría el álbum con duplicados."""
        RecompensaObtenida.objects.create(partida=self.partida, recompensa=self.premio)
        with self.assertRaises(IntegrityError):
            with transaction.atomic():
                RecompensaObtenida.objects.create(partida=self.partida, recompensa=self.premio)

    def test_dos_ninos_pueden_obtener_la_misma_recompensa(self):
        """La restricción es por partida, no global: el álbum es de cada niño."""
        otra = crear_partida("Otro nino")
        RecompensaObtenida.objects.create(partida=self.partida, recompensa=self.premio)
        RecompensaObtenida.objects.create(partida=otra, recompensa=self.premio)
        self.assertEqual(RecompensaObtenida.objects.count(), 2)


class OpcionBancoIdNoEsFKTests(TestCase):
    """`RecompensaAlbum.opcion_banco_id` es texto y no ForeignKey, a propósito.

    `cargar_banco` borra y recrea las opciones en cada corrida. Con una FK real,
    esa recarga se llevaría en cascada el álbum que los niños ya obtuvieron. Este
    test fija esa decisión: si alguien la convierte en FK, acá revienta.
    """

    def test_borrar_la_opcion_no_toca_la_recompensa(self):
        pregunta = PreguntaBanco.objects.create(
            pregunta_id="P_TEST", hdu="HDU-2", zona="desconocidos",
            categoria="prueba", mensaje_npc="hola",
        )
        OpcionBanco.objects.create(
            pregunta=pregunta, opcion_id="P_TEST_R1", texto="respuesta",
            tipo="segura_optima", consecuencia_narrativa="algo",
        )
        premio = RecompensaAlbum.objects.create(
            recompensa_id="ALB_P_TEST_R1", nombre="Estampa", opcion_banco_id="P_TEST_R1"
        )

        pregunta.opciones.all().delete()   # lo que hace el cargador en cada corrida

        premio.refresh_from_db()
        self.assertEqual(premio.opcion_banco_id, "P_TEST_R1")
        self.assertEqual(RecompensaAlbum.objects.count(), 1)
