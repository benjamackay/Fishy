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

        /// <summary>"!" y flecha de los NPC y chats de teléfono pendientes. Azul, para
        /// que no se confunda con la flecha amarilla de la zona.</summary>
        public static Color ObjetivoNpc = Paleta.Hex(0x3FA7F5);

        /// <summary>"?" y flecha de los casos del Modo Detective pendientes.</summary>
        public static Color ObjetivoDetective = Paleta.Hex(0x9B5DE5);
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

        // ── Señales de NPC (NpcMarker) ───────────────────────────────────────

        /// <summary>Tamaño del "!" o "?" sobre la cabeza, en tamaño de fuente de TMP
        /// en el mundo (10 de fuente ≈ 1 unidad de mundo de alto).</summary>
        public static float TamanoSigno = 8f;

        /// <summary>Alto del signo cuando es un dibujo (exclamacion.png / interrogacion.png),
        /// en unidades de mundo. Con el respaldo de texto manda <see cref="TamanoSigno"/>.</summary>
        public static float AlturaSignoDibujo = 1f;

        /// <summary>Distancia, en unidades de mundo, entre lo más alto del NPC y el
        /// signo.</summary>
        public static float ElevacionSigno = 0.5f;

        /// <summary>Cuánto sube y baja el signo, en unidades de mundo, y a qué ritmo.</summary>
        public static float AmplitudRebote = 0.12f;
        public static float VelocidadRebote = 3f;

        /// <summary>
        /// A qué distancia de Otto la flecha de un NPC es la más grande y a cuál la más
        /// pequeña. Se miden en "alturas de pantalla" y no en unidades de mundo, para que
        /// valga igual con cualquier zoom o resolución: 1 = un NPC a una pantalla de
        /// distancia de Otto.
        /// </summary>
        public static float DistanciaCerca = 0.6f;
        public static float DistanciaLejos = 3f;

        /// <summary>Multiplicador del tamaño de la flecha (1 = el de la flecha de zona)
        /// para el NPC más cercano y para el más lejano.</summary>
        public static float EscalaCerca = 1.3f;
        public static float EscalaLejos = 0.65f;
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
