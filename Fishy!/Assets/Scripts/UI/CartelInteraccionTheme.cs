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
            /// <summary>Cuánto sobresale el fondo por cada lado de lo que hay
            /// dentro.</summary>
            public static float Padding = 18f;

            /// <summary>Mismo redondeo que el resto de los paneles del juego.</summary>
            public static int RadioEsquina = 22;
        }
    }
}
