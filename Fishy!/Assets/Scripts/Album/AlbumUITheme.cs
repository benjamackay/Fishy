using System.Collections.Generic;
using Fishy.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// Aspecto del álbum de evidencias (HDU-12).
///
/// Es solo configuración, igual que <see cref="MenuTabsTheme"/>,
/// <c>DetectiveUITheme</c> y <c>DialogoNeutroTheme</c>: acá no hay nada que haga
/// nada. La lógica que aplica estos valores está en <see cref="AlbumEvidenciasUI"/>.
///
/// Los colores salen de <see cref="Paleta"/>, la misma del resto del juego. Antes el
/// álbum se dibujaba con un azul oscuro propio y tamaños a mano, y parecía de otro
/// programa: era un panel de relleno de HDU-11.
/// </summary>
public static class AlbumUITheme
{
    public static class Colores
    {
        /// <summary>Fondo de la pantalla, cuando no se puede copiar el del celular.
        /// Normalmente SÍ se puede —ver <c>PaginaDeAlbum.TomarAspectoDe</c>—, así que
        /// esto es el plan B para una escena sin prefab de teléfono.</summary>
        public static Color Fondo = Paleta.Marron;

        /// <summary>Fondo del área con scroll.</summary>
        public static Color Scroll = Paleta.MarronOscuro;

        /// <summary>Tarjeta de un grupo (un caso, o una temática).</summary>
        public static Color Tarjeta = Paleta.MarronMedio;

        /// <summary>Fila de una evidencia dentro de su tarjeta.</summary>
        public static Color Fila = Paleta.MarronClaro;

        /// <summary>Etiqueta de temática ("el tipo de señal").</summary>
        public static Color Chip = Paleta.MarronSuave;

        /// <summary>Ranura de un pin en el mosaico. Igual que la de la mochila, que
        /// es de donde se abre el álbum.</summary>
        public static Color RanuraPin = MenuTabsTheme.Colores.FondoRanura;

        public static Color Texto = Paleta.Crema;

        /// <summary>Segundas líneas: la zona, la cuenta de evidencias, la pista del
        /// pie. Arena sobre estos marrones da 4.9:1 o más, que es legible.</summary>
        public static Color TextoSuave = Paleta.Arena;

        /// <summary>Texto de "acá todavía no hay nada". El de menor contraste de la
        /// pantalla: si aclaras <see cref="Fondo"/>, es el primero en caerse.</summary>
        public static Color TextoVacio = new Color(Paleta.Arena.r, Paleta.Arena.g, Paleta.Arena.b, 0.85f);

        /// <summary>Pestaña de agrupación sin elegir, y la elegida. La activa sirve
        /// además de color para el botón «Volver» del detalle.</summary>
        public static Color Pestana = Paleta.MarronMedio;
        public static Color PestanaActiva = Paleta.Madera;
    }

    public static class Medidas
    {
        /// <summary>Fila de las dos formas de agrupar, fija arriba de la pantalla de
        /// evidencias. Ver <c>PaginaDeAlbum.AltoCabeceraPropia</c>.</summary>
        public static float AltoPestanas = 46f;
        public static float AnchoPestanas = 400f;

        /// <summary>Franja del pie donde se lee qué pin hay bajo el mouse.</summary>
        public static float AltoPista = 64f;

        public static float LadoPin = 96f;
        public static float EspacioPin = 16f;

        /// <summary>Columnas del mosaico de pines. Fijo y no "flexible" a propósito:
        /// con el número de columnas fijo, la grilla sabe calcular su propio alto y el
        /// ContentSizeFitter del scroll la mide bien. Con Flexible, el alto que
        /// reporta no es de fiar y el mosaico se solapa con lo de abajo.</summary>
        public static int ColumnasPines = 7;

        public static float AnchoDetalle = 620f;
        public static float AltoDetalle = 480f;

        public static float AnchoBarra = 10f;
        public static float AlturaFila = 56f;
        public static float AlturaBoton = 56f;

        public static int LadoSprite = 64;
        public static int Radio = 22;

        public static RectOffset PaddingContenido = new RectOffset(20, 20, 20, 20);
        public static RectOffset PaddingTarjeta = new RectOffset(18, 18, 14, 14);
        public static RectOffset PaddingDetalle = new RectOffset(24, 24, 20, 20);
        public static float Espaciado = 14f;
    }

