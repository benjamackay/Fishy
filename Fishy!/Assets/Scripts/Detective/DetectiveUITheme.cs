using UnityEngine;
using UnityEngine.InputSystem;
using Fishy.UI;
// Paleta de Sprout Lands defautlt palette.png  (16x7, 97 colores)
/*
Hex(0xF3F4E7),   // #F3F4E7
Hex(0x713970),   // #713970
Hex(0x645552),   // #645552
Hex(0x7B6762),   // #7B6762
Hex(0x8C7369),   // #8C7369
Hex(0x9D866F),   // #9D866F
Hex(0xB2A486),   // #B2A486
Hex(0xC4BC93),   // #C4BC93
Hex(0xDED9AB),   // #DED9AB
Hex(0xF0EFB9),   // #F0EFB9
Hex(0x353738),   // #353738
Hex(0x474A4B),   // #474A4B
Hex(0x545959),   // #545959
Hex(0x6B7470),   // #6B7470
Hex(0x818B83),   // #818B83
Hex(0x9DA89A),   // #9DA89A
Hex(0xC1C8B9),   // #C1C8B9
Hex(0xDCE0D2),   // #DCE0D2
Hex(0x583F83),   // #583F83
Hex(0x694A87),   // #694A87
Hex(0x7B568C),   // #7B568C
Hex(0x90689F),   // #90689F
Hex(0xA77BB3),   // #A77BB3
Hex(0xC89DD1),   // #C89DD1
Hex(0xD8B1E0),   // #D8B1E0
Hex(0xEBD6E0),   // #EBD6E0
Hex(0x754C60),   // #754C60
Hex(0x90625D),   // #90625D
Hex(0xAA7959),   // #AA7959
Hex(0xB68962),   // #B68962
Hex(0xC49A6C),   // #C49A6C
Hex(0xDCB98A),   // #DCB98A
Hex(0xE8CFA6),   // #E8CFA6
Hex(0xF3E5C2),   // #F3E5C2
Hex(0x4C468B),   // #4C468B
Hex(0x555793),   // #555793
Hex(0x5F699C),   // #5F699C
Hex(0x7180B1),   // #7180B1
Hex(0x8599C7),   // #8599C7
Hex(0x92B2D4),   // #92B2D4
Hex(0x99C5DE),   // #99C5DE
Hex(0xCBE0DE),   // #CBE0DE
Hex(0x504086),   // #504086
Hex(0x5C4E92),   // #5C4E92
Hex(0x685D9E),   // #685D9E
Hex(0x766DAA),   // #766DAA
Hex(0x867FB8),   // #867FB8
Hex(0xA09DD4),   // #A09DD4
Hex(0xBCAFDE),   // #BCAFDE
Hex(0xDDD5DE),   // #DDD5DE
Hex(0x505E77),   // #505E77
Hex(0x5F7A79),   // #5F7A79
Hex(0x6E967C),   // #6E967C
Hex(0x82A884),   // #82A884
Hex(0x97BB8E),   // #97BB8E
Hex(0xAED499),   // #AED499
Hex(0xC2E09A),   // #C2E09A
Hex(0xDFF0BB),   // #DFF0BB
Hex(0x4A588E),   // #4A588E
Hex(0x577297),   // #577297
Hex(0x658CA1),   // #658CA1
Hex(0x7BA6B4),   // #7BA6B4
Hex(0x8CBFC2),   // #8CBFC2
Hex(0x9BD4C3),   // #9BD4C3
Hex(0xB1E0BE),   // #B1E0BE
Hex(0xD6F1CD),   // #D6F1CD
Hex(0x795E53),   // #795E53
Hex(0x957A4B),   // #957A4B
Hex(0xB09643),   // #B09643
Hex(0xBFA954),   // #BFA954
Hex(0xD4C169),   // #D4C169
Hex(0xEAE178),   // #EAE178
Hex(0xEEEE9B),   // #EEEE9B
Hex(0xF3F2C0),   // #F3F2C0
Hex(0x566560),   // #566560
Hex(0x67835C),   // #67835C
Hex(0x78A158),   // #78A158
Hex(0x8DB15D),   // #8DB15D
Hex(0xA4C263),   // #A4C263
Hex(0xC0D470),   // #C0D470
Hex(0xD2E077),   // #D2E077
Hex(0xE8EEAA),   // #E8EEAA
Hex(0x8A4A70),   // #8A4A70
Hex(0xA35B70),   // #A35B70
Hex(0xAF6776),   // #AF6776
Hex(0xBD757E),   // #BD757E
Hex(0xD99A9A),   // #D99A9A
Hex(0xE8B5AC),   // #E8B5AC
Hex(0xF3D8C5),   // #F3D8C5
Hex(0x6B4B5B),   // #6B4B5B
Hex(0x865161),   // #865161
Hex(0xA16159),   // #A16159
Hex(0xBA7C54),   // #BA7C54
Hex(0xD79E61),   // #D79E61
Hex(0xEEBA77),   // #EEBA77
Hex(0xF2CF8C),   // #F2CF8C
Hex(0xF7EBAA),   // #F7EBAA
*/

