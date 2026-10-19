"""Rol `admin` del portal: lee todos los profesores y grupos, y solo marca profesores."""
from io import StringIO
from unittest.mock import patch

from django.core.management import call_command
from django.core.management.base import CommandError
from django.test import TestCase, override_settings
from rest_framework.test import APIClient

from api.models import AdultoResponsable, GrupoTutor, MiembroGrupo, Partida, UsuarioJugador


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class PanelAdminTests(TestCase):
    def setUp(self):
        crear = AdultoResponsable.objects.create_user
        self.admin = crear("equipo", "equipo@example.com", "clave", rol="admin")
        self.profe = crear("profe", "profe@example.com", "clave", rol="profesor")
        self.otro_profe = crear("otroprofe", "otroprofe@example.com", "clave", rol="profesor")
        self.padre = crear("familia", "familia@example.com", "clave")
        self.nino = UsuarioJugador.objects.create(adulto=self.padre, nombre="Martina")
        self.grupo = GrupoTutor.objects.create(tutor=self.profe, nombre="5° Básico A")
        self.grupo_ajeno = GrupoTutor.objects.create(tutor=self.otro_profe, nombre="6° Básico B")
        self.miembro = MiembroGrupo.objects.create(grupo=self.grupo, jugador=self.nino, nombre_invitado="Martina")
        self.client = APIClient()
        self.client.force_authenticate(self.admin)

    # ── Lectura ──────────────────────────────────────────────────────────────

    def test_lista_todos_los_profesores_con_su_cantidad_de_grupos(self):
        datos = self.client.get("/api/admin/profesores/").data
        self.assertEqual([(p["nombre"], p["total_grupos"]) for p in datos], [("otroprofe", 1), ("profe", 1)])
        self.assertNotIn("password", datos[0])

    def test_lista_todos_los_grupos_con_su_profesor(self):
        datos = self.client.get("/api/admin/grupos/").data
        self.assertEqual({g["nombre"]: g["profesor"]["nombre"] for g in datos},
                         {"5° Básico A": "profe", "6° Básico B": "otroprofe"})
        self.assertEqual({g["nombre"]: g["total_miembros"] for g in datos}, {"5° Básico A": 1, "6° Básico B": 0})

    def test_filtra_los_grupos_por_profesor(self):
        datos = self.client.get(f"/api/admin/grupos/?profesor={self.otro_profe.pk}").data
        self.assertEqual([g["nombre"] for g in datos], ["6° Básico B"])
        self.assertEqual(self.client.get("/api/admin/grupos/?profesor=abc").status_code, 400)

    def test_lee_el_detalle_y_el_reporte_de_un_grupo_ajeno(self):
        detalle = self.client.get(f"/api/grupos/{self.grupo.pk}/")
        self.assertEqual(detalle.status_code, 200)
        self.assertEqual([m["nombre_nino"] for m in detalle.data["miembros"]], ["Martina"])
        self.assertEqual(self.client.get(f"/api/grupos/{self.grupo.pk}/reporte/").status_code, 200)

    def test_no_ve_el_seguimiento_por_nino(self):
        self.assertEqual(self.client.get(f"/api/grupos/{self.grupo.pk}/seguimiento/").status_code, 403)

    # ── Lo que el admin NO puede hacer ───────────────────────────────────────

    def test_no_escribe_en_grupos(self):
        llamadas = [
            ("get", "/api/grupos/", None),      # "mis grupos" es del profesor; el admin usa admin/grupos/
            ("post", "/api/grupos/", {"nombre": "Nuevo"}),
            ("delete", f"/api/grupos/{self.grupo.pk}/", None),
            ("delete", f"/api/grupos/{self.grupo.pk}/miembros/{self.miembro.pk}/", None),
            ("post", f"/api/grupos/{self.grupo.pk}/invitaciones/", {"email": "x@example.com", "nombre_nino": "X"}),
        ]
        for metodo, ruta, cuerpo in llamadas:
            with self.subTest(metodo=metodo, ruta=ruta):
                self.assertEqual(getattr(self.client, metodo)(ruta, cuerpo, format="json").status_code, 403)
        self.assertTrue(GrupoTutor.objects.filter(pk=self.grupo.pk).exists())
        self.assertTrue(MiembroGrupo.objects.filter(pk=self.miembro.pk).exists())
        self.assertEqual(GrupoTutor.objects.count(), 2)

    def test_no_ve_datos_individuales_de_ninos(self):
        propio = UsuarioJugador.objects.create(adulto=self.admin, nombre="Perfil viejo")
        Partida.objects.create(usuario_jugador=propio)
        for ruta in ["/api/jugadores/", f"/api/jugadores/{self.nino.pk}/", f"/api/jugadores/{propio.pk}/",
                     f"/api/jugadores/{propio.pk}/partidas/", f"/api/jugadores/{self.nino.pk}/reporte/"]:
            with self.subTest(ruta=ruta):
                self.assertEqual(self.client.get(ruta).status_code, 403)

    def test_los_demas_roles_no_entran_al_panel(self):
        for cuenta in (self.profe, self.padre):
            self.client.force_authenticate(cuenta)
            for metodo, ruta in [("get", "/api/admin/profesores/"), ("get", "/api/admin/grupos/"),
                                 ("get", "/api/admin/cuentas/?buscar=familia"),
                                 ("patch", f"/api/admin/cuentas/{self.padre.pk}/rol/")]:
                with self.subTest(cuenta=cuenta.nombre, ruta=ruta):
                    self.assertEqual(getattr(self.client, metodo)(ruta, {"rol": "profesor"}, format="json").status_code, 403)
        self.padre.refresh_from_db()
        self.assertEqual(self.padre.rol, "padre")

    def test_sin_sesion_no_entra(self):
        self.client.force_authenticate(None)
        self.assertEqual(self.client.get("/api/admin/profesores/").status_code, 401)

    # ── Buscar y cambiar el rol ──────────────────────────────────────────────

    def test_busca_solo_por_correo_o_nombre_exacto(self):
        por_correo = self.client.get("/api/admin/cuentas/?buscar=FAMILIA@example.com").data
        self.assertEqual([(c["nombre"], c["total_perfiles"]) for c in por_correo], [("familia", 1)])
        self.assertEqual(len(self.client.get("/api/admin/cuentas/?buscar=profe").data), 1)
        self.assertEqual(self.client.get("/api/admin/cuentas/?buscar=fami").data, [])
        self.assertEqual(self.client.get("/api/admin/cuentas/").status_code, 400)

    def test_marca_como_profesor_a_una_cuenta_sin_ninos(self):
        nueva = AdultoResponsable.objects.create_user("nuevaprofe", "nueva@example.com", "clave")
        respuesta = self.client.patch(f"/api/admin/cuentas/{nueva.pk}/rol/", {"rol": "profesor"}, format="json")
        self.assertEqual(respuesta.status_code, 200)
        self.assertEqual(respuesta.data["rol"], "profesor")
        nueva.refresh_from_db()
        self.assertEqual(nueva.rol, "profesor")
        # Repetirlo no falla.
        self.assertEqual(self.client.patch(f"/api/admin/cuentas/{nueva.pk}/rol/", {"rol": "profesor"},
                                           format="json").status_code, 200)

    def test_no_convierte_en_profesor_a_quien_tiene_perfiles_de_ninos(self):
        respuesta = self.client.patch(f"/api/admin/cuentas/{self.padre.pk}/rol/", {"rol": "profesor"}, format="json")
        self.assertEqual(respuesta.status_code, 409)
        self.padre.refresh_from_db()
        self.assertEqual(self.padre.rol, "padre")

    def test_no_devuelve_a_padre_a_un_profesor_con_grupos(self):
        respuesta = self.client.patch(f"/api/admin/cuentas/{self.profe.pk}/rol/", {"rol": "padre"}, format="json")
        self.assertEqual(respuesta.status_code, 409)
        sin_grupos = AdultoResponsable.objects.create_user("exprofe", "ex@example.com", "clave", rol="profesor")
        self.assertEqual(self.client.patch(f"/api/admin/cuentas/{sin_grupos.pk}/rol/", {"rol": "padre"},
                                           format="json").status_code, 200)

    def test_no_asigna_admin_ni_toca_admins_ni_a_si_mismo(self):
        otro_admin = AdultoResponsable.objects.create_user("equipo2", "equipo2@example.com", "clave", rol="admin")
        casos = [(self.padre, "admin", 400), (otro_admin, "padre", 403), (self.admin, "profesor", 403),
                 (self.padre, "rector", 400)]
        for cuenta, rol, esperado in casos:
            with self.subTest(cuenta=cuenta.nombre, rol=rol):
                self.assertEqual(self.client.patch(f"/api/admin/cuentas/{cuenta.pk}/rol/", {"rol": rol},
                                                   format="json").status_code, esperado)
        self.assertEqual(sorted(AdultoResponsable.objects.filter(rol="admin").values_list("nombre", flat=True)),
                         ["equipo", "equipo2"])
        self.assertEqual(self.client.patch("/api/admin/cuentas/99999/rol/", {"rol": "profesor"},
                                           format="json").status_code, 404)


