"""Verifica que una credencial no termine escrita en una página de error.

## Por qué existe este archivo

Con `DEBUG=True`, Django arma una página de error que lista las variables locales
de cada frame de la traza. Ahí aparecía **la contraseña del apoderado en texto
plano**: medido, un fallo dentro del login devolvía 91 KB de HTML con ella
adentro. Y esa respuesta terminaba además en el log del juego, porque el cliente
de Unity registra el cuerpo de las respuestas de error.

Lo tapa el filtro global `juego_backend.filtros_error.FiltroCredenciales`.

Un arreglo de seguridad sin test se vuelve a romper en silencio: basta que
alguien agregue una variable, cambie una vista o toque el filtro para reabrir la
fuga sin que nadie se entere. Por eso está acá y no en un script suelto.

## Cómo leer una falla

Si alguno de estos tests falla, **la contraseña de los apoderados está saliendo
en las respuestas de error del servidor**. No es un test quisquilloso: el mensaje
dice en qué escenario y en qué contexto apareció.

## La trampa al escribir este tipo de prueba

El doble que fuerza el fallo tiene que imitar la firma real de la función que
reemplaza. El primer intento usó `def falso(*a, **k)` y ese `**k` metía la
contraseña en la traza **por culpa del test, no del código**: se medía un
artefacto propio. Por eso `_auth_que_falla` declara `(request=None, **credentials)`,
igual que `django.contrib.auth.authenticate`.
"""

from unittest import mock

from django.test import TestCase, override_settings
from rest_framework.authtoken.models import Token

from api.models import AdultoResponsable

# El valor se arma en pedazos a propósito, y no como un literal completo.
#
# La página de error de Django muestra las líneas de código fuente alrededor de
# cada frame de la traza. Como `_auth_que_falla` vive en este mismo archivo, su
# frame arrastra a la página unas líneas de aquí — y si la contraseña estuviera
# escrita entera en una de ellas, el test se marcaría fuga a sí mismo. Ya pasó.
#
# En producción esto no aplica: los frames son de `api/views.py`, donde ninguna
# contraseña está escrita como literal.
CLAVE = "".join(("Clave", "DePrueba", "MuySecreta", "987"))


def _auth_que_falla(request=None, **credentials):
    """Doble de `authenticate` con su misma firma. Simula una caída de la BD."""
    raise RuntimeError("caida simulada durante el login")