namespace Fishy.Detective
{
    /// <summary>
    /// Todo lo ajustable del Modo Detective en un solo sitio: colores, medidas,
    /// tamaños de letra y ritmo. <see cref="DetectiveUI"/> no tiene ni un número
    /// ni un color suelto, así que para cambiar cómo se ve el modo se edita este
    /// archivo y nada más — sin abrir el Editor ni tocar la escena.
    ///
    /// Los cafés salen del sprite del menú (Assets/images/Sprint 1.png) y el tan
    /// es el mismo color con que están escritas las pestañas del Tab, así que el
    /// Modo Detective queda a tono con el resto del juego.
    ///
    /// Los campos son `static` a propósito (no `readonly`): así se pueden pisar
    /// en runtime desde otro script si algún día hace falta, por ejemplo para un
    /// modo de alto contraste.
    /// </summary>
    public static class DetectiveUITheme
    {
        /// <summary>Convierte un color escrito como en Figma (0xRRGGBB) a Color.</summary>
        public static Color Hex(int rgb, float alfa = 1f) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f, alfa);

        // ─────────────────────────────────────────────────────────────────────
        //  COLORES
        // ─────────────────────────────────────────────────────────────────────
        public static class Colores
        {
            /// <summary>Oscurecido de la pantalla detrás de la ventana. Subir el
            /// alfa oscurece más el juego; bajarlo lo deja más visible.</summary>
            public static Color Backdrop = new Color(0f, 0f, 0f, 0.35f);

            public static Color Ventana = Paleta.Marron;   // café oscuro dominante
            public static Color Header = Paleta.MarronMedio;
            public static Color Historial = Paleta.MarronOscuro; // fondo del scroll
            public static Color BarraInferior = Paleta.MarronOscuro;

            public static Color BurbujaIzquierda = Paleta.MarronClaro;
            public static Color BurbujaDerecha = Paleta.MarronSuave;
            public static Color BurbujaOtto = Paleta.Madera; // Otto en el permiso

            /// <summary>Borde y tinte del mensaje marcado como sospechoso.
            /// Se mantiene rojo a propósito: es semántica de alerta.</summary>
            public static Color BordeMarcado = Paleta.Rojo;

            /// <summary>Cuánto se tiñe la burbuja al marcarla (0 = nada, 1 = rojo pleno).</summary>
            public static float FuerzaTinteMarcado = 0.25f;

            public static Color BotonConfirmar = Paleta.Arena;  // acento tan del menú
            /// <summary>Estos dos se salen de la paleta café a propósito: puestos en
            /// tonos cálidos se confundían entre sí y con el fondo de la tarjeta.</summary>
            public static Color BotonRepetir = Hex(0x2E3845);      // gris azulado
            public static Color BotonExplicacion = Hex(0x472961);  // morado

