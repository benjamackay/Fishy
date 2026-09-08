using System;
using System.Globalization;
using Fishy.Net;

namespace Fishy.UI
{
    /// <summary>
    /// Cómo se le cuenta a un niño/a de qué partida estamos hablando.
    ///
    /// Vive aparte porque las DOS puertas de entrada al juego enseñan la misma lista:
    /// <c>Fishy.UI.AuthScreen</c> (escena Boot) e <c>iniciar</c> (escena Ingresar).
    /// Con una copia en cada una, arreglar la hora en un sitio dejaría el otro mal.
    /// </summary>
    public static class TextoDePartida
    {
        // Los meses van a mano y no por cultura del sistema: Unity arranca con la
        // cultura invariante en muchos builds y saldría "Sep" en inglés en una
        // pantalla para niños.
        private static readonly string[] Meses =
            { "ene", "feb", "mar", "abr", "may", "jun",
              "jul", "ago", "sep", "oct", "nov", "dic" };

        /// <summary>Título + cuándo se guardó, en dos líneas.</summary>
        public static string Etiqueta(PartidaDto partida, bool masReciente)
        {
            if (partida == null) return "Partida";

            string titulo  = masReciente ? "Seguir donde quedaste" : "Partida anterior";
            string detalle = Cuando(partida.fecha_update);

            // El progreso hoy solo lo mueve el Bosque de los Desconocidos, así que en la
            // mayoría de las partidas es 0. Un "0% explorado" no informa: mejor callarlo.
            int avance = (int)Math.Round(partida.progreso);
            if (avance > 0) detalle += $"  ·  {avance}% explorado";

            return $"{titulo}\n{detalle}";
        }

        /// <summary>Convierte la fecha ISO del backend en algo que un niño pueda leer.</summary>
        public static string Cuando(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "sin fecha";

            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                                   DateTimeStyles.RoundtripKind, out var fecha))
                return "sin fecha";

            // Django serializa con el huso del servidor ("…-03:00") y el modo local de
            // ApiManager con UTC ("…Z"); el niño lee la hora de SU reloj. Con offset
            // explícito TryParse ya deja la fecha en local, con "Z" hay que convertirla,
            // y sin zona horaria se respeta tal cual porque no hay nada mejor que suponer.
            if (fecha.Kind == DateTimeKind.Utc) fecha = fecha.ToLocalTime();

            string hora = fecha.ToString("HH:mm", CultureInfo.InvariantCulture);
            var hoy = DateTime.Now.Date;

            if (fecha.Date == hoy)             return $"hoy a las {hora}";
            if (fecha.Date == hoy.AddDays(-1)) return $"ayer a las {hora}";
            return $"{fecha.Day} {Meses[fecha.Month - 1]} {fecha.Year}, {hora}";
        }
    }
}
