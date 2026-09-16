using Fishy.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// HDU-16 — Aspecto del cartel de misión activa y de la flecha que señala la zona.
///
/// Es solo configuración, igual que MenuTabsTheme, ChatUITheme y DetectiveUITheme:
/// aquí no hay código que haga nada. Quien aplica estos valores es
/// <see cref="MissionUIController"/> y <see cref="ZoneMarker"/>.
///
/// Los estados de misión (amarillo = en curso, verde = cumplido) se toman
/// prestados de <see cref="MenuTabsTheme"/> a propósito, en vez de repetir los
/// hex: el HUD y la página del Tab hablan de las mismas misiones, y si un día
/// alguien cambia el verde, tiene que cambiar en los dos sitios o dejan de
/// parecer lo mismo.
/// </summary>
public static class MisionHudTheme
{
    public static class Colores
    {
        /// <summary>Fondo del cartel. Translúcido para que no tape el mapa: es una
        /// guía permanente, no una ventana.</summary>
        public static Color FondoPanel = Paleta.Hex(0x33211A, 0.86f);

        /// <summary>La palabra "MISIÓN" encima del título. En arena para que se lea
        /// como una etiqueta y no compita con el nombre de la misión.</summary>
        public static Color Etiqueta = Paleta.Arena;

        /// <summary>Nombre de la misión activa.</summary>
        public static Color Titulo = MenuTabsTheme.Colores.MisionEnCurso;

        /// <summary>Objetivo que todavía falta.</summary>
        public static Color ObjetivoPendiente = MenuTabsTheme.Colores.ObjetivoPendiente;

        /// <summary>Objetivo ya cumplido.</summary>
        public static Color ObjetivoCumplido = MenuTabsTheme.Colores.MisionCompletada;

        /// <summary>Línea de "ve a tal sitio". En crema, el tono de lectura del
        /// juego: es una instrucción, no un estado.</summary>
        public static Color Guia = Paleta.Crema;

        /// <summary>Mensaje de "no hay nuevas misiones" (CA6).</summary>
        public static Color SinMisiones = MenuTabsTheme.Colores.TextoVacio;

        /// <summary>Relleno de la flecha que apunta a la zona.</summary>
        public static Color Flecha = MenuTabsTheme.Colores.MisionEnCurso;

        /// <summary>Contorno de la flecha. Oscuro para que se despegue del fondo
        /// del mapa, que en el juego es claro y arenoso: sin él la flecha amarilla
        /// desaparece justo sobre la playa.</summary>
        public static Color FlechaBorde = Paleta.MarronOscuro;

        /// <summary>Cartelito con el nombre de la zona, bajo la flecha.</summary>
        public static Color FondoEtiquetaZona = Paleta.Hex(0x33211A, 0.8f);
    }

    public static class Fuente
    {
        public static float Etiqueta    = 20f;
        public static float Titulo      = 32f;
        public static float Objetivo    = 22f;
        public static float Guia        = 24f;
        public static float NombreZona  = 22f;

        /// <summary>Mango, la de la marca, para la etiqueta y el título.</summary>
        public static TMP_FontAsset Titulos => FishyUIKit.Titulos;

        /// <summary>La de cuerpo para la lista de objetivos: Mango es de rótulo y
        /// cansa en frases seguidas, por lo mismo que en la página del Tab.</summary>
        public static TMP_FontAsset Objetivos => FishyUIKit.Cuerpo;
    }

    public static class Medidas
    {
        /// <summary>Ancho del cartel, en píxeles de la resolución de referencia
        /// (1920x1080).</summary>
        public static float AnchoPanel = 440f;

        /// <summary>Separación del cartel respecto a la esquina.</summary>
        public static Vector2 MargenPanel = new Vector2(28f, -28f);

        /// <summary>
        /// Cuántos objetivos se listan como mucho. El resumen es un recordatorio de
        /// un vistazo: pasada media docena de líneas deja de leerse y hay que abrir
        /// el Tab, que es donde está la lista entera.
        /// </summary>
        public static int MaxObjetivos = 3;

        /// <summary>Radio en píxeles al que orbita la flecha alrededor de Otto. Si el
        /// destino está más cerca que esto, la flecha se posa encima.</summary>
        public static float RadioFlecha = 190f;

        /// <summary>Tamaño de la flecha: largo en la dirección a la que apunta.</summary>
        public static float LargoFlecha = 56f;
        public static float AnchoFlecha = 46f;

        /// <summary>Grosor del contorno oscuro, en píxeles por lado.</summary>
        public static float GrosorBorde = 5f;

        /// <summary>Cuánto baja el cartelito del nombre respecto a la flecha.</summary>
        public static float BajadaEtiquetaZona = 46f;

        /// <summary>Margen mínimo entre el marcador y el borde de la pantalla, para
        /// que el nombre de la zona no quede cortado.</summary>
        public static float MargenPantalla = 90f;

        /// <summary>Cuánto late la flecha (0 = quieta). Un latido lento llama la
        /// atención sin marear.</summary>
        public static float AmplitudLatido = 0.08f;
        public static float VelocidadLatido = 2.2f;
    }

    public static class Textos
    {
        public static string Etiqueta = "MISIÓN";

        /// <summary>HDU-16 CA6. Frase entera y en positivo: "no hay nada" a secas se
        /// lee como que el juego se rompió.</summary>
        public static string SinMisiones =
            "Por ahora no hay nuevas misiones disponibles. ¡Explora y vuelve a hablar con quien quieras!";

        /// <summary>Plantilla de la línea que dice a dónde ir. {0} = nombre de zona.</summary>
        public static string IrA = "Ve a {0}";

        /// <summary>
        /// Viñeta de cada objetivo. Es el punto medio de Latin-1 (U+00B7) y no la
        /// viñeta redonda (U+2022) por lo mismo que el aspa de cerrar se dibuja en
        /// vez de escribirse: la fuente de cuerpo es estática y trae Latin-1 y poco
        /// más, así que un carácter de fuera sale como un cuadrito hueco.
        /// </summary>
        public static string Vinneta = "·";

        /// <summary>
        /// Lo que se le añade al objetivo ya cumplido. Va con palabras y no con un
        /// tic por lo mismo de arriba —U+2714 tampoco está en las fuentes— y, de
        /// paso, a esta edad una palabra se entiende mejor que un símbolo.
        /// </summary>
        public static string Cumplido = "¡listo!";
    }
}
