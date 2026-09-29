"""Separación entre el rol del portal (`rol`) y el privilegio técnico (`is_admin`)."""
from django.db import connection
from django.test import TestCase, override_settings
from django.utils import timezone
from rest_framework.test import APIClient

from api.models import AdultoResponsable, Partida, UsuarioJugador


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class RolesPortalTests(TestCase):
    def setUp(self):
        self.client = APIClient()
        self.profesor = AdultoResponsable.objects.create_user(
            nombre="Profesora", email="profe@example.com", password="prueba",
            rol=AdultoResponsable.ROL_PROFESOR,
        )
        self.padre = AdultoResponsable.objects.create_user(nombre="Familia", email="familia@example.com", password="prueba")
        self.nino = UsuarioJugador.objects.create(adulto=self.padre, nombre="Martina")

    def test_una_cuenta_nueva_es_padre_y_no_administrador(self):
        self.assertEqual(self.padre.rol, "padre")
        self.assertFalse(self.padre.es_profesor)
        self.assertFalse(self.padre.is_staff)

    def test_registro_ignora_rol_e_is_admin_enviados_por_el_cliente(self):
        respuesta = self.client.post("/api/auth/registro/", {
            "nombre": "Intruso", "email": "intruso@example.com", "password": "prueba",
            "rol": "profesor", "is_admin": True,
        }, format="json")
        self.assertEqual(respuesta.status_code, 201)
        cuenta = AdultoResponsable.objects.get(nombre="Intruso")
        self.assertEqual(cuenta.rol, "padre")
        self.assertFalse(cuenta.is_admin)

    def test_perfil_expone_rol_y_no_el_privilegio_tecnico(self):
        self.client.force_authenticate(self.profesor)
        datos = self.client.get("/api/auth/perfil/").data
        self.assertEqual(datos["rol"], "profesor")
        self.assertNotIn("is_admin", datos)

    def test_profesor_no_gestiona_perfiles_de_menores_por_la_api(self):
        propio = UsuarioJugador.objects.create(adulto=self.profesor, nombre="Perfil antiguo")
        Partida.objects.create(usuario_jugador=propio)
        self.client.force_authenticate(self.profesor)
        llamadas = [
            ("get", "/api/jugadores/", None),
            ("post", "/api/jugadores/", {"nombre": "Nuevo"}),
            ("get", f"/api/jugadores/{propio.pk}/", None),
            ("patch", f"/api/jugadores/{propio.pk}/", {"nombre": "Otro"}),
            ("delete", f"/api/jugadores/{propio.pk}/", None),
            ("get", f"/api/jugadores/{propio.pk}/partidas/", None),
            ("get", f"/api/jugadores/{self.nino.pk}/", None),
            ("get", f"/api/jugadores/{propio.pk}/reporte/", None),
        ]
        for metodo, ruta, cuerpo in llamadas:
            with self.subTest(metodo=metodo, ruta=ruta):
                self.assertEqual(getattr(self.client, metodo)(ruta, cuerpo, format="json").status_code, 403)
        # Nada se creó, cambió ni borró.
        self.assertEqual(list(UsuarioJugador.objects.filter(adulto=self.profesor).values_list("nombre", flat=True)), ["Perfil antiguo"])

    def test_padre_sigue_gestionando_sus_perfiles(self):
        self.client.force_authenticate(self.padre)
        self.assertEqual(self.client.get("/api/jugadores/").status_code, 200)
        self.assertEqual(self.client.post("/api/jugadores/", {"nombre": "Tomás"}, format="json").status_code, 201)
        self.assertEqual(self.client.get(f"/api/jugadores/{self.nino.pk}/partidas/").status_code, 200)

    def test_ser_profesor_no_da_acceso_al_admin_de_django(self):
        self.assertFalse(self.profesor.is_staff)
        self.assertFalse(self.profesor.has_module_perms("api"))
        self.client.force_login(self.profesor)
        self.assertEqual(self.client.get("/admin/").status_code, 302)

    def test_administrador_tecnico_no_se_vuelve_profesor(self):
        admin = AdultoResponsable.objects.create_superuser(nombre="Equipo", email="equipo@example.com", password="prueba")
        self.assertTrue(admin.is_staff)
        self.assertFalse(admin.es_profesor)
        self.client.force_authenticate(admin)
        self.assertEqual(self.client.get("/api/jugadores/").status_code, 200)

    def test_la_base_pone_padre_si_otro_backend_inserta_sin_la_columna(self):
        # Simula al backend de dev, que no conoce `rol` y lo omite en el INSERT.
        with connection.cursor() as cursor:
            cursor.execute(
                "INSERT INTO api_adultoresponsable (password, nombre, apellido, email, fecha_creacion, is_admin) "
                "VALUES (%s, %s, %s, %s, %s, %s)",
                ["x", "DesdeDev", "", "dev@example.com", timezone.now(), False],
            )
        self.assertEqual(AdultoResponsable.objects.get(nombre="DesdeDev").rol, "padre")

    def test_un_rol_desconocido_en_la_base_no_se_cuela_como_padre(self):
        # Lo que pasó en QA: alguien escribió `rol` a mano en Supabase. Los
        # `choices` no se validan en Postgres, así que la base lo acepta.
        rara = AdultoResponsable.objects.create_user(nombre="Rara", email="rara@example.com", password="prueba")
        AdultoResponsable.objects.filter(pk=rara.pk).update(rol="rector")
        rara.refresh_from_db()
        self.client.force_authenticate(rara)
        self.assertEqual(self.client.get("/api/jugadores/").status_code, 403)
        self.assertEqual(self.client.get("/api/admin/profesores/").status_code, 403)
        self.assertEqual(self.client.get("/api/grupos/").status_code, 403)
