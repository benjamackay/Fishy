using UnityEngine;

namespace Fishy.UI
{
    /// <summary>
    /// Aspecto y textos del dibujo del teclado que enseña dónde está cada tecla
    /// (<see cref="TecladoEnPantalla"/>).
    ///
    /// Es solo configuración, igual que MisionHudTheme, ChatUITheme y
    /// DetectiveUITheme: aquí no hay código que haga nada.
    ///
    /// <b>Lo que NO está aquí son las teclas.</b> Ni una. Las teclas se le preguntan
    /// a quien las escucha —el <c>OttoController</c>, el <c>InteractionDetector</c>,
    /// el menú de pausa y el del celular—, porque un dibujo que enseña teclas y
    /// miente es peor que no tener dibujo. Aquí solo está cómo se ve y cómo se
    /// llaman las cosas en castellano.
    ///
    /// La FORMA del teclado —qué tecla va en qué fila y cuánto mide cada una— sí es
    /// cosa del dibujo y vive en <see cref="TecladoEnPantalla"/>, porque es una tabla
    /// de posiciones y no un ajuste que nadie vaya a querer tocar a ojo.
    /// </summary>
    public static class TecladoEnPantallaTheme
    {
        /// <summary>
        /// Por encima del cartel de misión (710) y por debajo del celular (1200), el
        /// álbum (1300) y el menú de pausa (9500): es una ayuda del juego, así que
        /// cualquier pantalla que se abra encima lo tapa, que es lo correcto.
        /// </summary>
        public static int OrdenDeDibujo = 800;

        public static class Colores
        {
            /// <summary>Fondo de la tarjeta. El mismo tono y la misma transparencia
            /// que el cartel de misión: son las dos ayudas permanentes de la pantalla
            /// de juego y tienen que leerse como la misma cosa.</summary>
            public static Color Panel = Paleta.Hex(0x33211A, 0.86f);

            /// <summary>Las teclas que el juego no usa. Están para orientarse —el
            /// teclado se reconoce por su forma entera, no por seis teclas sueltas—
            /// así que se dibujan apagadas, sin competir con las que importan.</summary>
            public static Color Tapa      = Paleta.Hex(0x4A3226);
            public static Color TapaLetra = Paleta.Hex(0x7A5A42);

            /// <summary>Las teclas que sí hacen algo. Claras, como una tecla de
            /// verdad iluminada, y con la letra oscura encima.</summary>
            public static Color Marcada      = Paleta.Crema;
            public static Color MarcadaLetra = Paleta.MarronOscuro;

            /// <summary>Mientras se la está pulsando. Es el verde de "esto va bien"
            /// del resto del juego: el niño/a pulsa y ve responder, que es lo que
            /// enseña cuál es cuál.</summary>
            public static Color Pulsada      = Paleta.Verde;
            public static Color PulsadaLetra = Paleta.Crema;

            /// <summary>
            /// La tecla que hace falta para la misión activa. Es el amarillo de
            /// «misión en curso» que ya usan el cartel de la esquina y la flecha que
            /// señala la zona, y se toma prestado de ahí en vez de repetir el hex: si
            /// alguien cambia ese amarillo, las tres cosas tienen que cambiar juntas o
            /// dejan de leerse como lo mismo.
            /// </summary>
            public static Color Destacada      => MisionHudTheme.Colores.Titulo;
            public static Color DestacadaLetra => FishyUIKit.TextoSobre(Destacada);

            public static Color Titulo = Paleta.Crema;

            /// <summary>La lista de abajo: el nombre de lo que hace cada tecla.</summary>
            public static Color Leyenda = Paleta.Arena;

            /// <summary>Dentro de esa lista, el nombre de la tecla va del mismo color
            /// que las teclas iluminadas del dibujo. Ese color es todo el vínculo
            /// entre la lista y el teclado, así que los dos tienen que ir juntos si
            /// alguien cambia uno.</summary>
            public static Color LeyendaTecla = Paleta.Crema;
        }

        public static class Medidas
        {
            /// <summary>
            /// Lado de una tecla de una unidad, en píxeles de la resolución de
            /// referencia (1920x1080). Es la medida que manda: el ancho de la tarjeta
            /// sale del dibujo, que ocupa 18,5 unidades de ancho (15 del bloque
            /// principal más el hueco y el bloque de flechas).
            ///
            /// Con 36 cada tecla se ve a 26 px reales en el equipo del colegio, que
            /// es el tamaño en el que una tecla todavía se reconoce de un vistazo.
            /// Subirlo engorda la tarjeta a lo ancho y a lo alto a la vez, y abajo a
            /// la derecha no sobra sitio.
            /// </summary>
            public static float Unidad = 36f;

            /// <summary>Separación entre teclas. Se descuenta del tamaño de cada una,
            /// así que las cuentas de posición siguen siendo múltiplos limpios de la
            /// unidad.</summary>
            public static float Hueco = 4f;

