using Fishy.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// Aspecto del menú del Tab: las pestañas y sus páginas (Jugador, Misión,
/// Inventario, Mapa, Opciones).
///
/// Es solo configuración, igual que DetectiveUITheme, ChatUITheme y
/// DialogoNeutroTheme. Aquí no hay código que haga nada: la lógica que aplica
/// estos valores está en <see cref="TabsController"/>, <see cref="QuestPageUI"/> e
/// <see cref="InventoryManagerUI"/>.
///
/// Los colores salen de <see cref="Paleta"/>, la misma del resto del juego.
/// </summary>
public static class MenuTabsTheme
{
    public static class Colores
    {
        /// <summary>
        /// Panel de fondo del menú entero, detrás de las pestañas. Es la capa más
        /// exterior y por eso la más clara de las tres: el ojo lee "caja grande,
        /// dentro una página, dentro una lista".
        /// </summary>
        public static Color FondoMenu = Paleta.Marron;

        /// <summary>Fondo del área con scroll, dentro de la página. Igual que la
        /// página para que se lean como una sola superficie y no como un recuadro
        /// gris pegado encima, que es como se veía antes.</summary>
        public static Color FondoScroll = Paleta.MarronOscuro;

        /// <summary>Carril de las barras de desplazamiento.</summary>
        public static Color ScrollbarCarril = Paleta.MarronMedio;

        /// <summary>Tirador de las barras. En arena para que se vea sobre el carril
        /// sin gritar como el blanco de antes.</summary>
        public static Color ScrollbarTirador = Paleta.Arena;

        /// <summary>
        /// Fondo de las páginas del menú.
        ///
        /// Va oscuro por una razón medible, no por gusto: los textos de las páginas
        /// están escritos en colores claros —naranja para la misión en curso, verde
        /// para la completada, blanco translúcido para los objetivos— y sobre el
        /// blanco que tenían antes su contraste caía a 1.4:1 … 2.8:1, cuando el
        /// mínimo legible es 4.5:1. Literalmente no se leían.
        ///
        /// Sobre este marrón los cuatro suben a entre 5.4:1 y 11.1:1. Si lo aclaras,
        /// el primero que deja de leerse es el mensaje de "sin misiones", que es el
        /// de menor contraste.
        /// </summary>
        public static Color FondoPagina = Paleta.MarronOscuro;

        /// <summary>Tinte de la pestaña abierta. Blanco = el sprite se ve tal cual.</summary>
        public static Color PestanaActiva = Color.white;

        /// <summary>Tinte de las pestañas cerradas. Oscurece el sprite en vez de
        /// teñirlo, para que se lea "apagada" y no "de otro color".</summary>
        public static Color PestanaInactiva = new Color(0.6f, 0.6f, 0.6f, 1f);

        // ── Página de misiones ────────────────────────────────────────────────

        /// <summary>Título de una misión en curso.</summary>
        public static Color MisionEnCurso = new Color(1f, 0.85f, 0.2f);

        /// <summary>Título de una misión ya completada.</summary>
        public static Color MisionCompletada = new Color(0.4f, 0.85f, 0.4f);

        /// <summary>Objetivo que todavía falta.</summary>
        public static Color ObjetivoPendiente = new Color(1f, 1f, 1f, 0.75f);

        /// <summary>Texto de "no hay nada que mostrar". Es el de menor contraste de
        /// la página: si aclaras FondoPagina, este es el primero en caerse.</summary>
        public static Color TextoVacio = new Color(1f, 1f, 1f, 0.6f);

        // ── Página de inventario ──────────────────────────────────────────────

        /// <summary>Fondo de cada ranura de la mochila.</summary>
        public static Color FondoRanura = new Color(0f, 0f, 0f, 0.25f);
    }

    public static class Fuente
    {
        public static float TituloMision = 30f;
        public static float Objetivo     = 24f;

        /// <summary>
        /// Fuente de los títulos de misión: Mango, la misma de las pestañas y del
        /// resto del juego. Antes estos textos salían con la fuente por defecto de
        /// TMP, que no es de ninguna parte y hacía que la página pareciera de otro
        /// programa.
        /// </summary>
        public static TMP_FontAsset Titulo => FishyUIKit.Titulos;

        /// <summary>
        /// Fuente de los objetivos. Va en la de cuerpo y no en Mango a propósito:
        /// Mango es una fuente de rótulo, buena para tres palabras y cansada para
        /// una lista de frases. Si la prefieres en todo, cambia esto por
        /// FishyUIKit.Titulos.
        /// </summary>
        public static TMP_FontAsset Objetivos => FishyUIKit.Cuerpo;
    }

    public static class Textos
    {
        public static string SinMisiones  = "Sin misiones por ahora. Habla con alguien.";
        public static string MochilaVacia = "Mochila vacía";
    }
}
