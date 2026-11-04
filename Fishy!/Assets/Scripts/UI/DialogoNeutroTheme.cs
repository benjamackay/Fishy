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
    /// a mano en la escena, no generado en código. Durante mucho tiempo eso significó
    /// que aquí no hubiera medidas y que los tamaños de letra vinieran a 0 ("respeta
    /// lo que haya puesto la escena"). Ya no: la escena lo tenía a 24 pt en una caja
    /// de 100 px de alto que se solapaba 16 px con la del nombre, y en el equipo
    /// escolar típico —1366x768— esos 24 pt se dibujan a 17 px reales. Para niños/as
    /// de 9 a 13 eso no es un detalle de acabado, es el requisito.
    ///
    /// Así que ahora las medidas y los tamaños mandan desde aquí y
    /// <see cref="DialogoNeutroSkin"/> recoloca las cajas al abrir el diálogo. Se
    /// arregla en código y no en el YAML de la escena por lo de siempre en este
    /// proyecto: tocar escenas a mano descuadra fileIDs en silencio, y además así el
    /// panel queda igual venga de donde venga quien lo abra.
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

            /// <summary>
            /// Con esto en false el <see cref="DialogoNeutroSkin"/> solo pinta y deja
            /// las cajas donde las dejó la escena, que es como se comportaba antes.
            /// Está para poder volver atrás sin borrar código si alguien rediseña el
            /// panel en el editor y prefiere mandar desde ahí.
            /// </summary>
            public static bool AjustarGeometria = true;

            /// <summary>Tamaño del panel, en píxeles de la resolución de referencia
            /// (1920x1080). Antes era 800x200, donde no caben cuatro líneas a 32 pt.
            /// Tampoco conviene estirarlo mucho más: a 880 entran unos cuarenta
            /// caracteres por línea, que es el largo en el que un párrafo se sigue
            /// leyendo sin perder el renglón al volver.</summary>
            public static Vector2 TamanoPanel = new Vector2(880f, 280f);

            /// <summary>Cuánto se levanta el panel del borde de abajo.</summary>
            public static float MargenInferior = 100f;

            /// <summary>Margen interior del panel y separación entre el nombre y el
            /// cuerpo.</summary>
            public static float Margen = 28f;
            public static float Separacion = 12f;

            /// <summary>Lado del retrato y hueco entre él y el texto. El lado es el
            /// mismo que traía la escena: los retratos son dibujos de 128 px y
            /// agrandarlos solo los emborrona.</summary>
            public static float LadoRetrato = 128f;
            public static float HuecoRetrato = 24f;

            /// <summary>Alto de la línea del nombre. Una sola línea: el nombre no
            /// envuelve (ver <see cref="DialogoNeutroSkin"/>).</summary>
            public static float AltoNombre = 56f;
        }

        public static class Fuente
        {
            /// <summary>
            /// Tamaños en píxeles de la resolución de referencia (1920x1080). Hay que
            /// leerlos multiplicados por el factor de la pantalla real: en 1366x768,
            /// que es el equipo del colegio, el factor es 0,71 — así que 32 aquí son
            /// 23 px de verdad, y los 24 que traía la escena eran 17.
            ///
            /// 0 sigue significando "respeta lo que diga la escena", por si alguien
            /// quiere volver a mandar desde el editor.
            /// </summary>
            public static float Nombre = 40f;
            public static float Texto  = 32f;

            /// <summary>
            /// Cuánto texto entra en la caja del cuerpo con las medidas de arriba:
            /// unas cuatro líneas de ~42 caracteres. La línea más larga que tiene hoy
            /// el juego ronda los 90, así que sobra la mitad.
            ///
            /// No se enciende el ajuste automático de tamaño en el cuerpo —sí en el
            /// nombre— porque el texto se escribe letra a letra: con el ajuste puesto,
            /// la frase iría encogiendo sola mientras se teclea y daría un salto en
            /// cada línea nueva. Si alguna vez hace falta una línea más larga que
            /// esto, se parte en dos líneas de diálogo en el NPCDialogue, que para eso
            /// son un array. Hay una prueba del arnés que lo vigila.
            /// </summary>
            public const int CaracteresPorLinea = 42;
            public const int LineasQueCaben = 4;
        }
    }
}
