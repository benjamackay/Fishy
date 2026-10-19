"""Tests del flujo que ya existía, para comprobar que la migración 0004 no lo tocó.

La 0004 es puramente aditiva (cuatro CREATE TABLE, ningún ALTER), así que en
teoría no puede romper nada. Estos tests lo dejan escrito: si alguien más adelante
convierte `opcion_banco_id` en ForeignKey o le cuelga algo a las tablas viejas,
acá se nota.
"""
from django.db import IntegrityError, transaction
from django.test import TestCase

from api.models import (
    AdultoResponsable,
    Chat,
    Mensaje,
    NPC,
    NivelRiesgo,
    OpcionBanco,
    Partida,
    PreguntaBanco,
    UsuarioJugador,
)


class CadenaDeControlParentalTests(TestCase):
    """Adulto -> menor -> partida -> NPC -> chat -> mensaje.

    Es la columna vertebral del control parental: todo cuelga del adulto, y de ahí
    sale el aislamiento entre familias.
    """

    def setUp(self):
        self.adulto = AdultoResponsable.objects.create_user(
            email="apoderado@test.local", nombre="Apoderado", password="clave-de-prueba-123"
        )
        self.jugador = UsuarioJugador.objects.create(
            adulto=self.adulto, nombre="Otto de prueba", edad=9
        )
        self.nivel, _ = NivelRiesgo.objects.get_or_create(
            nombre="bajo", defaults={"descripcion": "prueba"}
        )
        self.partida = Partida.objects.create(
            usuario_jugador=self.jugador, nivel_riesgo=self.nivel
        )

    def test_la_cadena_completa_se_arma(self):
        npc = NPC.objects.create(partida=self.partida, nombre="Puma", area="desconocidos")
        chat = Chat.objects.create(partida=self.partida, npc=npc)
        Mensaje.objects.create(chat=chat, tipo="start", respuesta="hola")
        self.assertEqual(self.partida.chats.count(), 1)
        self.assertEqual(chat.mensajes.count(), 1)

    def test_borrar_al_adulto_se_lleva_todo_lo_del_menor(self):
        """El cascade es lo que permite que el smoke test limpie con un solo delete,
        y lo que garantiza que dar de baja una cuenta no deje datos del niño sueltos."""
        npc = NPC.objects.create(partida=self.partida, nombre="Puma", area="desconocidos")
        chat = Chat.objects.create(partida=self.partida, npc=npc)
        Mensaje.objects.create(chat=chat, tipo="start", respuesta="hola")

        self.adulto.delete()

        self.assertEqual(Partida.objects.count(), 0)
        self.assertEqual(Chat.objects.count(), 0)
        self.assertEqual(Mensaje.objects.count(), 0)

    def test_un_menor_no_puede_repetir_nombre_bajo_el_mismo_adulto(self):
        with self.assertRaises(IntegrityError):
            with transaction.atomic():
                UsuarioJugador.objects.create(
                    adulto=self.adulto, nombre="Otto de prueba", edad=10
                )


class RiesgoPorZonaTests(TestCase):
    """El mecanismo de `opcion_banco_id`: texto que se resuelve contra el banco.

    Así es como `riesgo_por_zona` acumula el puntaje real (-1 / +1 / +2) en vez de
    deducirlo de `calidad_respuesta`, que no distingue una respuesta segura básica
    de una óptima.
    """

    def setUp(self):
        self.pregunta = PreguntaBanco.objects.create(
            pregunta_id="HDU2_TEST_Q01", hdu="HDU-2", zona="desconocidos",
            categoria="prueba", mensaje_npc="¿me pasas tu dirección?",
        )
        self.opcion = OpcionBanco.objects.create(
            pregunta=self.pregunta, opcion_id="HDU2_TEST_Q01_R1",
            texto="no comparto mi dirección", tipo="segura_optima",
            consecuencia_narrativa="Otto se protege", impacto_puntuacion=2,
        )

        adulto = AdultoResponsable.objects.create_user(
            email="riesgo@test.local", nombre="Riesgo", password="clave-de-prueba-123"
        )
        jugador = UsuarioJugador.objects.create(adulto=adulto, nombre="Otto", edad=9)
        nivel, _ = NivelRiesgo.objects.get_or_create(
            nombre="bajo", defaults={"descripcion": "prueba"}
        )
        self.partida = Partida.objects.create(usuario_jugador=jugador, nivel_riesgo=nivel)
        npc = NPC.objects.create(partida=self.partida, nombre="Puma", area="desconocidos")
        self.chat = Chat.objects.create(partida=self.partida, npc=npc)

    def _resolver(self):
        """Réplica de lo que hace la vista riesgo_por_zona."""
        elegidos = list(
            Mensaje.objects.filter(chat__partida=self.partida)
            .exclude(opcion_banco_id__isnull=True)
            .exclude(opcion_banco_id="")
            .values_list("opcion_banco_id", flat=True)
        )
        opciones = {
            o.opcion_id: o
            for o in OpcionBanco.objects.filter(opcion_id__in=set(elegidos))
            .select_related("pregunta")
        }
        return elegidos, opciones

    def test_una_opcion_del_banco_suma_su_impacto_real(self):
        Mensaje.objects.create(
            chat=self.chat, tipo="request", respuesta="no comparto mi dirección",
            opcion_banco_id="HDU2_TEST_Q01_R1",
        )
        elegidos, opciones = self._resolver()
        total = sum(opciones[o].impacto_puntuacion for o in elegidos if o in opciones)
        self.assertEqual(total, 2)
        self.assertEqual(opciones["HDU2_TEST_Q01_R1"].pregunta.zona, "desconocidos")

    def test_un_id_que_no_existe_en_el_banco_no_suma_ni_revienta(self):
        """Pasa cuando Unity va con un banco viejo. Tiene que quedar sin clasificar,
        no romper la pantalla del apoderado ni contarse como riesgo."""
        Mensaje.objects.create(
            chat=self.chat, tipo="request", respuesta="algo",
            opcion_banco_id="ID_QUE_NO_EXISTE",
        )
        elegidos, opciones = self._resolver()
        self.assertEqual(len(elegidos), 1)
        self.assertEqual(len(opciones), 0)

    def test_recargar_el_banco_no_rompe_el_vinculo_del_mensaje(self):
        """`opcion_banco_id` es texto: sobrevive a que la opción se borre y se
        recree con otra PK, que es justo lo que hace `cargar_banco`."""
        Mensaje.objects.create(
            chat=self.chat, tipo="request", respuesta="no comparto mi dirección",
            opcion_banco_id="HDU2_TEST_Q01_R1",
        )
        self.pregunta.opciones.all().delete()
        OpcionBanco.objects.create(
            pregunta=self.pregunta, opcion_id="HDU2_TEST_Q01_R1",
            texto="no comparto mi dirección", tipo="segura_optima",
            consecuencia_narrativa="Otto se protege", impacto_puntuacion=2,
        )

        elegidos, opciones = self._resolver()
        self.assertEqual(sum(opciones[o].impacto_puntuacion for o in elegidos), 2)