            /// <summary>
            /// Separación respecto a la esquina de abajo a la izquierda.
            ///
            /// Esa esquina y no la derecha porque la derecha se la quedó el celular
            /// del guía del tutorial, que es alto y baja hasta media pantalla. A la
            /// izquierda no hay nada más: el cartel de «para interactuar» vivía ahí y
            /// se corrió al lado (ver <see cref="CartelInteraccionTheme"/>).
            /// </summary>
            public static Vector2 Margen = new Vector2(20f, 20f);

            /// <summary>Margen interior de la tarjeta y separación entre sus tres
            /// partes (título, dibujo y lista).</summary>
            public static float Padding    = 20f;
            public static float Separacion = 14f;

            /// <summary>Redondeo de la tarjeta y de las teclas. El de las teclas es
            /// pequeño porque son pequeñas: con el radio de panel saldrían
            /// pastillas.</summary>
            public static int RadioPanel = 22;
            public static int RadioTecla = 6;

            /// <summary>Lado del triángulo que hace de flecha dentro de las cuatro
            /// teclas de dirección.</summary>
            public static float LadoFlecha = 14f;

            /// <summary>Aire entre la letra y el borde de su tapa, y suelo al que
            /// puede encoger esa letra si la palabra no cabe de otra forma. Por
            /// debajo de 9 ya no se lee, y vale más que una tapa diga «Shift» un poco
            /// apretado a que lo diga saliéndose por los lados.</summary>
            public static float MargenLetra = 3f;
            public static float LetraMinima = 9f;

            /// <summary>Alto de cada renglón de la lista de abajo, y separación entre
            /// las entradas de un mismo renglón.</summary>
            public static float AltoLeyenda   = 30f;
            public static float HuecoLeyenda  = 26f;

            /// <summary>Transparencia de lo que el niño/a ya usó: la tecla en el
            /// dibujo y su renglón en la lista. No se borra, se apaga; así se ve lo
            /// aprendido sin que la tarjeta cambie de tamaño a mitad de partida.</summary>
            public static float AlfaAprendida = 0.45f;

            /// <summary>
            /// Cuánto crece y encoge una tecla que la misión activa pide ahora mismo,
            /// y a qué ritmo. Los valores salen de la flecha que señala la zona
            /// (<c>MisionHudTheme.Medidas</c>): es el mismo latido, y a propósito —en
            /// esta pantalla, lo que late es lo que la misión está pidiendo—.
            /// </summary>
            public static float AmplitudLatido  = 0.08f;
            public static float VelocidadLatido = 2.2f;
        }

        public static class Fuente
        {
            /// <summary>Tamaños en píxeles de la resolución de referencia; en el
            /// equipo escolar van multiplicados por 0,71.</summary>
            public static float Titulo = 30f;

            /// <summary>La letra de una tecla del juego, y la de una que solo está de
            /// relleno (más pequeña: es textura, no información).</summary>
            public static float Letra  = 22f;
            public static float Relleno = 17f;

            /// <summary>Para las teclas que llevan palabra en vez de letra: Esc, Tab,
            /// Shift, Ctrl… Tienen que caber en su tapa.</summary>
            public static float Nombre = 15f;

            public static float Leyenda = 22f;
        }

        public static class Textos
        {
            public static string Titulo = "Las teclas";

            /// <summary>
            /// Cómo se llama cada acción en la lista de abajo. El nombre de la tecla
            /// no se escribe aquí: lo pone el propio teclado a partir de la atadura
            /// de verdad.
            /// </summary>
            public static string Caminar = "Caminar";

            /// <summary>Se añade detrás de las teclas de caminar. Las flechas se
            /// nombran con palabras y no con ↑↓←→ porque la fuente de cuerpo es
            /// estática y trae Latin-1 y poco más: esos cuatro caracteres saldrían
            /// como cuadros huecos. Las que se ven en el dibujo son triángulos
            /// dibujados.</summary>
            public static string OLasFlechas = " o las flechas";

            /// <summary>Dice las dos cosas porque la misma tecla hace las dos: se
            /// empieza a hablar y se pasa de línea. Un niño/a que no sabe lo segundo
            /// se queda mirando el panel esperando a que avance solo.</summary>
            public static string Hablar = "Hablar y seguir";

            public static string Correr  = "Correr";
            public static string Saltar  = "Saltar";
            public static string Celular = "Celular";
            public static string Pausa   = "Pausa";

            /// <summary>Separador entre la tecla y lo que hace, en la lista. Es el
            /// punto medio de Latin-1 (U+00B7), el mismo que usa el cartel de misión
            /// y por la misma razón: la viñeta redonda no está en la fuente.</summary>
            public static string Separador = " · ";

            /// <summary>Nombres de las teclas que no son una letra. Lo que no esté
            /// aquí se dibuja con el nombre que le da el Input System.</summary>
            public static string Espacio = "Espacio";
            public static string Shift   = "Shift";
            public static string Escape  = "Esc";
            public static string Tab     = "Tab";
        }
    }
}
