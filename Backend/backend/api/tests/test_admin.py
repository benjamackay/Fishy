"""Tests del panel de administración.

El admin es la única pantalla donde alguien que no programa puede revisar si el
contenido quedó bien cargado. Durante meses no mostró nada del banco ni del Modo
Detective porque esos modelos nunca se registraron — y un modelo sin registrar no
da error ni sale vacío: simplemente no existe para el admin.
"""
from io import StringIO

from django.apps import apps
from django.contrib import admin
from django.core.management import call_command
from django.test import RequestFactory, TestCase

from api.models import (
    AdultoResponsable,
    CasoDetective,
    DialogoNPC,
    Mision,
    OpcionBanco,
    PreguntaBanco,
    RecompensaAlbum,
    RecompensaObtenida,
)


class RegistroCompletoTests(TestCase):
    def test_no_queda_ningun_modelo_invisible(self):
        """Guarda contra la regresión: un modelo nuevo sin registrar es contenido
        que nadie del equipo puede revisar sin abrir un shell."""
        del_app = set(apps.get_app_config("api").get_models())
        registrados = set(admin.site._registry)
        faltan = sorted(m.__name__ for m in del_app - registrados)
        self.assertEqual(
            faltan, [],
            f"estos modelos no se ven en el admin: {faltan}. "
            f"Regístralos en api/admin.py.",
        )


class ContenidoSoloLecturaTests(TestCase):
    """El JSON de banco_preguntas/ es la fuente de verdad.

    Editar contenido en el admin se vería aplicado hasta el próximo
    `cargar_banco`, que lo pisaría sin avisar: una divergencia silenciosa entre
    el JSON, la base y Unity. Por eso el contenido es de solo lectura.
    """

    CONTENIDO = (PreguntaBanco, OpcionBanco, CasoDetective,
                 Mision, DialogoNPC, RecompensaAlbum)

    def test_el_contenido_del_banco_no_se_puede_editar_desde_el_admin(self):
        for modelo in self.CONTENIDO:
            with self.subTest(modelo=modelo.__name__):
                opciones = admin.site._registry[modelo]
                self.assertFalse(opciones.has_add_permission(None))
                self.assertFalse(opciones.has_change_permission(None))
                self.assertFalse(opciones.has_delete_permission(None))

    def test_el_progreso_del_nino_si_es_administrable(self):
        """No viene del JSON: se genera jugando, y a veces hay que limpiarlo.

        Acá sí hace falta un request real: el ModelAdmin por defecto resuelve el
        permiso contra `request.user`, a diferencia de SoloLecturaAdmin, que
        responde que no sin mirar quién pregunta.
        """
        request = RequestFactory().get("/admin/")
        request.user = AdultoResponsable.objects.create_superuser(
            email="admin@test.local", nombre="Admin", password="clave-de-prueba-123"
        )
        opciones = admin.site._registry[RecompensaObtenida]
        self.assertTrue(opciones.has_delete_permission(request))


class PaginasDelAdminCarganTests(TestCase):
    """`manage.py check` valida la configuración, pero no que la página abra.

    Un `list_display` con un método que revienta, un `list_filter` sobre una
    relación mal escrita o un `__str__` que falla solo se notan al renderizar.
    Como el contenido del banco ya está cargado acá, las páginas se dibujan con
    filas reales y no vacías.
    """

    @classmethod
    def setUpTestData(cls):
        cls.admin_user = AdultoResponsable.objects.create_superuser(
            email="admin-paginas@test.local", nombre="Admin", password="clave-de-prueba-123"
        )
        call_command("cargar_banco", stdout=StringIO())

    def setUp(self):
        self.client.force_login(self.admin_user)

    def test_el_indice_lista_los_modelos_nuevos(self):
        r = self.client.get("/admin/")
        self.assertEqual(r.status_code, 200)

    def test_cada_listado_abre_sin_reventar(self):
        for modelo in apps.get_app_config("api").get_models():
            url = f"/admin/api/{modelo._meta.model_name}/"
            with self.subTest(modelo=modelo.__name__):
                r = self.client.get(url)
                self.assertEqual(r.status_code, 200, f"{url} devolvió {r.status_code}")

    def test_la_ficha_de_una_pregunta_muestra_sus_opciones(self):
        """El inline de opciones es lo que hace útil la pantalla: ver la pregunta
        y sus respuestas con su impacto, sin saltar a otra tabla."""
        pregunta = PreguntaBanco.objects.first()
        r = self.client.get(f"/admin/api/preguntabanco/{pregunta.pk}/change/")
        self.assertEqual(r.status_code, 200)
        for opcion in pregunta.opciones.all():
            self.assertContains(r, opcion.opcion_id)

    def test_la_recompensa_muestra_de_donde_viene(self):
        premio = RecompensaAlbum.objects.filter(mision__isnull=False).first()
        r = self.client.get("/admin/api/recompensaalbum/")
        self.assertEqual(r.status_code, 200)
        self.assertContains(r, premio.mision.mision_id)