@override_settings(PASSWORD_HASHERS=["django.contrib.auth.hashers.MD5PasswordHasher"])
class CrearCuentasPruebaTests(TestCase):
    def correr(self, **opciones):
        salida = StringIO()
        call_command("crear_cuentas_prueba", stdout=salida, **opciones)
        return salida.getvalue()

    @patch.dict("os.environ", {"FISHY_CLAVE_PRUEBA": "clave-de-prueba"})
    def test_crea_un_profesor_y_un_admin_con_la_clave_de_la_variable(self):
        salida = self.correr()
        profe = AdultoResponsable.objects.get(nombre="qa_profesor")
        admin = AdultoResponsable.objects.get(nombre="qa_admin")
        self.assertEqual((profe.rol, admin.rol), ("profesor", "admin"))
        self.assertTrue(profe.check_password("clave-de-prueba"))
        self.assertFalse(admin.is_admin)   # el rol del portal no abre /admin/ de Django
        self.assertNotIn("clave-de-prueba", salida)

    @patch.dict("os.environ", {"FISHY_CLAVE_PRUEBA": "otra"})
    def test_no_toca_cuentas_que_ya_existen(self):
        existente = AdultoResponsable.objects.create_user("qa_profesor", "real@example.com", "suya")
        self.correr()
        existente.refresh_from_db()
        self.assertEqual(existente.rol, "padre")
        self.assertTrue(existente.check_password("suya"))
        self.assertTrue(AdultoResponsable.objects.filter(nombre="qa_admin", rol="admin").exists())
        # Una segunda vuelta no crea nada más.
        self.assertIn("Nada que crear", self.correr())
        self.assertEqual(AdultoResponsable.objects.filter(nombre__startswith="qa_").count(), 2)

    @patch.dict("os.environ", {"FISHY_CLAVE_PRUEBA": ""})
    def test_sin_clave_ni_terminal_no_crea_nada(self):
        with patch("sys.stdin") as entrada:
            entrada.isatty.return_value = False
            with self.assertRaises(CommandError):
                self.correr()
        self.assertFalse(AdultoResponsable.objects.filter(nombre__startswith="qa_").exists())
