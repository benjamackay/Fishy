"""Filtro de las páginas de error de Django, para que no muestren credenciales.

## El problema

Con `DEBUG=True`, Django arma una página de error que lista **las variables
locales de cada frame** de la traza. Ahí aparecía la contraseña del apoderado en
texto plano. Medido: un fallo dentro del login devolvía 91 KB de HTML con la
contraseña adentro — y esa respuesta además terminaba escrita en el log del
juego, porque el cliente de Unity registraba el cuerpo de las respuestas de
error.

## Por qué no basta el decorador de Django

Lo primero que uno prueba es `@sensitive_variables("password")`, que es
justamente lo que Django trae para esto. **No funciona en este proyecto**, y se
comprobó midiendo con las dos disposiciones posibles: por fuera y por dentro de
`@api_view`. En ambas la contraseña seguía apareciendo.

El motivo es cómo está hecho el decorador: marca su propio frame envoltorio, y
el filtro de Django lo busca subiendo por los llamadores del frame que está
reportando. Entremedio está toda la maquinaria de DRF, así que la marca no queda
donde el filtro la espera. No es culpa de nadie: ese decorador se diseñó para
vistas de Django a secas, no para vistas de DRF.

Por eso acá se resuelve un nivel más arriba, con un filtro global: no depende de
acordarse de decorar cada vista, ni del orden de los decoradores, y cubre sola
cualquier vista nueva — incluidas las de JWT, si algún día se agregan.

## Las tres capas, y por qué hacen falta las tres

Cada una se agregó después de medir que la anterior no alcanzaba:

1. **Por nombre de variable.** En `auth_login` la contraseña es una local
   llamada `password`.
2. **Por clave de diccionario.** En `registro` viaja *dentro* de un dict, cuya
   variable tiene un nombre inocente.
3. **Sobre el texto impreso.** Los serializers de DRF reproducen en su `repr`
   los argumentos con que se construyeron, o sea
   `RegistroSerializer(data={'nombre': ..., 'password': '...'})`. Ni el nombre
   de la variable ni las claves del dict delatan eso: hay que mirar cómo se
   vería impreso.

## Lo que este filtro NO garantiza

Es defensa en profundidad, no una garantía. Una credencial con un nombre que no
esté en la lista, o incrustada en un texto con otro formato, se escapa igual.

**La única garantía real sigue siendo `DEBUG=False` fuera de desarrollo.** Esto
reduce mucho la exposición del día a día, que es cuando se trabaja con DEBUG
encendido; no lo reemplaza.
"""

import re

from django.views.debug import SafeExceptionReporterFilter

# Nombres que nunca deben aparecer en una traza. Se cubren también los de JWT
# (`access`, `refresh`) para el día que exista el portal de reportes a padres.
SOSPECHOSO = re.compile(
    r"pass|contrasen|clave|secret|token|auth|api_key|apikey|access|refresh|"
    r"credential|cookie|session",
    re.IGNORECASE,
)

# Hasta dónde bajar dentro de estructuras anidadas. Tres niveles cubren los
# casos reales sin arriesgarse a recorrer un grafo de objetos gigante mientras
# se está armando una página de error.
PROFUNDIDAD = 3

# `'password': 'loquesea'` dentro de un texto ya impreso. Las dos comillas se
# escriben por separado en vez de usar una retrorreferencia: es más largo, pero
# no se rompe al pasar por otras herramientas.
CLAVE_EN_TEXTO = re.compile(
    r"""(['"]\w*(?:pass|token|secret|clave|access|refresh|credential)\w*['"]\s*:\s*)"""
    r"""(?:'[^']*'|"[^"]*")""",
    re.IGNORECASE,
)


class FiltroCredenciales(SafeExceptionReporterFilter):
    """Tapa credenciales en las trazas: por nombre, por clave y por texto."""

    def _limpiar(self, valor, profundidad=0):
        if profundidad >= PROFUNDIDAD:
            return valor

        if isinstance(valor, dict):
            return {
                k: (
                    self.cleansed_substitute
                    if isinstance(k, str) and SOSPECHOSO.search(k)
                    else self._limpiar(v, profundidad + 1)
                )
                for k, v in valor.items()
            }

        if isinstance(valor, (list, tuple)):
            limpio = [self._limpiar(v, profundidad + 1) for v in valor]
            return tuple(limpio) if isinstance(valor, tuple) else limpio

        # Último recurso: mirar cómo se vería impreso. Si su repr trae algo con
        # pinta de credencial, se devuelve el texto ya saneado en vez del
        # objeto — que es lo que la página de error iba a imprimir igual.
        texto = repr(valor)
        saneado = CLAVE_EN_TEXTO.sub(
            lambda m: m.group(1) + "'" + self.cleansed_substitute + "'", texto
        )
        return saneado if saneado != texto else valor

    def get_traceback_frame_variables(self, request, tb_frame):
        limpias = []
        for nombre, valor in tb_frame.f_locals.items():
            if SOSPECHOSO.search(nombre):
                limpias.append((nombre, self.cleansed_substitute))
                continue
            try:
                limpias.append((nombre, self._limpiar(valor)))
            except Exception:
                # Armar la página de error no puede fallar por culpa del filtro:
                # ante cualquier objeto raro, se prefiere tapar a reventar.
                limpias.append((nombre, self.cleansed_substitute))
        return limpias
