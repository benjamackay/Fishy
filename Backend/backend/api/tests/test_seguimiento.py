from datetime import timedelta

from django.conf import settings
from django.core.cache import cache
from django.core.exceptions import ImproperlyConfigured
from django.test import TestCase, override_settings
from django.utils import timezone
from rest_framework.test import APIClient

from api.models import (AdultoResponsable, GrupoTutor, MiembroGrupo, InvitacionGrupo,
                        UsuarioJugador, Partida, NPC, Chat, Mensaje, PreguntaBanco, OpcionBanco)
from api.seguimiento import criterios_seguimiento


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class SeguimientoTests(TestCase):
    def setUp(self):
        cache.clear()
        self.profe = AdultoResponsable.objects.create_user("profe", "profe@example.com", "clave", rol="profesor")
        self.otro = AdultoResponsable.objects.create_user("otro", "otro@example.com", "clave", rol="profesor")
        self.padre = AdultoResponsable.objects.create_user("padre", "familia@example.com", "clave")
        self.nina = UsuarioJugador.objects.create(adulto=self.padre, nombre="Martina")
        self.hermano = UsuarioJugador.objects.create(adulto=self.padre, nombre="Tomás")
        self.grupo = GrupoTutor.objects.create(tutor=self.profe, nombre="Curso")
        self.miembro = MiembroGrupo.objects.create(grupo=self.grupo, jugador=self.nina, nombre_invitado="Martina")
        self.url = f"/api/grupos/{self.grupo.pk}/seguimiento/"
        self.client = APIClient()
        self.client.force_authenticate(self.profe)
        self.opciones = {}
        # Tema del portal -> slug real del banco. `reto_viral` es como lo guarda el banco;
        # si el seguimiento filtrara por `retos_virales` esa temática quedaría vacía.
        for i, (tema, zona) in enumerate((("desconocidos", "desconocidos"), ("ciberacoso", "ciberacoso"),
                                          ("retos_virales", "reto_viral"))):
            p = PreguntaBanco.objects.create(pregunta_id=f"P{i}", hdu="HDU-2", zona=zona,
                                            categoria=zona, mensaje_npc="ConversacionPrivada")
            for tipo in ("segura_basica", "segura_optima", "insegura"):
                self.opciones[tema, tipo] = OpcionBanco.objects.create(pregunta=p, opcion_id=f"O{i}_{tipo}",
                    texto="RespuestaPrivada", tipo=tipo, consecuencia_narrativa="ConsecuenciaPrivada")

    def registrar(self, seguras, total, tema="desconocidos", jugador=None, fecha=None, tipo_segura="segura_basica"):
        partida = Partida.objects.create(usuario_jugador=jugador or self.nina)
        npc = NPC.objects.create(partida=partida, nombre="NPC privado", area=tema, tipo="neutral")
        chat = Chat.objects.create(partida=partida, npc=npc)
        for i in range(total):
            Mensaje.objects.create(chat=chat, tipo="chain", respuesta="ConversacionPrivada",
                opcion_banco_id=self.opciones[tema, tipo_segura if i < seguras else "insegura"].opcion_id)
        if fecha:
            chat.mensajes.update(timestamp=fecha)
        return chat

    def consultar(self):
        r = self.client.get(self.url)
        self.assertEqual(r.status_code, 200, r.data)
        self.assertEqual(r["Cache-Control"], "no-store")
        return r.data

    def test_cero_y_menos_de_cinco_no_se_clasifican_como_bajo_rendimiento(self):
        a = self.consultar()["alumnos"][0]
        self.assertEqual(a["estado"], "sin_datos")
        self.assertIsNone(a["general"])
        self.assertTrue(all(t["estado"] == "sin_datos" for t in a["tematicas"]))
        self.registrar(0, 4)
        a = self.consultar()["alumnos"][0]
        self.assertEqual(a["estado"], "muestra_insuficiente")
        self.assertEqual(a["tematicas"][0]["estado"], "muestra_insuficiente")

    def test_cero_seguras_con_muestra_suficiente_es_prioritario(self):
        self.registrar(0, 5)
        a = self.consultar()["alumnos"][0]
        self.assertEqual(a["estado"], "prioritario")
        self.assertEqual(a["tematicas"][0]["estado"], "prioritario")

    def test_fronteras_40_y_60_y_seguras_optimas(self):
        self.registrar(2, 5)
        self.registrar(3, 5, "ciberacoso", tipo_segura="segura_optima")
        a = self.consultar()["alumnos"][0]
        self.assertEqual(a["estado"], "apoyo")
        self.assertEqual(a["tematicas"][0]["estado"], "apoyo")
        self.assertEqual(a["tematicas"][1]["estado"], "sin_alertas")
        self.assertEqual(a["general"], {"estado": "apoyo"})

    def test_una_tematica_baja_no_se_oculta_por_un_promedio_alto(self):
        self.registrar(0, 5)
        self.registrar(20, 20, "ciberacoso")
        self.registrar(20, 20, "retos_virales")
        a = self.consultar()["alumnos"][0]
        self.assertEqual(a["estado"], "prioritario")
        self.assertEqual(a["general"]["estado"], "sin_alertas")
        self.assertEqual(a["tematicas"][2]["tematica"], "retos_virales")
        self.assertEqual(a["tematicas"][2]["estado"], "sin_alertas")

    def test_general_no_incorpora_temas_con_pocos_datos(self):
        self.registrar(0, 20)
        self.registrar(4, 4, "ciberacoso")
        a = self.consultar()["alumnos"][0]
        self.assertIsNone(a["general"])
        self.assertEqual(a["tematicas"][1]["estado"], "muestra_insuficiente")

    def test_ventana_ultimas_20_por_tema_y_mejora_retira_alerta(self):
        self.registrar(0, 25, fecha=timezone.now() - timedelta(days=3))
        self.assertEqual(self.consultar()["alumnos"][0]["estado"], "prioritario")
        self.registrar(20, 20)
        self.registrar(5, 5, "ciberacoso")
        a = self.consultar()["alumnos"][0]
        self.assertEqual(a["estado"], "sin_alertas")
        # Sin la ventana, 20 seguras de 45 serían "apoyo".
        self.assertEqual(a["tematicas"][0]["estado"], "sin_alertas")
        self.assertEqual(a["tematicas"][1]["estado"], "sin_alertas")

    def test_empates_de_fecha_tienen_orden_determinista(self):
        fecha = timezone.now() - timedelta(hours=1)
        self.registrar(0, 20, fecha=fecha)
        self.registrar(20, 20, fecha=fecha)
        self.assertEqual(self.consultar()["alumnos"][0]["tematicas"][0]["estado"], "sin_alertas")

    def test_senala_datos_antiguos_incluso_si_hay_una_respuesta_nueva(self):
        self.registrar(0, 5, fecha=timezone.now() - timedelta(days=31))
        self.registrar(1, 1)
        a = self.consultar()["alumnos"][0]
        self.assertTrue(a["tematicas"][0]["datos_antiguos"])
        self.assertFalse(a["tematicas"][1]["datos_antiguos"])
        self.registrar(20, 20)
        self.assertFalse(self.consultar()["alumnos"][0]["tematicas"][0]["datos_antiguos"])

    def test_ignora_futuro_opciones_desconocidas_y_texto_sin_clasificacion(self):
        self.registrar(0, 10, fecha=timezone.now() + timedelta(days=1))
        chat = self.registrar(0, 0)
        for opcion in (None, "", "NO_EXISTE"):
            Mensaje.objects.create(chat=chat, tipo="chain", respuesta="ConversacionPrivada", opcion_banco_id=opcion)
        self.assertEqual(self.consultar()["alumnos"][0]["estado"], "sin_datos")
        self.registrar(3, 5)  # Con las 10 del futuro serían 3 de 15: prioritario.
        self.assertEqual(self.consultar()["alumnos"][0]["tematicas"][0]["estado"], "sin_alertas")

    def test_solo_el_nino_vinculado_nunca_hermanos_o_invitaciones_pendientes(self):
        self.registrar(5, 5)
        self.registrar(0, 20, jugador=self.hermano)
        InvitacionGrupo.objects.create(grupo=self.grupo, nombre_nino="Tomás", nombre_clave="tomas", email=self.padre.email,
            token_hash="a" * 64, ultimo_intento=timezone.now(), vence_en=timezone.now() + timedelta(days=7))
        datos = self.consultar()
        self.assertEqual(len(datos["alumnos"]), 1)
        self.assertEqual(datos["alumnos"][0]["estado"], "sin_alertas")
        self.assertNotIn("Tomás", str(datos))
        self.miembro.delete()
        self.assertEqual(self.consultar()["alumnos"], [])

    def test_ventanas_y_prioridad_separadas_por_alumno(self):
        MiembroGrupo.objects.create(grupo=self.grupo, jugador=self.hermano, nombre_invitado="Tomás")
        self.registrar(20, 20)
        self.registrar(0, 20, jugador=self.hermano)
        with self.assertNumQueries(6):  # Grupo, vínculos, banco y tres consultas de ventana; sin N+1.
            alumnos = self.consultar()["alumnos"]
        self.assertEqual([a["nombre_nino"] for a in alumnos], ["Tomás", "Martina"])
        self.assertEqual([a["tematicas"][0]["estado"] for a in alumnos], ["prioritario", "sin_alertas"])

    def test_el_profesor_recibe_la_alerta_no_los_resultados(self):
        # Es lo que dicen la invitación y el aviso a la familia: ni porcentajes ni conteos.
        self.registrar(1, 5)
        self.registrar(4, 5, "ciberacoso")
        self.registrar(5, 5, "retos_virales")
        alumno = self.consultar()["alumnos"][0]
        self.assertEqual(alumno["estado"], "prioritario")
        self.assertEqual(set(alumno["general"]), {"estado"})
        for t in alumno["tematicas"]:
            self.assertEqual(set(t), {"tematica", "estado", "actualizado_en", "datos_antiguos"})
        for texto in ("porcentaje", "decisiones", "seguras"):
            self.assertNotIn(texto, str(alumno))

    def test_acceso_exclusivo_profesor_propietario_y_revocacion(self):
        for usuario, codigo in ((self.padre, 403), (self.otro, 404), (None, 401)):
            self.client.force_authenticate(usuario)
            r = self.client.get(self.url)
            self.assertEqual(r.status_code, codigo)
            self.assertNotIn("Martina", str(r.data))
        self.client.force_authenticate(self.profe)
        self.consultar()
        self.grupo.delete()
        self.assertEqual(self.client.get(self.url).status_code, 404)

    def test_seguimiento_no_expone_textos_ni_identificadores_infantiles_y_pdf_sigue_agregado(self):
        self.registrar(0, 5)
        datos = self.consultar()
        self.assertEqual(datos["alumnos"][0]["email_familia"], "familia@example.com")
        for texto in ("ConversacionPrivada", "RespuestaPrivada", "ConsecuenciaPrivada", "NPC privado", "jugador_id", "chat_id", "password"):
            self.assertNotIn(texto, str(datos))
        reporte = self.client.get(f"/api/grupos/{self.grupo.pk}/reporte/").data
        for texto in ("Martina", "familia@example.com", "alumnos", "prioritario"):
            self.assertNotIn(texto, str(reporte))

    def test_criterios_configurables_y_configuracion_invalida_falla(self):
        self.registrar(3, 5)
        with override_settings(FISHY_SEGUIMIENTO={**settings.FISHY_SEGUIMIENTO, "umbral_apoyo": 70, "umbral_prioridad": 50}):
            datos = self.consultar()
            self.assertEqual(datos["criterios"]["umbral_apoyo"], 70)
            self.assertEqual(datos["alumnos"][0]["estado"], "apoyo")
        with override_settings(FISHY_SEGUIMIENTO={**settings.FISHY_SEGUIMIENTO, "ventana_decisiones": 3}):
            with self.assertRaises(ImproperlyConfigured):
                criterios_seguimiento()
