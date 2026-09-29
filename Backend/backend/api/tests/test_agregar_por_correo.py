"""HDU17 CA4/CA5: el profesor agrega niños a su curso por el correo del apoderado, sin invitación."""
import re
from unittest.mock import patch

from django.core import mail
from django.core.cache import cache
from django.db import IntegrityError, transaction
from django.test import TestCase, override_settings
from rest_framework.test import APIClient

from api.invitaciones import LimiteBusquedaFamilias
from api.models import AdultoResponsable, GrupoTutor, MiembroGrupo, UsuarioJugador

SIN_PERFILES = "No encontramos perfiles de niño para ese correo."


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class AgregarPorCorreoTests(TestCase):
    def setUp(self):
        cache.clear()
        self.profesor = AdultoResponsable.objects.create_user("profesor", "profe@example.com", "clave", rol="profesor")
        self.otro_profe = AdultoResponsable.objects.create_user("otroprofe", "otroprofe@example.com", "clave", rol="profesor")
        self.padre = AdultoResponsable.objects.create_user("padre", "familia@example.com", "clave")
        self.sin_hijos = AdultoResponsable.objects.create_user("sinhijos", "sinhijos@example.com", "clave")
        self.otra_familia = AdultoResponsable.objects.create_user("otro", "otro@example.com", "clave")
        self.nina = UsuarioJugador.objects.create(adulto=self.padre, nombre="Martina")
        self.hermano = UsuarioJugador.objects.create(adulto=self.padre, nombre="Tomás")
        self.ajeno = UsuarioJugador.objects.create(adulto=self.otra_familia, nombre="Pedro")
        # Un profesor con perfiles de niño (datos viejos) no debe aparecer como familia.
        UsuarioJugador.objects.create(adulto=self.otro_profe, nombre="Perfil de profe")
        self.grupo = GrupoTutor.objects.create(tutor=self.profesor, nombre="5° Básico A")
        self.otro_grupo = GrupoTutor.objects.create(tutor=self.otro_profe, nombre="6° Básico B")
        self.base = f"/api/grupos/{self.grupo.pk}/"
        self.client = APIClient()
        self.client.force_authenticate(self.profesor)

    def buscar(self, email="familia@example.com", grupo=None):
        return self.client.post(f"/api/grupos/{(grupo or self.grupo).pk}/buscar-familia/", {"email": email}, format="json")

    def agregar(self, ids, email="familia@example.com", grupo=None):
        return self.client.post(f"/api/grupos/{(grupo or self.grupo).pk}/miembros/", {"email": email, "jugador_ids": ids}, format="json")

    def test_buscar_muestra_solo_nombre_y_estado_de_cada_hijo(self):
        respuesta = self.buscar("FAMILIA@example.com")
        self.assertEqual(respuesta.status_code, 200, respuesta.data)
        self.assertEqual(respuesta.data["perfiles"], [
            {"jugador_id": self.nina.pk, "nombre": "Martina", "estado": "disponible"},
            {"jugador_id": self.hermano.pk, "nombre": "Tomás", "estado": "disponible"},
        ])
        self.assertEqual(respuesta["Cache-Control"], "no-store")
        self.assertEqual(MiembroGrupo.objects.count(), 0)

    def test_sin_cuenta_sin_hijos_o_de_profesor_dan_el_mismo_mensaje(self):
        for email in ["nadie@example.com", "sinhijos@example.com", "otroprofe@example.com"]:
            respuesta = self.buscar(email)
            self.assertEqual(respuesta.status_code, 404, email)
            self.assertEqual(respuesta.data["detail"], SIN_PERFILES)

    def test_agrega_de_inmediato_solo_los_elegidos_y_sin_correos(self):
        respuesta = self.agregar([self.nina.pk])
        self.assertEqual(respuesta.status_code, 201, respuesta.data)
        self.assertEqual([m["nombre_nino"] for m in respuesta.data["miembros"]], ["Martina"])
        self.assertEqual(list(self.grupo.miembros.values_list("jugador_id", flat=True)), [self.nina.pk])
        self.assertEqual(len(mail.outbox), 0)
        estados = {p["nombre"]: p["estado"] for p in self.buscar().data["perfiles"]}
        self.assertEqual(estados, {"Martina": "en_este_curso", "Tomás": "disponible"})

    def test_hermanos_en_cursos_distintos(self):
        self.assertEqual(self.agregar([self.nina.pk]).status_code, 201)
        self.client.force_authenticate(self.otro_profe)
        self.assertEqual(self.agregar([self.hermano.pk], grupo=self.otro_grupo).status_code, 201)
        self.assertEqual(self.nina.grupos.get().grupo, self.grupo)
        self.assertEqual(self.hermano.grupos.get().grupo, self.otro_grupo)

    def test_repetir_no_duplica_y_agrega_los_nuevos(self):
        self.agregar([self.nina.pk])
        self.assertEqual(self.agregar([self.nina.pk]).status_code, 200)
        self.assertEqual(self.agregar([self.nina.pk, self.hermano.pk]).status_code, 201)
        self.assertEqual(self.grupo.miembros.count(), 2)

    def test_nino_en_otro_curso_no_se_agrega_ni_se_revela_el_curso(self):
        self.agregar([self.nina.pk])
        self.client.force_authenticate(self.otro_profe)
        busqueda = self.buscar(grupo=self.otro_grupo)
        self.assertEqual({p["nombre"]: p["estado"] for p in busqueda.data["perfiles"]},
                         {"Martina": "en_otro_curso", "Tomás": "disponible"})
        self.assertNotIn("5° Básico A", str(busqueda.data))
        respuesta = self.agregar([self.nina.pk, self.hermano.pk], grupo=self.otro_grupo)
        self.assertEqual(respuesta.status_code, 409)
        self.assertIn("Martina ya está en otro curso", respuesta.data["detail"])
        self.assertNotIn("5° Básico A", respuesta.data["detail"])
        # Todo o nada: el hermano tampoco entró.
        self.assertEqual(self.otro_grupo.miembros.count(), 0)

    def test_ids_que_no_son_de_ese_correo_no_se_agregan(self):
        for ids, email in [([self.ajeno.pk], "familia@example.com"), ([self.nina.pk, self.ajeno.pk], "familia@example.com"),
                           ([self.nina.pk], "otro@example.com"), ([999999], "familia@example.com")]:
            respuesta = self.agregar(ids, email)
            self.assertEqual(respuesta.status_code, 404, (ids, email))
            self.assertEqual(respuesta.data["detail"], SIN_PERFILES)
        self.assertEqual(MiembroGrupo.objects.count(), 0)

    def test_datos_invalidos(self):
        for datos in [{"email": "familia@example.com", "jugador_ids": []}, {"email": "no-es-correo", "jugador_ids": [1]},
                      {"email": "familia@example.com"}, {"email": "familia@example.com", "jugador_ids": list(range(1, 22))}]:
            self.assertEqual(self.client.post(self.base + "miembros/", datos, format="json").status_code, 400, datos)
        self.assertEqual(self.client.post(self.base + "buscar-familia/", {}, format="json").status_code, 400)

    def test_solo_el_profesor_dueno_busca_y_agrega(self):
        admin = AdultoResponsable.objects.create_user("adminportal", "admin@example.com", "clave", rol="admin")
        for cuenta, esperado in [(None, 401), (self.padre, 403), (admin, 403), (self.otro_profe, 404)]:
            self.client.force_authenticate(cuenta)
            self.assertEqual(self.buscar().status_code, esperado, cuenta)
            self.assertEqual(self.agregar([self.nina.pk]).status_code, esperado, cuenta)
        self.assertEqual(MiembroGrupo.objects.count(), 0)

    def test_la_base_impide_un_nino_en_dos_cursos(self):
        MiembroGrupo.objects.create(grupo=self.grupo, jugador=self.nina, nombre_invitado="Martina")
        with self.assertRaises(IntegrityError), transaction.atomic():
            MiembroGrupo.objects.create(grupo=self.otro_grupo, jugador=self.nina, nombre_invitado="Martina")

    def test_busqueda_tiene_limite(self):
        with patch.object(LimiteBusquedaFamilias, "rate", "2/hour"):
            self.assertEqual(self.buscar().status_code, 200)
            self.assertEqual(self.buscar("nadie@example.com").status_code, 404)
            self.assertEqual(self.buscar().status_code, 429)


