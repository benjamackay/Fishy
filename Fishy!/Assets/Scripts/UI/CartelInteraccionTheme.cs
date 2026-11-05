using UnityEngine;

namespace Fishy.UI
{
    /// <summary>
    /// Aspecto del cartel de «E para interactuar»
    /// (<see cref="CartelInteraccionSkin"/>).
    ///
    /// Es solo configuración, como DialogoNeutroTheme y MisionHudTheme: aquí no hay
    /// código que haga nada.
    /// </summary>
    public static class CartelInteraccionTheme
    {
        public static class Colores
        {
            /// <summary>
            /// Fondo del cartel. Es el mismo del cartel de misión y del teclado de
            /// ayuda, y por el mismo motivo: son las tres ayudas que se leen mientras
            /// se camina, y tienen que parecer la misma cosa.
            ///
            /// Hace falta porque el cartel se dibuja directamente sobre el mapa. El
            /// texto va en crema, el bosque y la playa son claros y arenosos, y crema
            /// sobre arena no se lee: es el mismo problema que ya tenía la flecha de
            /// misión, que se resolvió poniéndole un contorno oscuro.
            /// </summary>
            public static Color Panel = Paleta.Hex(0x33211A, 0.86f);
        }

        public static class Medidas
        {
            /// <summary>
            /// Dónde cae la esquina de abajo a la izquierda del cartel ya pintado, en
            /// píxeles de la resolución de referencia (1920x1080).
            ///
            /// Está aquí, y no en la escena, porque el cartel está en dos escenas
            /// —la del mundo y la del tutorial— y en las dos estorbaba en el mismo
            /// sitio: su esquina de abajo a la izquierda se la quedó el dibujo del
            /// teclado (<see cref="TecladoEnPantalla"/>), que ocupa hasta x=726.
            /// Desde aquí se arregla una vez para las dos, y para la que venga.
            ///
            /// Si el dibujo del teclado cambia de tamaño, esto hay que moverlo: hay
            /// una prueba del arnés que avisa si los dos se pisan.
            /// </summary>
            public static Vector2 Posicion = new Vector2(750f, 20f);

            /// <summary>Cuánto sobresale el fondo por cada lado de lo que hay
            /// dentro.</summary>
            public static float Padding = 18f;

            /// <summary>Mismo redondeo que el resto de los paneles del juego.</summary>
            public static int RadioEsquina = 22;
        }
    }
}
