using UnityEngine;

namespace Fishy.UI
{
    /// <summary>
    /// Aspecto del diálogo de los NPCs neutros: colores, medidas y tamaños de letra.
    ///
    /// Es solo configuración, igual que DetectiveUITheme y ChatUITheme. La lógica que
    /// aplica estos valores está en <see cref="DialogoNeutroSkin"/>; aquí no hay
    /// código que haga nada, para poder ajustar cómo se ve sin leer cómo funciona.
    ///
    /// Ojo con una diferencia respecto a los otros dos temas: este panel está montado
    /// a mano en la escena, no generado en código. Por eso hay pocas medidas —la
    /// posición y el tamaño los manda la escena— y los tamaños de letra vienen a 0,
    /// que significa "respeta lo que haya puesto ahí".
    /// </summary>
    public static class DialogoNeutroTheme
    {
        public static class Colores
        {
            /// <summary>
            /// Fondo del panel. Va con algo de transparencia para que se siga viendo
            /// el mundo por detrás: la conversación es cara a cara y ocurre EN la
            /// escena, no en una pantalla aparte, así que tapar del todo el fondo
            /// rompe esa sensación.
            ///
            /// El alfa es lo que conviene tocar: 1 = opaco, 0.85 = apenas se
            /// transparenta, 0.6 = se ve claramente el bosque detrás. Por debajo de
            /// ~0.7 el texto empieza a competir con lo que haya de fondo.
            /// </summary>
            public static Color Panel = Paleta.Hex(0x4A3226, 0.85f);

            public static Color Nombre = Paleta.Crema;
            public static Color Texto  = Paleta.Crema;
        }

        public static class Medidas
        {
            /// <summary>Redondeo de las esquinas del panel. Mismo valor que el chat y
            /// el Modo Detective, para que los tres se lean como el mismo juego.</summary>
            public static int RadioEsquina = 22;
        }

        public static class Fuente
        {
            /// <summary>0 = respetar el tamaño puesto en la escena. Se deja así porque
            /// el panel está montado a mano y su tipografía ya está ajustada al
            /// espacio disponible; pisarla desde código descuadraría el diseño.
            /// Poniendo un número mayor que 0, manda este valor.</summary>
            public static float Nombre = 0f;
            public static float Texto  = 0f;
        }
    }
}
