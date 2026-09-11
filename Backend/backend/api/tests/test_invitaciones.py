import hashlib
import re
from datetime import timedelta
from unittest.mock import patch

from django.core import mail
from django.core.cache import cache
from django.db import IntegrityError, transaction
from django.test import TestCase, override_settings
from django.utils import timezone
from rest_framework.test import APIClient

from api.models import (AdultoResponsable, GrupoTutor, InvitacionGrupo, MiembroGrupo, UsuarioJugador,
                        Partida, NPC, Chat, Mensaje, PreguntaBanco, OpcionBanco)


@override_settings(EMAIL_BACKEND="django.core.mail.backends.locmem.EmailBackend", FISHY_EMAIL_ENABLED=True,
                   DEFAULT_FROM_EMAIL="Fishy <invitaciones@example.com>", FISHY_WEB_URL="https://fishy.example.com",
                   PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class InvitacionesTests(TestCase):
    def setUp(self):
        cache.clear()
        self.profesor = AdultoResponsable.objects.create_user("profesor", "profe@example.com", "clave", is_admin=True)
        self.otro_profe = AdultoResponsable.objects.create_user("otroprofe", "otroprofe@example.com", "clave", is_admin=True)
        self.padre = AdultoResponsable.objects.create_user("padre", "familia@example.com", "clave")
        self.otra_familia = AdultoResponsable.objects.create_user("otro", "otro@example.com", "clave")
        self.nina = UsuarioJugador.objects.create(adulto=self.padre, nombre="Martina")
        self.hermano = UsuarioJugador.objects.create(adulto=self.padre, nombre="Tomás")
        self.ajeno = UsuarioJugador.objects.create(adulto=self.otra_familia, nombre="Martina")
        self.grupo = GrupoTutor.objects.create(tutor=self.profesor, nombre="5° Básico A")
        self.base = f"/api/grupos/{self.grupo.pk}/"
        self.client = APIClient()
        self.client.force_authenticate(self.profesor)

    def invitar(self, nombre="Martina", email="familia@example.com"):
        self.client.force_authenticate(self.profesor)
        respuesta = self.client.post(self.base + "invitaciones/", {"email": email, "nombre_nino": nombre}, format="json")
        self.assertEqual(respuesta.status_code, 201, respuesta.data)
        token = re.search(r"/invitacion#([A-Za-z0-9_-]{43})", mail.outbox[-1].body)[1]
        return InvitacionGrupo.objects.get(pk=respuesta.data["id"]), token

    def aceptar(self, token, **datos):
        self.client.force_authenticate(self.padre)
        return self.client.post("/api/invitaciones/aceptar/", {"token": token, "confirmar": True, **datos}, format="json")

    def test_correo_contiene_solo_el_nino_invitado_y_token_se_guarda_hasheado(self):
        inv, token = self.invitar(email="FAMILIA@example.com")
        correo = mail.outbox[0]
        self.assertEqual(correo.to, ["familia@example.com"])
        self.assertIn("Martina", correo.body)
        self.assertIn("5° Básico A", correo.body)
        self.assertNotIn("Tomás", correo.body)
        self.assertEqual(len(correo.alternatives), 1)
        self.assertEqual(inv.token_hash, hashlib.sha256(token.encode()).hexdigest())
        self.assertNotIn(token, str(self.client.get(self.base).data))
        self.assertEqual(inv.estado_envio, "enviado")
        self.assertEqual(MiembroGrupo.objects.count(), 0)

    def test_acepta_un_perfil_existente_sin_incorporar_al_hermano_ni_perder_progreso(self):
        partida = Partida.objects.create(usuario_jugador=self.nina, progreso=35)
        inv, token = self.invitar()
        respuesta = self.aceptar(token, jugador_id=self.nina.pk)
        self.assertEqual(respuesta.status_code, 200, respuesta.data)
        self.assertEqual(self.grupo.miembros.get().jugador_id, self.nina.pk)
        self.assertEqual(Partida.objects.get(pk=partida.pk).progreso, 35)
        self.assertEqual(self.padre.jugadores.count(), 2)
        inv.refresh_from_db()
        self.assertEqual(inv.estado, "aceptada")
        self.assertEqual(self.aceptar(token, jugador_id=self.nina.pk).status_code, 200)
        self.assertEqual(MiembroGrupo.objects.count(), 1)

    def test_cuenta_nueva_se_registra_inicia_sesion_y_crea_solo_el_nino_invitado(self):
        inv, token = self.invitar("Lucía", "nueva@example.com")
        self.client.force_authenticate(None)
        respuesta = self.client.post("/api/auth/registro/", {"nombre": "familia_nueva", "email": "NUEVA@example.com", "password": "clave-larga", "is_admin": True}, format="json")
        self.assertEqual(respuesta.status_code, 201)
        nueva = AdultoResponsable.objects.get(pk=respuesta.data["adulto_id"])
        self.assertFalse(nueva.is_admin)
        self.client.credentials(HTTP_AUTHORIZATION="Token " + respuesta.data["token"])
        aceptada = self.client.post("/api/invitaciones/aceptar/", {"token": token, "confirmar": True, "crear_perfil": True}, format="json")
        self.assertEqual(aceptada.status_code, 200, aceptada.data)
        self.assertEqual(list(nueva.jugadores.values_list("nombre", flat=True)), ["Lucía"])
        self.assertEqual(self.grupo.miembros.get().jugador.adulto_id, nueva.pk)

    def test_un_padre_puede_recibir_invitaciones_separadas_para_sus_dos_hijos(self):
        _, primera = self.invitar()
        _, segunda = self.invitar("Tomás")
        self.assertEqual(self.aceptar(primera, jugador_id=self.nina.pk).status_code, 200)
        self.assertEqual(self.aceptar(segunda, jugador_id=self.hermano.pk).status_code, 200)
        self.assertEqual(self.grupo.miembros.count(), 2)

    def test_nombre_no_asigna_automaticamente_y_no_permite_otro_hermano_o_nino_ajeno(self):
        _, token = self.invitar()
        self.assertEqual(self.aceptar(token, jugador_id=self.hermano.pk).status_code, 400)
        self.assertEqual(self.aceptar(token, jugador_id=self.ajeno.pk).status_code, 404)
        self.assertEqual(self.aceptar(token, crear_perfil=True).status_code, 409)
        self.assertEqual(self.aceptar(token).status_code, 400)
        self.assertEqual(self.grupo.miembros.count(), 0)
        self.assertEqual(self.padre.jugadores.count(), 2)

    def test_token_no_autoriza_cuentas_ajenas_ni_profesores(self):
        _, token = self.invitar()
        for cuenta in [None, self.otra_familia, self.profesor]:
            self.client.force_authenticate(cuenta)
            respuesta = self.client.post("/api/invitaciones/aceptar/", {"token": token, "confirmar": True, "crear_perfil": True}, format="json")
            self.assertIn(respuesta.status_code, (401, 403))
        self.assertEqual(self.grupo.miembros.count(), 0)

    def test_duplicados_de_invitacion_y_miembros_se_impiden(self):
        _, token = self.invitar()
        duplicada = self.client.post(self.base + "invitaciones/", {"email": "FAMILIA@example.com", "nombre_nino": "  martina  "}, format="json")
        self.assertEqual(duplicada.status_code, 409)
        self.aceptar(token, jugador_id=self.nina.pk)
        self.client.force_authenticate(self.profesor)
        self.assertEqual(self.client.post(self.base + "invitaciones/", {"email": "familia@example.com", "nombre_nino": "Martina"}, format="json").status_code, 409)
        with self.assertRaises(IntegrityError), transaction.atomic():
            MiembroGrupo.objects.create(grupo=self.grupo, jugador=self.nina, nombre_invitado="Martina")

    def test_cancelacion_y_vencimiento_impiden_aceptar(self):
        inv, token = self.invitar()
        self.assertEqual(self.client.delete(self.base + f"invitaciones/{inv.pk}/").status_code, 204)
        self.assertEqual(self.aceptar(token, jugador_id=self.nina.pk).status_code, 409)
        otra, segundo = self.invitar()
        InvitacionGrupo.objects.filter(pk=otra.pk).update(vence_en=timezone.now() - timedelta(seconds=1))
        self.assertEqual(self.aceptar(segundo, jugador_id=self.nina.pk).status_code, 409)
        self.assertEqual(self.grupo.miembros.count(), 0)

    def test_reenvio_rota_el_secreto_y_respeta_espera(self):
        inv, viejo = self.invitar()
        ruta = self.base + f"invitaciones/{inv.pk}/"
        self.assertEqual(self.client.post(ruta).status_code, 409)
        InvitacionGrupo.objects.filter(pk=inv.pk).update(ultimo_intento=timezone.now() - timedelta(minutes=2))
        self.assertEqual(self.client.post(ruta).status_code, 201)
        nuevo = re.search(r"/invitacion#([A-Za-z0-9_-]{43})", mail.outbox[-1].body)[1]
        self.assertNotEqual(viejo, nuevo)
        self.assertEqual(self.aceptar(viejo, jugador_id=self.nina.pk).status_code, 404)
        self.assertEqual(self.aceptar(nuevo, jugador_id=self.nina.pk).status_code, 200)

    def test_fallo_smtp_no_confirma_envio_y_se_puede_reintentar(self):
        with patch("api.invitaciones.EmailMultiAlternatives.send", side_effect=OSError("proveedor")):
            respuesta = self.client.post(self.base + "invitaciones/", {"email": self.padre.email, "nombre_nino": "Martina"}, format="json")
        self.assertEqual(respuesta.status_code, 503)
        inv = InvitacionGrupo.objects.get()
        self.assertEqual(inv.estado_envio, "fallido")
        self.assertIsNone(inv.enviada_en)
        InvitacionGrupo.objects.filter(pk=inv.pk).update(ultimo_intento=timezone.now() - timedelta(minutes=2))
        self.assertEqual(self.client.post(self.base + f"invitaciones/{inv.pk}/").status_code, 201)
        inv.refresh_from_db()
        self.assertEqual(inv.estado_envio, "enviado")

    @override_settings(FISHY_EMAIL_ENABLED=False)
    def test_sin_proveedor_no_finge_un_envio(self):
        respuesta = self.client.post(self.base + "invitaciones/", {"email": self.padre.email, "nombre_nino": "Martina"}, format="json")
        self.assertEqual(respuesta.status_code, 503)
        self.assertEqual(InvitacionGrupo.objects.count(), 0)

    def test_lectura_del_enlace_no_lo_consume_y_no_expone_secretos_en_listados(self):
        _, token = self.invitar()
        self.client.force_authenticate(None)
        for _ in range(2):
            respuesta = self.client.post("/api/invitaciones/consultar/", {"token": token}, format="json")
            self.assertEqual(respuesta.status_code, 200)
            self.assertEqual(respuesta["Cache-Control"], "no-store")
            self.assertNotIn("token", respuesta.data)
        self.assertEqual(InvitacionGrupo.objects.get().estado, "pendiente")

    def test_otro_profesor_no_puede_gestionar_el_grupo_ni_sus_invitaciones(self):
        inv, _ = self.invitar()
        self.client.force_authenticate(self.otro_profe)
        self.assertEqual(self.client.get(self.base).status_code, 404)
        self.assertEqual(self.client.delete(self.base + f"invitaciones/{inv.pk}/").status_code, 404)
        self.assertEqual(self.client.get(self.base + "reporte/").status_code, 404)
        self.client.force_authenticate(self.padre)
        self.assertEqual(self.client.get("/api/grupos/").status_code, 403)

    def test_quitar_miembro_no_borra_perfil_y_el_enlace_usado_no_lo_reincorpora(self):
        _, token = self.invitar()
        self.aceptar(token, jugador_id=self.nina.pk)
        miembro = self.grupo.miembros.get()
        self.client.force_authenticate(self.profesor)
        self.assertEqual(self.client.delete(self.base + f"miembros/{miembro.pk}/").status_code, 204)
        self.assertTrue(UsuarioJugador.objects.filter(pk=self.nina.pk).exists())
        self.assertEqual(self.aceptar(token, jugador_id=self.nina.pk).status_code, 409)

    def test_borrar_grupo_invalida_enlaces_sin_borrar_perfiles(self):
        _, token = self.invitar()
        self.assertEqual(self.client.delete(self.base).status_code, 204)
        self.assertEqual(self.aceptar(token, jugador_id=self.nina.pk).status_code, 404)
        self.assertTrue(UsuarioJugador.objects.filter(pk=self.nina.pk).exists())

    def test_rollback_no_deja_perfil_huerfano_si_falla_la_vinculacion(self):
        _, token = self.invitar("Lucía")
        with patch("api.invitaciones.MiembroGrupo.objects.get_or_create", side_effect=IntegrityError("fallo")):
            with self.assertRaises(IntegrityError):
                self.aceptar(token, crear_perfil=True)
        self.assertFalse(self.padre.jugadores.filter(nombre="Lucía").exists())
        self.assertEqual(InvitacionGrupo.objects.get().estado, "pendiente")

    def test_reporte_grupal_usa_solo_ninos_aceptados_y_no_hermanos(self):
        pregunta = PreguntaBanco.objects.create(pregunta_id="P1", hdu="HDU-2", zona="desconocidos", categoria="desconocidos", mensaje_npc="Privado")
        segura = OpcionBanco.objects.create(pregunta=pregunta, opcion_id="S1", texto="Privado", tipo="segura_basica", consecuencia_narrativa="Privado")
        insegura = OpcionBanco.objects.create(pregunta=pregunta, opcion_id="I1", texto="Privado", tipo="insegura", consecuencia_narrativa="Privado")
        def resultado(nino, opcion):
            partida = Partida.objects.create(usuario_jugador=nino)
            npc = NPC.objects.create(partida=partida, nombre="NPC", area="zona", tipo="neutral")
            chat = Chat.objects.create(partida=partida, npc=npc)
            return Mensaje.objects.create(chat=chat, tipo="chain", respuesta="ConversacionPrivada", opcion_banco_id=opcion.opcion_id)
        otros = [UsuarioJugador.objects.create(adulto=self.otra_familia, nombre=n) for n in ["Pedro", "Ana"]]
        for nino in [self.nina, *otros]:
            MiembroGrupo.objects.create(grupo=self.grupo, jugador=nino, nombre_invitado=nino.nombre)
            resultado(nino, segura)
        for _ in range(10):
            resultado(self.hermano, insegura)
        respuesta = self.client.get(self.base + "reporte/")
        self.assertEqual(respuesta.status_code, 200)
        self.assertEqual(respuesta.data["tematicas"][0]["metricas"]["decisiones_evaluadas"], 3)
        self.assertEqual(respuesta.data["tematicas"][0]["metricas"]["decisiones_seguras"], 3)
        for privado in ["Martina", "Tomás", "Pedro", "Ana", "familia@example.com", "ConversacionPrivada", "jugador_id"]:
            self.assertNotIn(privado, str(respuesta.data))
        self.assertIsNone(respuesta.data["tematicas"][1]["metricas"])
        resultado(self.nina, insegura)
        actualizado = self.client.get(self.base + "reporte/")
        self.assertEqual(actualizado.data["tematicas"][0]["metricas"]["decisiones_evaluadas"], 4)
        self.grupo.miembros.filter(jugador=otros[0]).delete()
        insuficiente = self.client.get(self.base + "reporte/")
        self.assertIsNone(insuficiente.data["tematicas"][0]["metricas"])

    def test_rol_en_perfil_y_proteccion_de_reportes_individuales(self):
        self.assertTrue(self.client.get("/api/auth/perfil/").data["is_admin"])
        self.assertEqual(self.client.get(f"/api/jugadores/{self.nina.pk}/reporte/").status_code, 403)
        self.client.force_authenticate(self.padre)
        self.assertFalse(self.client.get("/api/auth/perfil/").data["is_admin"])
        self.assertEqual(self.client.get(f"/api/jugadores/{self.ajeno.pk}/reporte/").status_code, 404)
        self.assertEqual(self.client.get(f"/api/jugadores/{self.nina.pk}/reporte/").status_code, 200)
