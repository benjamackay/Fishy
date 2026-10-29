from datetime import datetime, timezone

from django.test import TestCase, override_settings
from rest_framework.test import APIClient

from api.models import (AdultoResponsable, CasoDetective, CasoDetectiveProgreso, GrupoTutor, MiembroGrupo,
                        Partida, UsuarioJugador)


def caso(caso_id, zona):
    return CasoDetective.objects.create(caso_id=caso_id, titulo=caso_id, zona=zona, permiso_player_text="x",
                                        permiso_npc_nombre="x", permiso_npc_response="x")


def intento(nino, caso, aciertos, total, fecha, partida=None):
    partida = partida or Partida.objects.create(usuario_jugador=nino)
    progreso = CasoDetectiveProgreso.objects.create(partida=partida, caso=caso, aciertos=aciertos, total_riesgo=total,
                                                    porcentaje=aciertos / total, mensajes_marcados=["privado"])
    # fecha_termino la fija la vista; aquí se escribe directo para controlar el período.
    CasoDetectiveProgreso.objects.filter(pk=progreso.pk).update(fecha_termino=fecha)
    return partida


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class ReporteDetectiveTests(TestCase):
    """Ticket 09-29: una partida con solo el caso detective no aportaba nada al reporte."""

    def setUp(self):
        self.padre = AdultoResponsable.objects.create_user(nombre="Familia", email="familia@example.com", password="x")
        self.nina = UsuarioJugador.objects.create(adulto=self.padre, nombre="Martina")
        self.desconocidos = caso("DC_CASO_01", "desconocidos")
        self.retos = caso("DC_CASO_03", "reto_viral")
        self.client = APIClient()
        self.client.force_authenticate(self.padre)
        self.url = f"/api/jugadores/{self.nina.pk}/reporte/"

    def tematicas(self, **params):
        return {t["tematica"]: t for t in self.client.get(self.url, params).data["tematicas"]}

    def test_partida_solo_con_detective_aparece_aparte_de_las_decisiones(self):
        intento(self.nina, self.desconocidos, 1, 4, datetime(2026, 10, 6, 15, tzinfo=timezone.utc))
        tema = self.tematicas()["desconocidos"]
        self.assertIsNone(tema["metricas"])
        self.assertEqual(tema["detective"], {"senales_detectadas": 1, "senales_totales": 4, "casos": 1})
        self.assertIsNone(self.tematicas()["ciberacoso"]["detective"])

    def test_el_slug_reto_viral_del_caso_llega_como_retos_virales(self):
        intento(self.nina, self.retos, 2, 3, datetime(2026, 10, 6, 15, tzinfo=timezone.utc))
        self.assertEqual(self.tematicas()["retos_virales"]["detective"]["senales_detectadas"], 2)

    def test_repetir_el_caso_en_otra_partida_cuenta_solo_el_ultimo_intento(self):
        intento(self.nina, self.desconocidos, 4, 4, datetime(2026, 10, 7, 15, tzinfo=timezone.utc))
        intento(self.nina, self.desconocidos, 1, 4, datetime(2026, 10, 6, 15, tzinfo=timezone.utc))
        self.assertEqual(self.tematicas()["desconocidos"]["detective"], {"senales_detectadas": 4, "senales_totales": 4, "casos": 1})

    def test_periodo_filtra_por_fecha_de_termino_en_hora_de_chile(self):
        # 02:59 UTC del 6 = 23:59 del 5 en Chile.
        intento(self.nina, self.desconocidos, 3, 4, datetime(2026, 10, 6, 2, 59, tzinfo=timezone.utc))
        self.assertEqual(self.tematicas(inicio="2026-10-05", fin="2026-10-05")["desconocidos"]["detective"]["casos"], 1)
        self.assertIsNone(self.tematicas(inicio="2026-10-06", fin="2026-10-06")["desconocidos"]["detective"])

    def test_no_expone_las_marcas_del_nino(self):
        intento(self.nina, self.desconocidos, 1, 4, datetime(2026, 10, 6, 15, tzinfo=timezone.utc))
        self.assertNotIn("privado", str(self.client.get(self.url).data))


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"], FISHY_REPORTE_MINIMO=3)
class ReporteDetectiveGrupoTests(TestCase):
    def setUp(self):
        padre = AdultoResponsable.objects.create_user(nombre="Familia", email="familia@example.com", password="x")
        profesor = AdultoResponsable.objects.create_user(nombre="Profe", email="profe@example.com", password="x", rol="profesor")
        self.grupo = GrupoTutor.objects.create(nombre="Curso", tutor=profesor)
        self.ninos = [UsuarioJugador.objects.create(adulto=padre, nombre=f"Nino{i}") for i in range(3)]
        for nino in self.ninos:
            MiembroGrupo.objects.create(grupo=self.grupo, jugador=nino)
        self.caso = caso("DC_CASO_01", "desconocidos")
        self.client = APIClient()
        self.client.force_authenticate(profesor)
        self.url = f"/api/grupos/{self.grupo.pk}/reporte/"

    def test_respeta_el_minimo_de_participantes_y_los_cuenta(self):
        fecha = datetime(2026, 10, 6, 15, tzinfo=timezone.utc)
        for nino in self.ninos[:2]:
            intento(nino, self.caso, 2, 4, fecha)
        r = self.client.get(self.url).data
        self.assertIsNone(r["tematicas"][0]["detective"])
        self.assertEqual(r["participantes_con_resultados"], 2)
        intento(self.ninos[2], self.caso, 4, 4, fecha)
        r = self.client.get(self.url).data
        self.assertEqual(r["tematicas"][0]["detective"], {"senales_detectadas": 8, "senales_totales": 12, "casos": 3})
        self.assertEqual(r["participantes_con_resultados"], 3)