class CredencialesEnTrazasTest(TestCase):
    """Ninguna respuesta de error puede contener la contraseña."""

    def setUp(self):
        AdultoResponsable.objects.create_user(
            nombre="apoderado_de_prueba", email="apoderado@ejemplo.cl", password=CLAVE
        )

    def _comprobar(self, respuesta, escenario, debug):
        cuerpo = respuesta.content.decode("utf-8", errors="replace")
        if CLAVE not in cuerpo:
            return
        i = cuerpo.index(CLAVE)
        contexto = " ".join(cuerpo[max(0, i - 120):i + 40].split())
        self.fail(
            "FUGA DE CONTRASENA en la respuesta de error.\n"
            "  escenario : %s\n"
            "  DEBUG     : %s\n"
            "  contexto  : ...%s...\n"
            "Revisa juego_backend/filtros_error.py — probablemente la credencial "
            "viaja de una forma que sus tres capas todavia no cubren."
            % (escenario, debug, contexto)
        )

    def _pedir(self, ruta, cuerpo):
        return self.client.post(ruta, cuerpo, content_type="application/json")

    # ── Escenario 1: falla dentro del login ─────────────────────────────────
    def test_login_no_filtra_la_contrasena_con_debug_encendido(self):
        with override_settings(DEBUG=True, ALLOWED_HOSTS=["*"]):
            with mock.patch("api.views.authenticate", _auth_que_falla):
                self.client.raise_request_exception = False
                r = self._pedir(
                    "/api/auth/login/",
                    {"nombre": "apoderado_de_prueba", "password": CLAVE},
                )
        self._comprobar(r, "falla dentro del login", True)

    def test_login_no_filtra_la_contrasena_con_debug_apagado(self):
        with override_settings(DEBUG=False, ALLOWED_HOSTS=["*"]):
            with mock.patch("api.views.authenticate", _auth_que_falla):
                self.client.raise_request_exception = False
                r = self._pedir(
                    "/api/auth/login/",
                    {"nombre": "apoderado_de_prueba", "password": CLAVE},
                )
        self._comprobar(r, "falla dentro del login", False)

    # ── Escenario 2: falla el registro, ya con el serializer cargado ────────
    #
    # Este es el que costó cerrar: acá la contraseña no es una variable local
    # llamada `password`, viaja dentro del `repr` del serializer, que reproduce
    # los argumentos con que se construyó.
    def test_registro_no_filtra_la_contrasena_con_debug_encendido(self):
        with override_settings(DEBUG=True, ALLOWED_HOSTS=["*"]):
            with mock.patch.object(
                Token.objects, "get_or_create", side_effect=RuntimeError("caida")
            ):
                self.client.raise_request_exception = False
                r = self._pedir(
                    "/api/auth/registro/",
                    {
                        "nombre": "apoderado_nuevo",
                        "email": "nuevo@ejemplo.cl",
                        "password": CLAVE,
                    },
                )
        self._comprobar(r, "falla al emitir el token en el registro", True)

    # ── El login normal tampoco debe devolver la contraseña ─────────────────
    def test_el_login_correcto_no_devuelve_la_contrasena(self):
        r = self._pedir(
            "/api/auth/login/",
            {"nombre": "apoderado_de_prueba", "password": CLAVE},
        )
        self.assertEqual(r.status_code, 200)
        self._comprobar(r, "login correcto", "por omision")

    def test_el_perfil_del_adulto_no_devuelve_la_contrasena(self):
        entrada = self._pedir(
            "/api/auth/login/",
            {"nombre": "apoderado_de_prueba", "password": CLAVE},
        )
        token = entrada.json()["token"]
        r = self.client.get("/api/auth/perfil/", HTTP_AUTHORIZATION="Token " + token)
        self.assertEqual(r.status_code, 200)
        self.assertNotIn("password", r.json())
        self._comprobar(r, "perfil del adulto", "por omision")


class FiltroDeCredencialesTest(TestCase):
    """Prueba el filtro directamente, sin pasar por una vista.

    Sirve para saber, cuando algo falle arriba, si el problema es el filtro o es
    que la credencial viaja de una forma nueva.
    """

    def setUp(self):
        from juego_backend.filtros_error import FiltroCredenciales

        self.filtro = FiltroCredenciales()
        self.tapado = self.filtro.cleansed_substitute

    def _limpiar(self, valor):
        return self.filtro._limpiar(valor)

    def test_tapa_una_clave_dentro_de_un_diccionario(self):
        limpio = self._limpiar({"nombre": "Ana", "password": CLAVE})
        self.assertEqual(limpio["password"], self.tapado)
        self.assertEqual(limpio["nombre"], "Ana", "no debe tapar lo que no es secreto")

    def test_tapa_tambien_los_nombres_de_jwt(self):
        limpio = self._limpiar({"access": "abc", "refresh": "def", "zona": "bosque"})
        self.assertEqual(limpio["access"], self.tapado)
        self.assertEqual(limpio["refresh"], self.tapado)
        self.assertEqual(limpio["zona"], "bosque")

    def test_tapa_una_clave_anidada(self):
        limpio = self._limpiar({"cuerpo": {"datos": {"password": CLAVE}}})
        self.assertEqual(limpio["cuerpo"]["datos"]["password"], self.tapado)

    def test_tapa_una_clave_dentro_del_texto_impreso_de_un_objeto(self):
        class Falso:
            def __repr__(self):
                return "Serializer(data={'nombre': 'Ana', 'password': '%s'})" % CLAVE

        limpio = self._limpiar(Falso())
        self.assertNotIn(CLAVE, str(limpio))
        self.assertIn("Ana", str(limpio), "solo debe tapar la credencial")

    def test_no_revienta_con_un_objeto_cuyo_repr_falla(self):
        class Explosivo:
            def __repr__(self):
                raise ValueError("no se puede imprimir")

        frame = mock.Mock()
        frame.f_locals = {"cosa": Explosivo()}
        # Armar la pagina de error no puede fallar por culpa del filtro.
        resultado = self.filtro.get_traceback_frame_variables(None, frame)
        self.assertEqual(resultado[0][0], "cosa")