            public static Color PanelResultado = new Color(0.15f, 0.09f, 0.07f, 0.97f);
            public static Color Card = Paleta.MarronMedio;

            public static Color Texto = Paleta.Crema;       // crema del sprite
            public static Color TextoSuave = Paleta.Arena;  // autores y subtítulos
            public static Color TextoOscuro = Paleta.Marron; // sobre fondos claros

            /// <summary>Opacidad del nombre del autor y de la hora dentro de la burbuja.</summary>
            public static float AlfaTextoSecundario = 0.45f;
            public static float AlfaAutorPermiso = 0.75f;

            /// <summary>Opacidad de cada tarjeta de explicación guiada.</summary>
            public static float AlfaExplicacion = 0.95f;

            /// <summary>
            /// Elige texto crema u oscuro según qué tan claro sea el fondo. Evita
            /// que al subir el brillo de un botón el texto quede ilegible.
            /// </summary>
            public static Color TextoSobre(Color fondo) =>
                (fondo.r * 0.299f + fondo.g * 0.587f + fondo.b * 0.114f) > 0.6f
                    ? TextoOscuro
                    : Texto;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MEDIDAS  (en unidades del canvas, referencia 1920x1080)
        // ─────────────────────────────────────────────────────────────────────
        public static class Medidas
        {
            /// <summary>Tamaño de la ventana principal del chat observado.</summary>
            public static Vector2 Ventana = new Vector2(800f, 1000f);

            public static float AlturaHeader = 90f;
            public static float AlturaBarraInferior = 87f;

            /// <summary>Márgenes del scroll dentro de la ventana: deja hueco arriba
            /// para el header y abajo para la barra del botón. Tienen que ir a la par
            /// de AlturaHeader y AlturaBarraInferior o el historial se les monta encima.</summary>
            public static float MargenScrollAbajo = 87f;
            public static float MargenScrollArriba = 90f;

            // Burbujas de la conversación observada
            /// <summary>Tope: AnchoBurbuja + AnchoEspaciador debe caber en
            /// Ventana.x menos el padding de la fila, o la burbuja se recorta.</summary>
            public static float AnchoBurbuja = 640f;
            public static float FlexBurbuja = 300f;
            public static RectOffset PaddingBurbuja => new RectOffset(21, 21, 15, 15);
            public static float EspaciadoBurbuja = 8f;
            public static RectOffset PaddingFila => new RectOffset(12, 12, 5, 5);
            public static float EspaciadoHistorial = 9f;
            public static RectOffset PaddingHistorial => new RectOffset(0, 0, 12, 12);

            /// <summary>Ancho mínimo del hueco que empuja la burbuja al lado contrario.</summary>
            public static float AnchoEspaciador = 90f;

            /// <summary>Grosor del borde rojo del mensaje marcado.</summary>
            public static float GrosorBordeMarcado = 5f;

            // Burbujas del ritual de permiso
            public static float AnchoBurbujaPermiso = 600f;
            public static RectOffset PaddingBurbujaPermiso => new RectOffset(24, 24, 15, 15);
            public static float EspaciadoBurbujaPermiso = 6f;
            public static float EspaciadoEntreBurbujasPermiso = 15f;

            // Tarjetas (permiso y resultado)
            public static float AnchoCardPermiso = 800f;
            public static float AnchoCardResultado = 720f;
            public static RectOffset PaddingCardPermiso => new RectOffset(36, 36, 36, 36);
            public static RectOffset PaddingCardResultado => new RectOffset(42, 42, 42, 42);
            public static float EspaciadoCard = 24f;
            public static float EspaciadoExplicaciones = 15f;
            public static RectOffset PaddingExplicacion => new RectOffset(21, 21, 15, 15);

            // Header
            public static Vector2 TamanoAvatar = new Vector2(60f, 60f);
            public static float MargenAvatar = 21f;
            /// <summary>Sangría del título/subtítulo para dejar pasar el avatar.
            /// Debe ser mayor que MargenAvatar + TamanoAvatar.x o el texto se le encima.</summary>
            public static float SangriaTextoHeader = 96f;

