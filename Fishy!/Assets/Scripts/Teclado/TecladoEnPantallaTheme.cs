using UnityEngine;

namespace Fishy.UI
{
    /// <summary>
    /// Aspecto y textos de la tarjeta de controles (<see cref="TecladoEnPantalla"/>),
    /// que sale en dos sitios: abierta a mano desde Configuración ▸ Controles, y como
    /// recordatorio en una esquina cuando empieza una misión nueva.
    ///
    /// Es solo configuración, igual que MisionHudTheme, ChatUITheme y
    /// DetectiveUITheme: aquí no hay código que haga nada.
    ///
    /// <b>Lo que NO está aquí son las teclas.</b> Ni una. Las teclas se le preguntan
    /// a quien las escucha —el <c>OttoController</c>, el <c>InteractionDetector</c>,
    /// el menú de pausa y el del celular—, porque un cartel que enseña teclas y
    /// miente es peor que no tener cartel. Aquí solo está cómo se ven y cómo se
    /// llaman en castellano.
    /// </summary>
    public static class TecladoEnPantallaTheme
    {
        /// <summary>
        /// Orden de dibujo del canvas propio que monta el recordatorio.
        ///
        /// Solo lo usa ese modo: abierta desde la pausa, la tarjeta cuelga del canvas
        /// del menú (9500) y no tiene canvas que ordenar. 800 la deja encima del
        /// cartel de misión (710) y por debajo del celular (1200) y del Modo
        /// Detective (950): es una ayuda al caminar, no algo que deba taparle una
        /// pantalla al niño/a.
        /// </summary>
        public static int OrdenDeDibujo = 800;

        public static class Colores
        {
            /// <summary>Fondo de la tarjeta: el mismo de las tarjetas de la pausa,
            /// porque se abre en su lugar.</summary>
            public static Color Panel = Paleta.Marron;

            /// <summary>Cara de una tecla en reposo.</summary>
            public static Color Tecla = Paleta.MarronClaro;

            /// <summary>Cara de una tecla mientras se la está pulsando. Es el verde de
            /// "esto va bien" del resto del juego: el niño/a pulsa y ve responder, que
            /// es justo lo que enseña cuál es cuál.</summary>
            public static Color TeclaPulsada = Paleta.Verde;

            /// <summary>
            /// Cara de una tecla que la misión activa está pidiendo ahora mismo.
            ///
            /// Es el color del cartel de misión, y a propósito: el niño/a ya aprendió
            /// en el cartel de la esquina que ese color significa «esto es lo que hay
            /// que hacer». Reusarlo aquí no es ahorro, es que signifique lo mismo en
            /// las dos pantallas.
            /// </summary>
            public static Color Destacada      => MisionHudTheme.Colores.Titulo;
            public static Color DestacadaLetra => FishyUIKit.TextoSobre(Destacada);

            /// <summary>Letra de la tecla y nombre de lo que hace.</summary>
            public static Color Letra    = Paleta.Crema;
            public static Color Etiqueta = Paleta.Crema;

            /// <summary>El "o las flechas" y demás letra pequeña.</summary>
            public static Color Pista = Paleta.Arena;

            public static Color Titulo = Paleta.Crema;
        }

        public static class Medidas
        {
            /// <summary>Ancho de la tarjeta, en píxeles de la resolución de referencia
            /// (1920x1080), antes de <see cref="Escala"/>. El alto lo pone el contenido.</summary>
            public static float AnchoPanel = 480f;

            /// <summary>Cuánto se agranda la tarjeta entera en la pausa. Se diseñó para
            /// una esquina de la pantalla; en el centro y sola, a tamaño 1 se ve chica.</summary>
            public static float Escala = 1.3f;

            /// <summary>
            /// Dónde se pone la tarjeta cuando sale como recordatorio: su esquina de
            /// abajo a la izquierda, a esta distancia del borde.
            ///
            /// Abajo a la izquierda y no en el centro porque como recordatorio tiene
            /// que poderse ignorar: sale sola mientras el niño/a camina, y una tarjeta
            /// en medio de la pantalla no es una ayuda, es un obstáculo. A 480x1.3 =
            /// 624 px de ancho cabe antes del cartel de «para interactuar», que está
            /// puesto en x=750 justamente por esto
            /// (ver <c>CartelInteraccionTheme.Medidas.Posicion</c>). Hay una prueba
            /// del arnés que avisa si los dos se pisan.
            /// </summary>
            public static Vector2 MargenRecordatorio = new Vector2(20f, 20f);