@override_settings(EMAIL_BACKEND="django.core.mail.backends.locmem.EmailBackend", FISHY_EMAIL_ENABLED=True,
                   DEFAULT_FROM_EMAIL="Fishy <invitaciones@example.com>", FISHY_WEB_URL="https://fishy.example.com",
                   PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class InvitacionRespetaUnCursoTests(TestCase):
    def test_aceptar_invitacion_de_un_nino_que_ya_esta_en_otro_curso_se_rechaza(self):
        cache.clear()
        profesor = AdultoResponsable.objects.create_user("profesor", "profe@example.com", "clave", rol="profesor")
        otro = AdultoResponsable.objects.create_user("otroprofe", "otroprofe@example.com", "clave", rol="profesor")
        padre = AdultoResponsable.objects.create_user("padre", "familia@example.com", "clave")
        nina = UsuarioJugador.objects.create(adulto=padre, nombre="Martina")
        grupo = GrupoTutor.objects.create(tutor=profesor, nombre="5° Básico A")
        MiembroGrupo.objects.create(grupo=GrupoTutor.objects.create(tutor=otro, nombre="6° B"), jugador=nina, nombre_invitado="Martina")
        client = APIClient()
        client.force_authenticate(profesor)
        self.assertEqual(client.post(f"/api/grupos/{grupo.pk}/invitaciones/", {"email": "familia@example.com", "nombre_nino": "Martina"}, format="json").status_code, 201)
        token = re.search(r"/invitacion#([A-Za-z0-9_-]{43})", mail.outbox[-1].body)[1]
        client.force_authenticate(padre)
        respuesta = client.post("/api/invitaciones/aceptar/", {"token": token, "confirmar": True, "jugador_id": nina.pk}, format="json")
        self.assertEqual(respuesta.status_code, 409)
        self.assertIn("ya está en otro curso", respuesta.data["detail"])
        self.assertEqual(grupo.miembros.count(), 0)