            /// <summary>La que se usa cuando no hay icono: sin el avatar delante,
            /// la sangría grande dejaría un hueco vacío raro a la izquierda.</summary>
            public static float SangriaTextoHeaderSinIcono = 24f;
            public static float MargenDerechoHeader = -18f;

            // Botones
            public static float AlturaBoton = 78f;
            public static Vector2 MargenBotonBarra = new Vector2(21f, 14f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TIPOGRAFÍA
        // ─────────────────────────────────────────────────────────────────────
        public static class Fuente
        {
            public static int AutorBurbuja = 27;
            public static int TextoBurbuja = 33;
            public static int Hora = 24;

            public static int AutorPermiso = 24;
            public static int TextoPermiso = 30;

            public static int TituloHeader = 33;
            public static int SubtituloHeader = 26;
            public static int IconoHeader = 40;

            public static int Boton = 33;
            public static int Marcador = 78;
            public static int Resultado = 33;
            public static int Explicacion = 30;
            public static int TituloCard = 39;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  RITMO
        // ─────────────────────────────────────────────────────────────────────
        public static class Ritmo
        {
            /// <summary>Segundos entre mensaje y mensaje al reproducir la conversación.</summary>
            public static float RetardoEntreMensajes = 0.5f;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FUENTES  (TextMeshPro)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Rutas dentro de una carpeta Resources.
        ///
        /// Mango es la fuente de la marca (la misma de la pantalla de inicio) y
        /// tiene el español completo — 201 glifos, con tildes y ñ. Ojo: en el
        /// proyecto conviven DOS generaciones de Mango y la otra
        /// ("Mango SDF") solo tiene 96 glifos ASCII, así que escribiría
        /// "explicacion" con la ó rota. La buena es la de abajo.
        ///
        /// La de cuerpo queda como red de seguridad: <c>DetectiveUI</c> le
        /// pregunta a Mango si puede escribir cada texto y solo cae a esta si
        /// aparece algún carácter que le falte.
        /// </summary>
        public static class Fuentes
        {
            public static string RutaTitulos = FishyUIKit.RutaTitulos;
            public static string RutaCuerpo = FishyUIKit.RutaCuerpo;

            /// <summary>Fuente de símbolos (lupa, check…). Mango no los tiene, así
            /// que sin esto el 🔍 saldría como cuadrito roto. Se genera con
            /// Fishy → Generar fuente de iconos.</summary>
            public static string RutaIconos = "Fonts & Materials/NotoSymbols2 SDF";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SPRITES
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Fondo ilustrado del historial de mensajes, lo que pidió Daniela.
        ///
        /// Mientras se decide cuál usar, el modo toma una imagen al azar de la
        /// carpeta y deja rotar con una tecla dentro del juego. Una vez elegida,
        /// se pone Rotar en false y el nombre en FijoPorNombre.
        /// </summary>
        public static class Fondo
        {
            /// <summary>Carpeta dentro de Assets/Resources con las candidatas. Las
            /// imágenes tienen que estar importadas como Sprite o no se ven.</summary>
            public static string Carpeta = "Fondos/Detective";

            /// <summary>Mientras esté en true se elige una al azar al abrir el modo
            /// y la tecla de abajo salta a otra. En false manda FijoPorNombre.</summary>
            public static bool Rotar = true;

            /// <summary>Con Rotar en false, el fondo que queda fijo. Vacío = ninguno,
            /// y el historial se ve con su color liso de siempre.</summary>
            public static string FijoPorNombre = "";

            /// <summary>Tecla para saltar al siguiente fondo al azar.</summary>
            public static Key TeclaSiguiente = Key.F;

            /// <summary>
            /// Tinte de la ilustración. El alfa es lo importante: a 1 el dibujo tapa
            /// el color del historial y compite con los mensajes; bajarlo lo deja de
            /// telón de fondo, que es como se ve un fondo de chat.
            /// </summary>
            public static Color Tinte = new Color(1f, 1f, 1f, 0.30f);

            /// <summary>Repetir la imagen en mosaico en vez de estirarla. Para
            /// patrones sirve; para una ilustración entera, no.</summary>
            public static bool Repetir = false;

            /// <summary>Muestra el nombre del fondo en pantalla mientras se prueba,
            /// para poder anotar cuál gustó.</summary>
            public static bool MostrarNombre = true;

            public static float TamanoNombre = 22f;
        }

        public static class Sprites
        {
            /// <summary>
            /// Lado, en píxeles, del 9-slice de esquinas redondeadas que se dibuja
            /// en memoria al abrir el modo. No se usa un sprite del proyecto porque
            /// no hace falta: la forma es un rectángulo redondeado y sale más barato
            /// generarla que mantener un asset.
            /// </summary>
            public static int LadoRedondeado = 64;

            /// <summary>
            /// Radio de las esquinas, en píxeles, y también el borde del 9-slice: por
            /// eso el redondeo se ve igual sin importar cuánto se estire la burbuja.
            /// Subirlo redondea más. Tiene que ser como mucho la mitad de
            /// <see cref="LadoRedondeado"/>, o las esquinas se pisan entre ellas.
            /// </summary>
            public static int RadioRedondeado = 22;

            /// <summary>Lupa propia para el header, si existe. Va en una carpeta
            /// Resources (ej. Assets/Resources/Iconos/lupa.png). Si no está, se
            /// dibuja una en memoria con las medidas de más abajo.</summary>
            public static string IconoLupa = "Iconos/lupa";

            // ── Lupa dibujada en memoria ──────────────────────────────────────
            //
            // Es la que se usa mientras no haya un sprite propio. Se dibuja en vez
            // de sacarla de una fuente de símbolos porque esa vía resultó frágil:
            // 🔍 es U+1F50D, fuera del BMP, y el motor de fuentes de Unity no llegó
            // a él ni con un TTF que sí trae el glifo. Dibujada no depende de
            // ninguna fuente, ni del atlas, ni de lo que el build haga con ellos.
            //
            // Las tres medidas de abajo son proporciones, no píxeles: la forma se
            // reescala sola para llenar el sprite, así que cambiar LadoLupa solo
            // cambia la resolución, nunca el diseño.

            /// <summary>Resolución del sprite, en píxeles. Subirlo solo lo hace más
            /// nítido; no cambia el tamaño en pantalla (eso es TamanoAvatar).</summary>
            public static int LadoLupa = 128;

            /// <summary>Grosor del trazo, como fracción del radio del lente. Más
            /// alto = lupa más gorda y más legible en chico.</summary>
            public static float GrosorLupa = 0.28f;

            /// <summary>Largo del mango, como fracción del radio del lente.</summary>
            public static float MangoLupa = 0.85f;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TEXTOS FIJOS DE LA INTERFAZ
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Sin emoji a propósito: ninguna de las fuentes del proyecto los tiene, y
        /// se dibujarían como un cuadrito roto. La lupa del header se hace con
        /// formas, no con el carácter 🔍.
        /// </summary>
        public static class Textos
        {
            /// <summary>Se dibuja con la fuente de iconos, no con Mango.</summary>
            public static string IconoHeader = "🔍";

            public static string TituloHeader = "Modo Detective";
            public static string SubtituloHeader = "toca un mensaje para marcarlo como sospechoso";
            public static string BotonConfirmar = "Confirmar marcas";
            public static string TituloPermiso = "Pidiendo permiso...";
            public static string BotonContinuarPermiso = "Continuar";
            public static string TituloResultado = "Resultado";
            public static string ResultadoConSenales = "señales de riesgo identificadas";
            public static string ResultadoSinSenales = "¡No había señales de riesgo en esta conversación!";
            public static string BotonRepetir = "Repetir caso";
            public static string BotonExplicacion = "Ver explicación";
            public static string BotonCerrar = "Continuar";
        }
    }
}