    public static class Fuente
    {
        /// <summary>Mango, la de la marca, para rótulos. Es de cartel: buena para tres
        /// palabras y cansada para una lista de frases, de ahí que el cuerpo vaya en la
        /// otra. Mismo criterio que MenuTabsTheme.</summary>
        public static TMP_FontAsset Titulo => FishyUIKit.Titulos;
        public static TMP_FontAsset Cuerpo => FishyUIKit.Cuerpo;

        public static float Seccion = 30f;
        public static float TituloGrupo = 28f;
        public static float Subtitulo = 22f;
        public static float Fila = 23f;
        public static float Pista = 23f;
        public static float Chip = 18f;
        public static float Detalle = 24f;
        public static float Boton = 24f;
    }

    public static class Textos
    {
        /// <summary>Rótulo de la sección de pines. Hoy se usa como subtítulo del
        /// detalle de un pin; el título de la pantalla lo pone el encabezado del
        /// propio celular (ver <c>AccesoAlbumEnElCelular.TituloEvidencias</c>).</summary>
        public static string SeccionPines = "Tus pines";

        public static string PorCaso = "Por caso";
        public static string PorTematica = "Por temática";
        public static string Volver = "Volver";

        public static string PistaDefecto = "Pasa el mouse sobre un pin para ver de qué se trata.";

        /// <summary>
        /// CA4 — La pantalla de evidencias, vacía. El texto del banco de casos
        /// (<c>mensajes_sistema.album_vacio</c>) decía «tu álbum está vacío»; acá se
        /// precisa cuál, porque ahora son dos y el otro puede tener cosas dentro.
        ///
        /// Dice cómo se llena, no solo que está vacío: a un niño/a que abre una
        /// pantalla en blanco hay que darle el siguiente paso, no un diagnóstico.
        /// </summary>
        public static string VacioEvidencias =
            "Tu álbum de evidencias está vacío por ahora.\n" +
            "Resuelve un caso del Modo Detective: cada señal de riesgo que marques bien " +
            "va a quedar guardada acá.";

        /// <summary>
        /// CA4 — La pantalla de recompensas, vacía. Es un estado muy real, y distinto
        /// del anterior: marcar una señal de cuatro deja evidencia y NO alcanza para
        /// el pin, así que se puede llegar acá con el álbum de evidencias lleno.
        /// Por eso explica el umbral en vez de repetir «resuelve un caso».
        /// </summary>
        public static string SinPines =
            "Todavía no tienes pines.\n" +
            "Se ganan acertando más de la mitad de las señales de un caso.";

        /// <summary>Una evidencia que llegó del servidor y de la que no tenemos el
        /// texto, porque el servidor no lo guarda (solo los ids).</summary>
        public static string EvidenciaSinDatos =
            "Esta evidencia la ganaste en otro dispositivo. Vuelve a jugar el caso para ver de qué se trataba.";

        public static string SinClasificar = "Sin clasificar";

        /// <summary>Cómo se llaman las etiquetas del banco cuando se le enseñan a un
        /// niño/a. La etiqueta cruda ("retos_virales") es para el modelo, no para la
        /// pantalla. Lo que no esté acá se muestra con los guiones bajos cambiados por
        /// espacios: mejor eso que no mostrar la temática.</summary>
        private static readonly Dictionary<string, string> _tematicas = new Dictionary<string, string>
        {
            { "grooming",       "Contacto de desconocidos" },
            { "privacidad",     "Datos privados" },
            { "ciberacoso",     "Ciberacoso" },
            { "exclusion",      "Dejar a alguien afuera" },
            { "retos_virales",  "Retos virales" },
            { "presion_social", "Presión del grupo" },
        };

        public static string NombreTematica(string etiqueta)
        {
            if (string.IsNullOrWhiteSpace(etiqueta)) return SinClasificar;
            if (etiqueta == Fishy.Album.AlbumDeEvidencias.ClaveSinTematica) return SinClasificar;

            string clave = etiqueta.Trim();
            if (_tematicas.TryGetValue(clave, out string nombre)) return nombre;

            string suelto = clave.Replace('_', ' ');
            return char.ToUpperInvariant(suelto[0]) + suelto.Substring(1);
        }
    }
}
