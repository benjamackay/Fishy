using Fishy.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fishy.Chat
{
    /// <summary>
    /// Aspecto del chat de NPCs: colores, medidas y tamaños de letra, todos juntos
    /// para poder ajustarlos sin entrar en la lógica de <see cref="ChatModuleUI"/>.
    ///
    /// Los colores salen de <see cref="Paleta"/>, la misma que usa el resto del
    /// juego, así que el chat dejó de ser gris azulado sobre negro y pasó a verse
    /// como Fishy. Lo único que se sale de la paleta a propósito es el marco del
    /// teléfono: un celular tiene el bisel negro, y teñirlo de marrón lo haría
    /// parecer un mueble en vez de un aparato.
    /// </summary>
    public static class ChatUITheme
    {
        public static class Colores
        {
            public static Color Ventana   = Paleta.Marron;
            public static Color Header    = Paleta.MarronMedio;
            public static Color Historial = Paleta.MarronOscuro;

            /// <summary>Burbuja del NPC, a la izquierda.</summary>
            public static Color BurbujaNpc = Paleta.MarronClaro;

            /// <summary>Burbuja del niño/a, a la derecha. Más clara para que se
            /// distinga de un vistazo de quién es cada mensaje.</summary>
            public static Color BurbujaNino = Paleta.MarronSuave;

            /// <summary>Avisos del sistema. Va en madera para separarse de las dos
            /// anteriores sin gritar como lo haría un rojo.</summary>
            public static Color BurbujaSistema = Paleta.Madera;

            public static Color Texto      = Paleta.Crema;
            public static Color TextoSuave = Paleta.Arena;

            /// <summary>Botones de respuesta. Todos iguales a propósito: si las
            /// opciones seguras se vieran distintas de las arriesgadas, el juego
            /// estaría dando la respuesta antes de que el niño/a piense.</summary>
            public static Color BotonOpcion = Paleta.MarronMedio;

            public static Color BotonCerrar     = Paleta.Rojo;
            public static Color BotonContinuar  = Paleta.Verde;
            public static Color Card            = Paleta.MarronMedio;

            /// <summary>Oscurecido de la pantalla. En modo teléfono es más fuerte
            /// porque el celular tiene que destacar sobre el mundo.</summary>
            public static Color Backdrop        = new Color(0f, 0f, 0f, 0.12f);
            public static Color BackdropTelefono = new Color(0f, 0f, 0f, 0.55f);
        }

        /// <summary>Medidas que comparten los dos modos del chat.</summary>
        public static class Medidas
        {
            /// <summary>Redondeo de esquinas. Es lo que pidió Daniela: "que su forma
            /// no sea tan cuadrada".</summary>
            public static int   RadioEsquina   = 22;

            /// <summary>Alto mínimo de un botón de respuesta. Es un suelo: si el texto
            /// envuelve a varias líneas, el botón crece.</summary>
            public static float AlturaBoton    = 78f;

            public static float EspaciadoLista = 12f;

            /// <summary>Ancho de burbuja de reserva, en píxeles. Solo se usa si no se
            /// puede medir la pantalla; normalmente manda Telefono.FraccionAnchoBurbuja.</summary>
            public static float AnchoBurbuja   = 420f;

            /// <summary>Tamaño de la ventana antes de que el modo teléfono la
            /// redimensione. También es el de reserva para calcular el tope de las
            /// opciones.</summary>
            public static Vector2 Ventana      = new Vector2(560f, 900f);

            /// <summary>Tope de la zona de opciones, en fracción del alto del panel.
            /// Pasado de ahí las opciones hacen scroll: sin este tope, un nodo con
            /// muchas respuestas largas se comía el historial entero.</summary>
            public static float FraccionMaxOpciones = 0.45f;

            // Tarjeta del estado de ánimo de Otto, al cerrar la conversación.
            public static Vector2 CardAnimo  = new Vector2(900f, 700f);
            public static Vector2 BotonAnimo = new Vector2(360f, 90f);
        }

        /// <summary>
        /// Solo para la conversación CARA A CARA. Se ve como el diálogo de un NPC
        /// neutro: un panel pegado abajo, sin cabecera ni historial.
        /// </summary>
        public static class CaraACara
        {
            /// <summary>Ancho del panel. Ancho y bajo, como el del NPC neutro:
            /// hablando en persona no hay una pantalla de por medio.</summary>
            public static float AnchoPanel   = 1400f;

            /// <summary>Separación desde el borde inferior de la pantalla.</summary>
            public static float MargenInferior = 60f;
        }

        /// <summary>
        /// Solo para la conversación POR TELÉFONO. Se ve como el Modo Detective: una
        /// app de mensajería dentro del celular de Otto.
        /// </summary>
        public static class Telefono
        {
            /// <summary>
            /// Pantalla del teléfono. Más ancha que un móvil real a propósito: con la
            /// proporción 9:16 auténtica (600x1060) las respuestas largas envolvían a
            /// tres o cuatro líneas y el marco no cabía en el canvas de 1080 de alto.
            ///
            /// Al cambiarla, deja margen: el marco es esto más Borde por lado, y el
            /// canvas de referencia mide 1920x1080. El techo práctico son ~1040 de alto.
            /// </summary>
            public static Vector2 Ventana = new Vector2(780f, 920f);

            /// <summary>Grosor del bisel a cada lado. El marco se calcula solo a partir
            /// de la pantalla, así que ensanchar una cosa ya no descuadra la otra.</summary>
            public static float Borde = 20f;

            /// <summary>Ancho de burbuja como fracción del ancho de la pantalla. En
            /// fracción y no en píxeles para que al cambiar el tamaño del teléfono las
            /// burbujas sigan ocupando lo mismo en proporción.</summary>
            public static float FraccionAnchoBurbuja = 0.74f;

            /// <summary>Alto de la cabecera con el nombre del contacto.</summary>
            public static float AlturaHeader = 90f;

            /// <summary>Aire alrededor del contenido del panel.</summary>
            public static int   PadPanel = 16;

            /// <summary>Alto de la barra de estado (reloj e iconos).</summary>
            public static float AlturaBarraEstado = 48f;

            /// <summary>Alto al que flota la barra de inicio sobre el borde inferior
            /// de la pantalla.</summary>
            public static float MargenBarraInicio = 10f;

            /// <summary>Lado del botón de cerrar. Cuadrado.</summary>
            public static float LadoBotonCerrar = 52f;

            /// <summary>
            /// Largo de cada barra del aspa de cerrar, en fracción del lado del botón.
            /// Ojo: al estar giradas 45°, el aspa ocupa solo un 71% de ese largo en
            /// cada eje. Con 0.62 mide ~44% del botón, la proporción habitual.
            /// </summary>
            public static float FraccionAspa = 0.62f;

            /// <summary>Grosor de cada barra del aspa, en píxeles.</summary>
            public static float GrosorAspa = 5f;

            /// <summary>
            /// Papel tapiz del historial, igual que en el Modo Detective: una
            /// imagen al azar de una carpeta de Resources, detrás de las burbujas.
            /// Solo aplica al chat por teléfono; el diálogo cara a cara sigue liso
            /// porque no es una pantalla, es una conversación en persona.
            ///
            /// Lo mueve <see cref="Fishy.UI.FondoAleatorio"/>, el mismo componente
            /// que usa el detective.
            /// </summary>
            public static class Fondo
            {
                /// <summary>Carpeta dentro de Assets/Resources con las candidatas.
                /// Las imágenes tienen que estar importadas como Sprite o no se ven.
                ///
                /// Apunta de momento a la carpeta del detective para poder probar
                /// con las once que ya hay. Cuando existan tapices propios del
                /// teléfono, esto pasa a "Fondos/Chat" y no hay que tocar nada más.</summary>
                public static string Carpeta = "Fondos/Detective";

                /// <summary>Mientras esté en true se elige una al azar al abrir el
                /// chat y la tecla de abajo salta a otra. En false manda FijoPorNombre.</summary>
                public static bool Rotar = true;

                /// <summary>Con Rotar en false, el tapiz que queda fijo. Vacío =
                /// ninguno, y el historial se ve con su color liso de siempre.</summary>
                public static string FijoPorNombre = "";

                /// <summary>Tecla para saltar al siguiente tapiz al azar. Distinta de
                /// la del detective a propósito: si las dos pantallas siguen vivas en
                /// memoria, una sola tecla movería las dos a la vez y no se sabría
                /// cuál se está mirando.</summary>
                public static Key TeclaSiguiente = Key.G;

                /// <summary>
                /// Tinte de la ilustración. El alfa es lo importante: a 1 el dibujo
                /// tapa el color del historial y compite con los mensajes; bajarlo lo
                /// deja de telón de fondo, que es como se ve un fondo de chat.
                /// </summary>
                public static Color Tinte = new Color(1f, 1f, 1f, 0.30f);

                /// <summary>Repetir la imagen en mosaico en vez de estirarla. Para
                /// patrones sirve; para una ilustración entera, no.</summary>
                public static bool Repetir = false;

                /// <summary>Muestra el nombre del tapiz en pantalla mientras se
                /// prueba, para poder anotar cuál gustó.</summary>
                public static bool MostrarNombre = true;

                public static float TamanoNombre = 22f;
            }
        }

        public static class Fuente
        {
            public static float Header       = 36f;
            public static float TextoBurbuja = 30f;
            public static float Autor        = 24f;
            public static float Boton        = 30f;
            public static float BotonCerrar  = 40f;
            public static float Emoji        = 160f;
            public static float MensajeAnimo = 44f;
            public static float Reloj        = 24f;

            // Panel de diálogo cara a cara
            public static float NombrePanel    = 34f;
            public static float TextoPanel     = 32f;
            public static float RespuestaPanel = 26f;
        }

        public static class Textos
        {
            public static string BotonContinuar = "Continuar";

            /// <summary>Prefijo de la respuesta elegida en el panel cara a cara.</summary>
            public static string PrefijoRespuesta = "Tú: ";
        }

        /// <summary>
        /// Animación de Otto en el panel de cierre, en el sitio del emoji. Cada una es
        /// una hoja de sprites dentro de una carpeta Resources, cortada en frames
        /// (_0, _1, _2) en el Sprite Editor. Se reproduce en bucle mientras el panel
        /// esté abierto.
        /// </summary>
        public static class Animo
        {
            /// <summary>"Otto se siente seguro": las flores se abren.</summary>
            public static string Seguro     = "Otto/otto_safe";

            /// <summary>"Otto está preocupado": la gota de sudor crece.</summary>
            public static string Preocupado = "Otto/otto_worried";

            /// <summary>Cuánto dura cada frame. Con tres, cada vuelta tarda menos de
            /// un segundo.</summary>
            public static float SegundosPorFrame = 0.25f;

            /// <summary>Tamaño en pantalla. Los frames miden 256 px; cuanto más
            /// cerca de eso, más nítidos.</summary>
            public static Vector2 Tamano = new Vector2(240f, 240f);
        }
    }
}