            /// <summary>Alto del botón «Volver».</summary>
            public static float AltoBoton = 60f;

            /// <summary>Margen interior, separación entre filas y entre teclas.</summary>
            public static float Padding    = 20f;
            public static float Separacion = 12f;
            public static float Hueco      = 8f;

            /// <summary>
            /// Qué parte del ancho se lleva la acción de la izquierda en las filas
            /// que llevan dos. Menos de la mitad a propósito: a la izquierda van
            /// Shift y Tab, que son teclas cortas, y a la derecha Espacio, que es la
            /// más larga de todas y es la que se queda sin sitio.
            /// </summary>
            public static float RepartoFilaDoble = 0.45f;

            /// <summary>Lado de una tecla cuadrada (las letras) y alto de todas.</summary>
            public static float LadoTecla = 56f;

            /// <summary>
            /// De qué ancho se hace una tecla que no es una letra: tantos píxeles por
            /// carácter más un margen a los lados. Es una cuenta aproximada y a
            /// propósito —medir el texto de verdad obliga a montar el canvas— y se
            /// queda corta antes que larga: una tecla un pelo ancha no se nota, una
            /// estrecha corta la palabra. Hay una prueba del arnés que comprueba que
            /// ninguna fila se sale de la tarjeta.
            /// </summary>
            public static float AnchoPorLetra = 0.55f;
            public static float PaddingTecla  = 24f;

            /// <summary>Redondeo de la tarjeta y de las teclas. El de la tarjeta es el
            /// mismo de siempre; el de las teclas es menor porque son pequeñas y con
            /// 22 se quedarían casi redondas.</summary>
            public static int RadioPanel = 22;
            public static int RadioTecla = 10;

            /// <summary>Lado del triangulito que indica la dirección dentro de cada
            /// tecla de caminar.</summary>
            public static float LadoFlecha = 16f;

            /// <summary>Cuánto se baja la flecha respecto al centro de la tecla, para
            /// que no se monte con la letra.</summary>
            public static float BajadaFlecha = 17f;

            /// <summary>
            /// Cuánto crece y a qué ritmo late una tecla que la misión está pidiendo.
            ///
            /// Es el mismo latido de la flecha que señala la zona, y con el mismo
            /// sentido: en esta pantalla, lo que se mueve es lo que la misión pide. Un
            /// 8% es poco a propósito —se ve de reojo y no distrae de caminar— y 2,2
            /// es un latido tranquilo, más lento que un pulso nervioso.
            /// </summary>
            public static float AmplitudLatido  = 0.08f;
            public static float VelocidadLatido = 2.2f;
        }

        public static class Fuente
        {
            /// <summary>Tamaños en píxeles de la resolución de referencia. En el equipo
            /// escolar (1366x768) hay que multiplicarlos por 0,71: la etiqueta de 26
            /// son 18 px de verdad, y la pista de 20 son 14, que es el suelo de lo que
            /// se lee cómodamente a esa edad.</summary>
            public static float Titulo   = 30f;
            public static float Letra    = 28f;
            public static float Etiqueta = 24f;
            public static float Pista    = 20f;
            public static float Boton    = 26f;
        }

        public static class Textos
        {
            public static string Titulo = "Controles";
            public static string Volver = "Volver";

            public static string Caminar = "Caminar";

            /// <summary>Se dice con palabras y no con ↑↓←→: la fuente de cuerpo es
            /// estática y trae Latin-1 y poco más, así que las flechas saldrían como
            /// cuadros huecos. Las que sí se ven son dibujadas (ver
            /// <see cref="FishyUIKit.SpriteTriangulo"/>).</summary>
            public static string CaminarPista = "o las flechas";

            /// <summary>Dice las dos cosas porque la misma tecla hace las dos: se
            /// empieza a hablar y se pasa de línea. Un niño/a que no sabe lo segundo
            /// se queda mirando el panel esperando a que avance solo.</summary>
            public static string Hablar = "Hablar y seguir";

            public static string Correr  = "Correr";
            public static string Celular = "Celular";
            public static string Pausa   = "Pausa";

            /// <summary>Nombres de las teclas que no son una letra. Lo que no esté
            /// aquí se dibuja con el nombre que le da el Input System.</summary>
            public static string Espacio = "Espacio";
            public static string Shift   = "Shift";
            public static string Escape  = "Esc";
            public static string Tab     = "Tab";
        }
    }
}
