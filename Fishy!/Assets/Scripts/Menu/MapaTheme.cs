using UnityEngine;

/// <summary>
/// Aspecto de la página "Mapa" del celular. Solo configuración, igual que
/// <see cref="MenuTabsTheme"/> y <see cref="MisionHudTheme"/>: quien la aplica es
/// <see cref="MapaTarjetaUI"/> (y, más adelante, los marcadores).
///
/// <b>Todo está medido sobre <c>referencias/mapaRefe.png</c></b> (928 × 514 px de celular =
/// 1500 × 830 unidades, o sea 1,616 unidades por píxel), no puesto a ojo. Los colores no
/// se toman de los otros temas a propósito: el amarillo del panel del Tab (#FFD933) y el
/// azul de la referencia (#38B6FF) no coinciden con los que ya había.
/// </summary>
public static class MapaTheme
{
    public static class Colores
    {
        /// <summary>Relleno de la tarjeta.</summary>
        public static Color FondoTarjeta = new Color32(0x6C, 0x44, 0x33, 255);

        /// <summary>Marco de la tarjeta y nombre de la zona: el mismo tono arena-rosado.</summary>
        public static Color BordeTarjeta = new Color32(0xC5, 0x87, 0x6C, 255);
        public static Color NombreZona = BordeTarjeta;

        /// <summary>«Zona actual» y «Misiones Activas».</summary>
        public static Color Titulo = new Color32(0xFF, 0xDE, 0x59, 255);

        /// <summary>El porcentaje de progreso de la zona.</summary>
        public static Color NumeroProgreso = new Color32(0x38, 0xB6, 0xFF, 255);

        /// <summary>El guion que sale cuando la zona no tiene nada que hacer: se apaga, para que
        /// no parezca un dato.</summary>
        public static Color NumeroProgresoVacio = new Color32(0xC5, 0x87, 0x6C, 255);
    }

    public static class Medidas
    {
        // Tarjeta: 326 × 354 px en la referencia.
        public static float AnchoTarjeta = 527f;
        public static float AltoTarjeta = 572f;

        /// <summary>Distancia de la tarjeta al borde izquierdo y al borde de arriba del mapa.</summary>
        public static float MargenIzquierdo = 74f;
        public static float MargenSuperior = 110f;

        /// <summary>Grosor del marco (7 px) y radio de las esquinas (unos 21 px).</summary>
        public static float GrosorMarco = 11f;
        public static float RadioEsquinas = 34f;

        /// <summary>Espacio entre el borde de la tarjeta y el texto, sin contar el marco.</summary>
        public static int RellenoLateral = 23;
        public static int RellenoArriba = 18;
        public static int RellenoAbajo = 20;

        public static float SeparacionEntreLineas = 6f;

        // Marcadores sobre el mapa. En la referencia el «!» mide unos 78 px de alto (≈ 126
        // unidades), el «?» unos 70 (≈ 113) y la estrella unos 58 de ancho (≈ 94). Son
        // constantes del panel y no del mundo: no crecen ni se encogen con el zoom del mapa.
        public static float AlturaSigno = 120f;
        public static float TamanoEstrella = 90f;
    }

    public static class Fuente
    {
        public static float Titulo = 54f;

        /// <summary>Era 42; más grande ahora que la tarjeta ya no lleva la lista de objetivos.</summary>
        public static float NombreZona = 56f;

        /// <summary>El porcentaje: solo eso, así que puede ser grande.</summary>
        public static float NumeroProgreso = 120f;
    }

    public static class Textos
    {
        public static string ZonaActual = "Zona actual";

        /// <summary>Una sola palabra a propósito: «Progreso de la zona» no cabe en una línea.</summary>
        public static string Progreso = "Progreso";

        /// <summary>Lo que se ve cuando la zona no tiene misiones ni objetos que contar.</summary>
        public static string SinDatos = "-";
    }
}
